using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Records expenses: turns "42 € at the supermarket" into a balanced entry.
/// </summary>
/// <remarks>
/// The shape every write operation here follows: validate the request, load what the domain
/// needs, let the domain build the result, persist it.
/// <para>
/// Note there is no arithmetic in this class. It hands the pieces to
/// <see cref="JournalEntry.RegisterExpense"/> and stores what comes back, so when a second way of
/// recording an expense appears (an import, a rule firing) both go through the same factory.
/// </para>
/// </remarks>
/// <param name="db">Ledger data context.</param>
/// <param name="tenant">The current request's household.</param>
/// <param name="validator">Shape validation for the request.</param>
internal sealed class ExpenseRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    IValidator<RegisterExpenseRequest> validator) : IExpenseRegistrar
{
    /// <inheritdoc />
    public async Task<RegisteredExpense> RegisterAsync(
        RegisterExpenseRequest request,
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
                a => a.Id == request.AccountId && a.HouseholdId == householdId && !a.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("accountId", "That account is not available.");

        var category = await db.Categories
            .FirstOrDefaultAsync(
                c => c.Id == request.CategoryId && c.HouseholdId == householdId && !c.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("categoryId", "That category is not available.");

        var expenseAccount = await GetOrCreateExpenseAccountAsync(
                householdId,
                account.Currency,
                cancellationToken)
            .ConfigureAwait(false);

        RecurringRule? rule = null;

        if (request.Recurrence != ExpenseRecurrence.Once)
        {
            rule = RecurringRule.Create(
                householdId,
                EntryKind.Expense,
                ToFrequency(request.Recurrence),
                account,
                category,
                request.Description,
                Money.Of(request.Amount, account.Currency),
                request.OccurredOn);

            db.RecurringRules.Add(rule);
        }

        var entry = JournalEntry.RegisterExpense(
            householdId,
            request.OccurredOn,
            request.Description,
            paidFrom: account,
            expenseAccount: expenseAccount,
            category: category,
            amount: Money.Of(request.Amount, account.Currency),
            memberId: request.MemberId,
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
    private async Task<RegisteredExpense?> FindByMutationKeyAsync(
        Guid householdId,
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
            ? ExpenseRecurrence.Once
            : ToRecurrence(await db.RecurringRules
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
    private async Task<Account> GetOrCreateExpenseAccountAsync(
        Guid householdId,
        CurrencyCode currency,
        CancellationToken cancellationToken)
    {
        var existing = await db.Accounts
            .FirstOrDefaultAsync(
                a => a.HouseholdId == householdId
                    && a.Type == AccountType.Expense
                    && a.Currency == currency,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var created = Account.Create(
            householdId,
            "Expenses",
            AccountType.Expense,
            currency,
            displayOrder: 900);

        db.Accounts.Add(created);

        return created;
    }

    private static RegisteredExpense Describe(
        JournalEntry entry,
        Account account,
        Category category,
        ExpenseRecurrence recurrence,
        bool wasAlreadyRegistered)
    {
        // The positive side is the one the user thinks in. The negative posting is the same fact
        // from the bank's side.
        var amount = entry.Postings
            .Where(p => p.CategoryId is not null)
            .Sum(p => p.Amount);

        return new RegisteredExpense(
            entry.Id,
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

    /// <summary>
    /// Translates the published recurrence onto the domain's own frequency.
    /// </summary>
    /// <param name="recurrence">Recurrence as the client sent it. Must not be
    /// <see cref="ExpenseRecurrence.Once"/>: a one-off expense creates no rule.</param>
    /// <returns>The matching domain frequency.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the value has no counterpart.</exception>
    /// <remarks>
    /// Written out member by member rather than cast across. The two enums are separate on
    /// purpose — one is a frozen wire contract, the other is free to change — and a numeric cast
    /// silently ties their ordering together: inserting a member in the middle of the domain enum
    /// would relabel every rule already stored, without a compiler error and without a failing
    /// test. This switch stops compiling instead.
    /// </remarks>
    private static RecurrenceFrequency ToFrequency(ExpenseRecurrence recurrence) => recurrence switch
    {
        ExpenseRecurrence.Monthly => RecurrenceFrequency.Monthly,
        ExpenseRecurrence.BiMonthly => RecurrenceFrequency.BiMonthly,
        ExpenseRecurrence.Quarterly => RecurrenceFrequency.Quarterly,
        ExpenseRecurrence.Once => throw new ArgumentOutOfRangeException(
            nameof(recurrence),
            recurrence,
            "A one-off expense creates no recurring rule."),
        _ => throw new ArgumentOutOfRangeException(
            nameof(recurrence),
            recurrence,
            "Unknown recurrence."),
    };

    /// <summary>
    /// Translates a stored frequency back onto the published recurrence.
    /// </summary>
    /// <param name="frequency">Frequency as the rule holds it.</param>
    /// <returns>The matching contract value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the value has no counterpart.</exception>
    /// <remarks>
    /// The inverse of <see cref="ToFrequency"/>, and explicit for the same reason. When the domain
    /// grows a frequency the contract does not publish yet, this is where it has to be decided
    /// rather than leaking out as an unnamed number.
    /// </remarks>
    private static ExpenseRecurrence ToRecurrence(RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Monthly => ExpenseRecurrence.Monthly,
        RecurrenceFrequency.BiMonthly => ExpenseRecurrence.BiMonthly,
        RecurrenceFrequency.Quarterly => ExpenseRecurrence.Quarterly,
        _ => throw new ArgumentOutOfRangeException(
            nameof(frequency),
            frequency,
            "The rule has a frequency the contract does not publish."),
    };

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
