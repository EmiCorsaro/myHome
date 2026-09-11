using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Transfers;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Moves money between two of the household's own accounts (story 011).
/// </summary>
/// <remarks>
/// This is not an income or an expense: the domain method it calls into never gives either leg a
/// category unless the destination account is not controlled, in which case that leg is the
/// household's only chance to classify money leaving the tracked perimeter (RF-14, RF-15, RF-16,
/// RF-17). Every other rule here mirrors <see cref="ExpenseRegistrar"/> and
/// <see cref="IncomeRegistrar"/>: the field-mapped validation lives here, the domain repeats the
/// invariant it cannot afford to trust a caller with (RF-8, RF-11, RF-12, RF-19, RF-21).
/// </remarks>
internal sealed class TransferRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    IValidator<RegisterTransferRequest> validator) : ITransferRegistrar
{
    public async Task<RegisteredTransfer> RegisterAsync(
        RegisterTransferRequest request,
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

        var fromAccount = await db.Accounts
            .FirstOrDefaultAsync(
                a => a.PublicId == request.FromAccountId
                    && a.HouseholdId == householdId
                    && !a.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("fromAccountId", "That account is not available.");

        var toAccount = await db.Accounts
            .FirstOrDefaultAsync(
                a => a.PublicId == request.ToAccountId
                    && a.HouseholdId == householdId
                    && !a.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("toAccountId", "That account is not available.");

        // RF-8
        if (fromAccount.Id == toAccount.Id)
        {
            throw Invalid("toAccountId", SameAccountMessage);
        }

        // RF-19: the domain enforces the very same rule; this turns it into a message naming the
        // field the member would correct, instead of an exception travelling up from the model.
        if (fromAccount.Type == AccountType.CreditCard)
        {
            throw Invalid("fromAccountId", CashAdvanceMessage);
        }

        Category? category = null;

        // RF-14, RF-15: a category is only required, and only ever looked at, when the destination
        // is not controlled — that is the household's one chance to classify this money.
        if (!toAccount.IsTracked)
        {
            if (request.CategoryId is not { } categoryPublicId)
            {
                throw Invalid("categoryId", CategoryRequiredMessage);
            }

            category = await db.Categories
                .FirstOrDefaultAsync(
                    c => c.PublicId == categoryPublicId && c.HouseholdId == householdId && !c.IsArchived,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw Invalid("categoryId", "That category is not available.");

            // RF-17
            if (category.Kind != CategoryKind.Expense)
            {
                throw Invalid("categoryId", CategoryMustBeExpenseMessage);
            }
        }

        // RF-21: both accounts are checked, not only the one paying.
        if (await OpeningBalances
                .IsBeforeOpeningBalanceAsync(db, fromAccount.Id, request.OccurredOn, cancellationToken)
                .ConfigureAwait(false)
            || await OpeningBalances
                .IsBeforeOpeningBalanceAsync(db, toAccount.Id, request.OccurredOn, cancellationToken)
                .ConfigureAwait(false))
        {
            throw Invalid("occurredOn", TransferBeforeOpeningBalanceMessage);
        }

        var entry = JournalEntry.RegisterTransfer(
            householdId,
            request.OccurredOn,
            request.Description,
            from: fromAccount,
            to: toAccount,
            amount: Money.Of(request.Amount, fromAccount.Currency),
            category: category,
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

        return Describe(entry, fromAccount, toAccount, category, wasAlreadyRegistered: false);
    }

    private async Task<RegisteredTransfer?> FindByMutationKeyAsync(
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

        var fromPosting = entry.Postings.Single(p => p.Amount < 0m);
        var toPosting = entry.Postings.Single(p => p.Amount > 0m);

        var fromAccount = await db.Accounts
            .FirstAsync(a => a.Id == fromPosting.AccountId, cancellationToken)
            .ConfigureAwait(false);

        var toAccount = await db.Accounts
            .FirstAsync(a => a.Id == toPosting.AccountId, cancellationToken)
            .ConfigureAwait(false);

        Category? category = null;

        if (toPosting.CategoryId is { } categoryId)
        {
            category = await db.Categories
                .FirstAsync(c => c.Id == categoryId, cancellationToken)
                .ConfigureAwait(false);
        }

        return Describe(entry, fromAccount, toAccount, category, wasAlreadyRegistered: true);
    }

    private static RegisteredTransfer Describe(
        JournalEntry entry,
        Account from,
        Account to,
        Category? category,
        bool wasAlreadyRegistered)
    {
        var amount = entry.Postings.Single(p => p.Amount > 0m).Amount;

        return new RegisteredTransfer(
            entry.PublicId,
            entry.OccurredOn,
            entry.Description,
            decimal.Round(amount, 2, MidpointRounding.ToEven),
            from.Currency.Value,
            from.Name,
            to.Name,
            category?.Name,
            category?.ColorIndex,
            wasAlreadyRegistered);
    }

    /// <summary>Shown when the origin and destination accounts are the same (RF-8).</summary>
    private const string SameAccountMessage =
        "The origin and destination accounts cannot be the same: nothing would move.";

    /// <summary>
    /// Shown when the origin account is a credit card (RF-19).
    /// </summary>
    /// <remarks>
    /// A cash advance accrues interest from day one, with its own rate, so registering it as a
    /// plain transfer would hide its real cost; modelling that interest is out of this story's
    /// scope.
    /// </remarks>
    private const string CashAdvanceMessage =
        "A credit card cannot be the origin of a transfer: a cash advance is not admitted yet, it "
            + "accrues its own interest from day one.";

    /// <summary>Shown when the destination is not controlled and no category was named (RF-15).</summary>
    private const string CategoryRequiredMessage =
        "That account is not controlled: choose a category, this is the only chance to classify "
            + "this money.";

    /// <summary>Shown when the category named for a transfer classifies income (RF-17).</summary>
    private const string CategoryMustBeExpenseMessage =
        "That category classifies income, and money leaving the tracked accounts is always spent, "
            + "never earned.";

    /// <summary>
    /// Shown when a transfer is dated earlier than the opening balance declared for either account
    /// (RF-21).
    /// </summary>
    private const string TransferBeforeOpeningBalanceMessage =
        "A transfer cannot be dated before either account's opening balance.";

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
