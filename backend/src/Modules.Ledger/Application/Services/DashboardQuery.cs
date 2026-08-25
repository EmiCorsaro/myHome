using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

internal sealed class DashboardQuery(
    LedgerDbContext db,
    ITenantContext tenant,
    IHouseholdDirectory households) : IDashboardQuery
{
    private const int MaxMovements = 200;

    public async Task<DashboardSummary> GetMonthlySummaryAsync(
        DateOnly? reference = null,
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();

        var household = await households.GetCurrentAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The request resolved to a household that no longer exists.");

        var today = reference ?? TodayIn(household.TimeZoneId);
        var start = new DateOnly(today.Year, today.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var accounts = await db.Accounts
            .Where(a => a.HouseholdId == householdId && !a.IsArchived)
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var categories = await db.Categories
            .Where(c => c.HouseholdId == householdId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var balances = await AccountDirectory
            .BalancesByAccountAsync(db, householdId, cancellationToken)
            .ConfigureAwait(false);

        var movements = await NominalMovementsAsync(householdId, start, end, cancellationToken)
            .ConfigureAwait(false);

        var income = -movements
            .Where(m => m.Type == AccountType.Income)
            .Sum(m => m.Amount);

        var expense = movements
            .Where(m => m.Type == AccountType.Expense)
            .Sum(m => m.Amount);

        var byCategory = SummariseByCategory(movements, categories, expense);

        var realAccounts = accounts
            .Where(a => a.IsReal)
            .Select(a => AccountDirectory.ToSummary(a, balances.GetValueOrDefault(a.Id)))
            .ToList();

        var trackedBalance = accounts
            .Where(a => a.IsTracked)
            .Sum(a => balances.GetValueOrDefault(a.Id));

        var monthMovements = await MovementsAsync(
                householdId,
                start,
                end,
                accounts,
                categories,
                cancellationToken)
            .ConfigureAwait(false);

        return new DashboardSummary(
            household.BaseCurrency,
            start,
            end,
            Round(income),
            Round(expense),
            Round(income - expense),
            Round(trackedBalance),

            IsProjectionAvailable: false,
            byCategory,
            realAccounts,
            monthMovements);
    }

    private async Task<List<NominalMovement>> NominalMovementsAsync(
        int householdId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var query =
            from posting in db.Postings
            join account in db.Accounts on posting.AccountId equals account.Id
            join entry in db.Entries on posting.JournalEntryId equals entry.Id
            where account.HouseholdId == householdId
                && (account.Type == AccountType.Income || account.Type == AccountType.Expense)
                && entry.OccurredOn >= start
                && entry.OccurredOn <= end
            select new NominalMovement(account.Type, posting.AmountBase, posting.CategoryId);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private static List<CategoryTotal> SummariseByCategory(
        List<NominalMovement> movements,
        List<Category> categories,
        decimal totalExpense)
    {
        var byId = categories.ToDictionary(c => c.Id);

        return
        [
            .. movements
                .Where(m => m.Type == AccountType.Expense && m.CategoryId is not null)
                .GroupBy(m => m.CategoryId!.Value)
                .Select(group =>
                {
                    var total = group.Sum(m => m.Amount);
                    var category = byId.GetValueOrDefault(group.Key);

                    return new CategoryTotal(

                        category?.PublicId ?? Guid.Empty,

                        category?.Name ?? "Uncategorised",
                        category?.ColorIndex ?? Category.PaletteSize,
                        Round(total),
                        totalExpense == 0m
                            ? 0m
                            : decimal.Round(total / totalExpense, 4, MidpointRounding.ToEven));
                })
                .OrderByDescending(c => c.Total)
                .ThenBy(c => c.Name, StringComparer.CurrentCulture),
        ];
    }

    private async Task<List<LedgerEntrySummary>> MovementsAsync(
        int householdId,
        DateOnly start,
        DateOnly end,
        List<Account> accounts,
        List<Category> categories,
        CancellationToken cancellationToken)
    {
        var entries = await db.Entries
            .Where(e => e.HouseholdId == householdId
                && e.OccurredOn >= start
                && e.OccurredOn <= end)
            .OrderByDescending(e => e.OccurredOn)
            .ThenByDescending(e => e.CreatedAt)
            .Take(MaxMovements)
            .Include(e => e.Postings)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var accountsById = accounts.ToDictionary(a => a.Id);
        var categoriesById = categories.ToDictionary(c => c.Id);

        var lines = new List<LedgerEntrySummary>(entries.Count);

        foreach (var entry in entries)
        {
            var cash = entry.Postings.FirstOrDefault(
                p => accountsById.TryGetValue(p.AccountId, out var account) && account.IsReal);

            if (cash is null)
            {
                continue;
            }

            var classified = entry.Postings.FirstOrDefault(p => p.CategoryId is not null);
            var category = classified?.CategoryId is { } id
                ? categoriesById.GetValueOrDefault(id)
                : null;

            lines.Add(new LedgerEntrySummary(
                entry.PublicId,
                entry.OccurredOn,
                entry.Description,
                entry.Kind.ToContractName(),
                Round(cash.Amount),
                accountsById[cash.AccountId].Name,
                category?.Name,
                category?.ColorIndex,
                entry.RecurringRuleId is not null));
        }

        return lines;
    }

    private static DateOnly TodayIn(string timeZoneId)
    {
        TimeZoneInfo zone;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date);
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.ToEven);

    private sealed record NominalMovement(AccountType Type, decimal Amount, int? CategoryId);
}
