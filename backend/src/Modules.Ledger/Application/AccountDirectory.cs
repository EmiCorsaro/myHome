using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Lists accounts with their balances.
/// </summary>
/// <param name="db">Ledger data context.</param>
/// <param name="tenant">The current request's household.</param>
internal sealed class AccountDirectory(LedgerDbContext db, ITenantContext tenant)
    : IAccountDirectory
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountSummary>> ListRealAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();

        var accounts = await db.Accounts
            .Where(a => a.HouseholdId == householdId
                && !a.IsArchived
                && a.Type != AccountType.Income
                && a.Type != AccountType.Expense)
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var balances = await BalancesByAccountAsync(db, householdId, cancellationToken)
            .ConfigureAwait(false);

        return [.. accounts.Select(a => ToSummary(a, balances.GetValueOrDefault(a.Id)))];
    }

    /// <summary>
    /// Sums every posting of the household, grouped by account.
    /// </summary>
    /// <param name="db">Ledger data context.</param>
    /// <param name="householdId">Household to compute for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Balance per account. Accounts with no postings are absent, meaning zero.</returns>
    /// <remarks>
    /// One aggregation for all accounts rather than one query each. Invisible at four accounts,
    /// but it stays sane at forty and leaves no excuse for a stored balance column.
    /// </remarks>
    internal static async Task<Dictionary<Guid, decimal>> BalancesByAccountAsync(
        LedgerDbContext db,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await db.Postings
            .Where(p => db.Accounts.Any(a => a.Id == p.AccountId && a.HouseholdId == householdId))
            .GroupBy(p => p.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(p => p.AmountBase) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(r => r.AccountId, r => r.Balance);
    }

    /// <summary>
    /// Maps an account and its balance to the published contract.
    /// </summary>
    /// <param name="account">Account to map.</param>
    /// <param name="balance">Its balance.</param>
    /// <returns>The summary.</returns>
    internal static AccountSummary ToSummary(Account account, decimal balance) => new(
        account.Id,
        account.Name,
        account.Type.ToContractName(),
        account.Currency.Value,
        decimal.Round(balance, 2, MidpointRounding.ToEven),
        account.IsTracked,
        account.MinimumBufferTarget);
}
