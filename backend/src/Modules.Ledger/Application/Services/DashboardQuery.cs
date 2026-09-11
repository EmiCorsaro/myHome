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
            .Where(m => m.Kind == CategoryKind.Income)
            .Sum(m => m.Amount);

        var expense = movements
            .Where(m => m.Kind == CategoryKind.Expense)
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

    /// <summary>
    /// The classified side of every movement of the period, used to build the month's income,
    /// expense and spend-by-category report.
    /// </summary>
    /// <remarks>
    /// A movement is "nominal" here because its posting carries a category, not because of what
    /// type of account it happens to sit on: an income or an expense always puts the category on
    /// its one nominal-account leg, and story 011's transfer to an uncontrolled account puts it on
    /// that real account's own leg instead, since there is no nominal account of its own to carry
    /// it. Both are picked up by the same rule, so a transfer that classifies itself joins this
    /// report without the query needing to know a transfer even exists (RF-16, story 011). The
    /// posting's economic nature — income or expense — comes from the category's own kind, which
    /// always agrees with the nominal account type wherever one exists (RF-17, story 011).
    /// <para/>
    /// Each movement also carries whether the real account on the other side of its entry is
    /// controlled. That is what lets the spend-by-category report leave out the movements of an
    /// uncontrolled account (RF-10, story 004) without also touching the month's income and
    /// expense totals, which RF-10 never asked to change. The entry's other leg is found through
    /// the sibling posting that shares its journal entry — every entry that carries a category
    /// carries exactly one classified posting and one real, unclassified one.
    /// <para/>
    /// A voided entry and its reversal (story 013, RF-4) are left out here too, on top of the
    /// "carries a category" rule story 011 generalised: a movement that never really counted does
    /// not get a phantom, zero-total row of its own in the category report. Their contribution to
    /// the month's income and expense totals is exactly zero anyway — the reversal negates the
    /// original one for one — so leaving both out here changes nothing those totals ever promised.
    /// </remarks>
    private async Task<List<NominalMovement>> NominalMovementsAsync(
        int householdId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var query =
            from posting in db.Postings
            where posting.CategoryId != null
            join category in db.Categories on posting.CategoryId equals (int?)category.Id
            join entry in db.Entries on posting.JournalEntryId equals entry.Id
            join realPosting in db.Postings on posting.JournalEntryId equals realPosting.JournalEntryId
            join realAccount in db.Accounts on realPosting.AccountId equals realAccount.Id
            where entry.HouseholdId == householdId
                && entry.OccurredOn >= start
                && entry.OccurredOn <= end
                && realPosting.Id != posting.Id
                && !entry.IsVoided
                && entry.ReversalOfEntryId == null
            select new NominalMovement(
                category.Kind, posting.AmountBase, posting.CategoryId, realAccount.IsTracked);

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
                .Where(m => m.Kind == CategoryKind.Expense
                    && m.CategoryId is not null
                    && m.IsRealAccountTracked)
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

    private static DateOnly TodayIn(string timeZoneId) =>
        HouseholdClock.TodayIn(timeZoneId, TimeProvider.System);

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.ToEven);

    private sealed record NominalMovement(
        CategoryKind Kind, decimal Amount, int? CategoryId, bool IsRealAccountTracked);
}
