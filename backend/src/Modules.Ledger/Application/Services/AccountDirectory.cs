using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

internal sealed class AccountDirectory(LedgerDbContext db, ITenantContext tenant)
    : IAccountDirectory
{
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

    internal static async Task<Dictionary<int, decimal>> BalancesByAccountAsync(
        LedgerDbContext db,
        int householdId,
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

    internal static AccountSummary ToSummary(Account account, decimal balance) => new(
        account.PublicId,
        account.Name,
        account.Type.ToContractName(),
        account.Currency.Value,
        decimal.Round(balance, 2, MidpointRounding.ToEven),
        account.IsTracked,
        account.MinimumBufferTarget);
}
