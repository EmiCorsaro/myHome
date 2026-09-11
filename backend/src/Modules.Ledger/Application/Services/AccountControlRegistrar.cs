using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Marks an account as controlled or not, and fixes or retires its minimum buffer (story 004).
/// </summary>
/// <remarks>
/// No role is asked for anywhere in here: any member of the household may act on either setting
/// (RF-14). The only thing that is checked is that the account belongs to the household making
/// the request, and that a credit card never loses the two guarantees that make its balance
/// readable as debt rather than as spare cash (RF-11, RF-12).
/// </remarks>
internal sealed class AccountControlRegistrar(LedgerDbContext db, ITenantContext tenant)
    : IAccountControlRegistrar
{
    public async Task<AccountSummary> SetTrackedAsync(
        Guid accountId,
        SetAccountTrackedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var householdId = tenant.RequireHouseholdId();
        var account = await RequireAccountAsync(householdId, accountId, cancellationToken)
            .ConfigureAwait(false);

        // The domain enforces the very same rule; this is what turns it into a message the member
        // asked for a change is shown, instead of an exception travelling up from the model (RF-12).
        if (!request.IsTracked && account.Type == AccountType.CreditCard)
        {
            throw Invalid("isTracked", CreditCardCannotBeUntrackedMessage);
        }

        account.SetTracked(request.IsTracked);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await DescribeAsync(account, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountSummary> SetMinimumBufferTargetAsync(
        Guid accountId,
        SetMinimumBufferTargetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var householdId = tenant.RequireHouseholdId();
        var account = await RequireAccountAsync(householdId, accountId, cancellationToken)
            .ConfigureAwait(false);

        if (request.Amount is { } amount)
        {
            // RF-7: a negative floor is a validation error, not a domain crash.
            if (amount < 0m)
            {
                throw Invalid("amount", NegativeBufferMessage);
            }

            // RF-11: a credit card's balance is debt, not liquidity.
            if (account.Type == AccountType.CreditCard)
            {
                throw Invalid("amount", CreditCardCannotHaveBufferMessage);
            }
        }

        account.SetMinimumBufferTarget(request.Amount);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await DescribeAsync(account, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The account the request names, when the household may act on it.
    /// </summary>
    /// <remarks>
    /// An account of another household, an archived one, and one the ledger keeps to classify
    /// income and expense all get the same answer: it is not available. Telling them apart would
    /// say something about accounts the caller is not meant to know exist.
    /// </remarks>
    private async Task<Account> RequireAccountAsync(
        int householdId,
        Guid accountId,
        CancellationToken cancellationToken) =>
        await db.Accounts
            .FirstOrDefaultAsync(
                a => a.PublicId == accountId
                    && a.HouseholdId == householdId
                    && !a.IsArchived
                    && a.Type != AccountType.Income
                    && a.Type != AccountType.Expense,
                cancellationToken)
            .ConfigureAwait(false)
        ?? throw Invalid("accountId", "Esa cuenta no está disponible.");

    private async Task<AccountSummary> DescribeAsync(
        Account account,
        CancellationToken cancellationToken)
    {
        var balances = await AccountDirectory
            .BalancesByAccountAsync(db, account.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        return AccountDirectory.ToSummary(account, balances.GetValueOrDefault(account.Id));
    }

    private const string CreditCardCannotBeUntrackedMessage =
        "Una tarjeta de crédito no puede excluirse del disponible: su saldo es deuda, no dinero "
            + "disponible.";

    private const string CreditCardCannotHaveBufferMessage =
        "Una tarjeta de crédito no admite un colchón mínimo: su saldo es deuda, no liquidez.";

    private const string NegativeBufferMessage = "El colchón mínimo no puede ser negativo.";

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
