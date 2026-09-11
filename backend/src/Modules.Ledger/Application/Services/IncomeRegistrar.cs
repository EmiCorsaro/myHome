using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Incomes;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

internal sealed class IncomeRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    IValidator<RegisterIncomeRequest> validator,
    IHouseholdDirectory households) : IIncomeRegistrar
{
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

        if (account.Type == AccountType.CreditCard)
        {
            throw Invalid("accountId", CreditCardMessage);
        }

        var category = await db.Categories
            .FirstOrDefaultAsync(
                c => c.PublicId == request.CategoryId && c.HouseholdId == householdId && !c.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("categoryId", "That category is not available.");

        if (category.Kind != CategoryKind.Income)
        {
            throw Invalid("categoryId", "That category classifies an expense, not income.");
        }

        if (await OpeningBalances
                .IsBeforeOpeningBalanceAsync(db, account.Id, request.OccurredOn, cancellationToken)
                .ConfigureAwait(false))
        {
            throw Invalid("occurredOn", IncomeBeforeOpeningBalanceMessage);
        }

        int? memberId = null;

        if (request.MemberId is { } memberPublicId)
        {
            memberId = await households
                .ResolveMemberAsync(memberPublicId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw Invalid("memberId", "That member is not in this household.");
        }

        var incomeAccount = await GetOrCreateIncomeAccountAsync(
                householdId,
                account.Currency,
                cancellationToken)
            .ConfigureAwait(false);

        var entry = JournalEntry.RegisterIncome(
            householdId,
            request.OccurredOn,
            request.Description,
            depositTo: account,
            incomeAccount: incomeAccount,
            category: category,
            amount: Money.Of(request.Amount, account.Currency),
            memberId: memberId,
            clientMutationId: request.ClientMutationId);

        db.Entries.Add(entry);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (request.ClientMutationId is { Length: > 0 } raced)
        {
            db.ChangeTracker.Clear();

            var recovered = await FindByMutationKeyAsync(householdId, raced, cancellationToken)
                .ConfigureAwait(false);

            if (recovered is null)
            {
                throw;
            }

            return recovered;
        }

        return Describe(entry, account, category, wasAlreadyRegistered: false);
    }

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

        return Describe(entry, account, category, wasAlreadyRegistered: true);
    }

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

        // The postings built right after this call reference the account by its numeric id, not
        // through a tracked navigation, so the id must already exist before they are built.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return created;
    }

    private static RegisteredIncome Describe(
        JournalEntry entry,
        Account account,
        Category category,
        bool wasAlreadyRegistered)
    {
        var amount = entry.Postings
            .Where(p => p.CategoryId is null)
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
            wasAlreadyRegistered);
    }

    /// <summary>
    /// Shown when a credit card is named as the account an income is deposited into (RF-13).
    /// </summary>
    /// <remarks>
    /// A credit card's balance is debt, and money "coming into" it is never a household receipt: it
    /// is the correction of the expense that caused the debt in the first place. Reusing a generic
    /// "invalid account" message here would name the symptom, not the cause, so this story gets its
    /// own wording rather than one written for another scenario.
    /// </remarks>
    private const string CreditCardMessage =
        "A credit to a credit card is not an income: correct the expense that caused it instead.";

    /// <summary>
    /// Shown when an income is dated earlier than the opening balance already declared for its
    /// account (RF-17).
    /// </summary>
    /// <remarks>
    /// This story does not reuse <see cref="OpeningBalances.MovementBeforeOpeningMessage"/> nor
    /// <c>ExpenseRegistrar</c>'s equivalent constant: both describe a different party arriving too
    /// early. Here it is the new income itself that is dated before the account's declared start.
    /// </remarks>
    private const string IncomeBeforeOpeningBalanceMessage =
        "An income cannot be dated before the account's opening balance.";

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
