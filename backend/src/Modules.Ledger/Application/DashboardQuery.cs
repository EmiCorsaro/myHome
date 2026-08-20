using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Builds the landing screen's figures out of the ledger.
/// </summary>
/// <remarks>
/// Every number here is the sum of postings with a different filter: balance is all postings on
/// real accounts, monthly expense is postings on expense accounts inside the period, the
/// breakdown is the same query grouped by category. No second source of truth to reconcile.
/// <para>
/// Aggregation happens in memory after one round trip instead of in several grouped queries. At
/// a few thousand postings a year the difference is unmeasurable and the code stays readable
/// without thinking in SQL.
/// </para>
/// </remarks>
/// <param name="db">Ledger data context.</param>
/// <param name="tenant">The current request's household.</param>
/// <param name="households">Household directory, for the base currency and time zone.</param>
internal sealed class DashboardQuery(
    LedgerDbContext db,
    ITenantContext tenant,
    IHouseholdDirectory households) : IDashboardQuery
{
    /// <summary>Cap on the movements returned for one month.</summary>
    /// <remarks>
    /// A household records tens of movements a month, not hundreds. The cap is here so a bad
    /// import cannot turn one dashboard request into a several-megabyte response.
    /// </remarks>
    private const int MaxMovements = 200;

    /// <inheritdoc />
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

        // Income lands as a negative posting on the income account. Flip the sign so the figure
        // reads the way a person expects.
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

            // Turned on in sub-phase 1.2, once there are enough rules to project from. Until
            // then the screen says so rather than relabelling this month's total as a forecast.
            IsProjectionAvailable: false,
            byCategory,
            realAccounts,
            monthMovements);
    }

    /// <summary>
    /// Loads the period's postings on nominal accounts, which is where income and expense
    /// accumulate.
    /// </summary>
    /// <param name="householdId">Household.</param>
    /// <param name="start">First day of the period.</param>
    /// <param name="end">Last day of the period, inclusive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One row per posting.</returns>
    private async Task<List<NominalMovement>> NominalMovementsAsync(
        Guid householdId,
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
                        group.Key,

                        // Archived categories keep their history, so the name has to survive
                        // after they disappear from the pickers.
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

    /// <summary>
    /// Loads the month's entries and flattens each one into a single line.
    /// </summary>
    /// <param name="householdId">Household.</param>
    /// <param name="start">First day of the period.</param>
    /// <param name="end">Last day of the period, inclusive.</param>
    /// <param name="accounts">The household's accounts, already loaded.</param>
    /// <param name="categories">The household's categories, already loaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entries, newest first.</returns>
    private async Task<List<LedgerEntrySummary>> MovementsAsync(
        Guid householdId,
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
            // The line worth reading is the one that touched real money. The nominal side only
            // makes the books balance.
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
                entry.Id,
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

    /// <summary>
    /// Today's date in the household's time zone.
    /// </summary>
    /// <param name="timeZoneId">IANA time zone identifier.</param>
    /// <returns>The local date.</returns>
    /// <remarks>
    /// Not <c>DateTime.Today</c>: the server can be anywhere, and a household in Madrid would see
    /// the first hours of each month reported against the previous one.
    /// </remarks>
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

    /// <summary>
    /// One posting on a nominal account, reduced to what the totals need.
    /// </summary>
    /// <param name="Type">Type of the account it landed on.</param>
    /// <param name="Amount">Signed amount in base currency.</param>
    /// <param name="CategoryId">Classifying category, if any.</param>
    private sealed record NominalMovement(AccountType Type, decimal Amount, Guid? CategoryId);
}
