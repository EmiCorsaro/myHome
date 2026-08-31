using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Income;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application.Interfaces.IncomeRegister;
using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Records income: turns "42 € at the supermarket" into a balanced entry.
/// </summary>
/// <remarks>
/// The shape every write operation here follows: validate the request, load what the domain
/// needs, let the domain build the result, persist it.
/// <para>
/// Note there is no arithmetic in this class. It hands the pieces to
/// <see cref="JournalEntry.RegisterIncome"/> and stores what comes back, so when a second way of
/// recording income appears (an import, a rule firing) both go through the same factory.
/// </para>
/// </remarks>
/// <param name="db">Ledger data context.</param>
/// <param name="tenant">The current request's household.</param>
/// <param name="validator">Shape validation for the request.</param>
internal sealed class IncomeRegister(
    LedgerDbContext db,
    ITenantContext tenant,
    IValidator<RegisterIncomeRequest> validator) : IIncomeRegister
{
    /// <inheritdoc />
    public async Task<RegisteredIncome> RegisterAsync(
        RegisterIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await validator.ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!validation.IsValid)
        {
            throw new ValidationFailedException(
                validation.Errors
                    .GroupBy(e => ToFieldName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var householdId = tenant.RequireHouseholdId();

        // Idempotency, first pass: the client already sent this one and got no answer.
        if (request.ClientMutationId is { Length: > 0 } key)
        {
            var already = await FindByMutationKeyAsync(householdId, key, cancellationToken)
                .ConfigureAwait(false);

            if (already is not null)
            {
                return already;
            }
        }

        var account = await db.Accounts
            .FirstOrDefaultAsync(
                a => a.PublicId == request.AccountId && a.HouseholdId == householdId && !a.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("accountId", "That account is not available.");

        var category = await db.Categories
            .FirstOrDefaultAsync(
                c => c.PublicId == request.CategoryId && c.HouseholdId == householdId && !c.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("categoryId", "That category is not available.");

        var incomeAccount = await GetOrCreateIncomeAccountAsync(
                householdId,
                account.Currency,
                cancellationToken)
            .ConfigureAwait(false);

        RecurringRule? rule = null;

        if (request.Recurrence != IncomeRecurrence.Once)
        {
            rule = RecurringRule.Create(
                householdId,
                EntryKind.Income,
                (RecurrenceFrequency)request.Recurrence,
                account,
                category,
                request.Description,
                Money.Of(request.Amount, account.Currency),
                request.OccurredOn);

            db.RecurringRules.Add(rule);
        }

        var entry = JournalEntry.RegisterIncome(
            householdId,
            request.OccurredOn,
            request.Description,
            paidFrom: account,
            incomeAccount: incomeAccount,
            category: category,
            amount: Money.Of(request.Amount, account.Currency),
            memberId: null, //NEED TO BE FIXED
            clientMutationId: request.ClientMutationId,
            recurringRuleId: rule?.Id);

        db.Entries.Add(entry);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (request.ClientMutationId is { Length: > 0 } raced)
        {
            // Two requests with the same key arrived close enough that both passed the check
            // above, and the unique index rejected the loser. Without this the user would see an
            // error for an expense that was in fact saved.
            db.ChangeTracker.Clear();

            var recovered = await FindByMutationKeyAsync(householdId, raced, cancellationToken)
                .ConfigureAwait(false);

            if (recovered is null)
            {
                // Not a duplicate: the write failed for some other reason and must not be
                // reported as a success.
                throw;
            }

            return recovered;
        }

        return Describe(entry, account, category, request.Recurrence, wasAlreadyRegistered: false);
    }

    /// <summary>
    /// Finds an already-recorded expense by its idempotency key.
    /// </summary>
    /// <param name="householdId">Household to search in.</param>
    /// <param name="key">Key sent by the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The expense, or <see langword="null"/> if this key has not been seen.</returns>
    private async Task<RegisteredIncome?> FindByMutationKeyAsync(
        int householdId,
        string key,
        CancellationToken cancellationToken)
    {
        var entry = await db.Entries
            .Include(e => e.Postings)
            .FirstOrDefaultAsync(
                e => e.HouseholdId == householdId && e.ClientMutationId == key,
                cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return null;
        }

        var accountId = entry.Postings.First(p => p.CategoryId is null).AccountId;
        var categoryId = entry.Postings.First(p => p.CategoryId is not null).CategoryId;

        var account = await db.Accounts
            .FirstAsync(a => a.Id == accountId, cancellationToken)
            .ConfigureAwait(false);

        var category = await db.Categories
            .FirstAsync(c => c.Id == categoryId, cancellationToken)
            .ConfigureAwait(false);

        var recurrence = entry.RecurringRuleId is null
            ? IncomeRecurrence.Once
            : (IncomeRecurrence)(await db.RecurringRules
                .Where(r => r.Id == entry.RecurringRuleId)
                .Select(r => r.Frequency)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false));

        return Describe(entry, account, category, recurrence, wasAlreadyRegistered: true);
    }

    /// <summary>
    /// Returns the household's nominal expense account, creating it if it is not there yet.
    /// </summary>
    /// <param name="householdId">Household.</param>
    /// <param name="currency">Currency the account must work in.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The expense account.</returns>
    /// <remarks>
    /// Created on demand instead of assumed. It is an accounting artefact the user never sees, so
    /// there is no reason to turn its absence into a startup ordering problem.
    /// </remarks>
    private async Task<Account> GetOrCreateIncomeAccountAsync(
        int householdId,
        CurrencyCode currency,
        CancellationToken cancellationToken)
    {
        var existing = await db.Accounts
            .FirstOrDefaultAsync(
                a => a.HouseholdId == householdId
                    && a.Type == AccountType.Income
                    && a.Currency == currency,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var created = Account.Create(
            householdId,
            "Income",
            AccountType.Income,
            currency,
            displayOrder: 900);

        db.Accounts.Add(created);

        return created;
    }

    private static RegisteredIncome Describe(
        JournalEntry entry,
        Account account,
        Category category,
        IncomeRecurrence recurrence,
        bool wasAlreadyRegistered)
    {
        // The positive side is the one the user thinks in. The negative posting is the same fact
        // from the bank's side.
        var amount = entry.Postings
            .Where(p => p.CategoryId is not null)
            .Sum(p => p.Amount);

        return new RegisteredIncome(
            entry.PublicId,
            entry.OccurredOn,
            entry.Description,
            decimal.Round(amount, 2, MidpointRounding.ToEven),
            account.Currency.Value,
            account.Name,
            category.Name,
            category.ColorIndex,
            recurrence,
            wasAlreadyRegistered);
    }

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>
    /// Maps a C# property name to the JSON field the client sent, so each message lands under the
    /// right input.
    /// </summary>
    /// <param name="propertyName">Property name reported by the validator.</param>
    /// <returns>The camelCase field name.</returns>
    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
