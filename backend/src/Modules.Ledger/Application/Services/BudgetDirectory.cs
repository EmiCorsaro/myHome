using MyHome.Modules.Ledger.Contracts.Budget;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// The budget the household declared for a month: its lines and what they commit.
/// </summary>
/// <remarks>
/// The total is computed here and published as a figure, not left for the screen to add up. Two
/// implementations of the household's arithmetic is one too many, and the disagreement always
/// surfaces on the number the household is about to make a decision with.
/// </remarks>
internal sealed class BudgetDirectory(
    LedgerDbContext db,
    ITenantContext tenant,
    IHouseholdDirectory households,
    TimeProvider clock) : IBudgetDirectory
{
    public async Task<MonthBudget> GetMonthAsync(
        DateOnly? month = null,
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();

        var household = await households.GetCurrentAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The request resolved to a household that no longer exists.");

        var reference = month ?? HouseholdClock.TodayIn(household.TimeZoneId, clock);
        var start = new DateOnly(reference.Year, reference.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var lines = await db.BudgetLines
            .Where(b => b.HouseholdId == householdId && b.PeriodStart == start)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // A month with nothing declared is not an error: the listing comes back empty and the
        // total is zero, and the screen says so in words rather than showing a broken table.
        if (lines.Count == 0)
        {
            return new MonthBudget(start, end, household.BaseCurrency, 0m, [], 0m, []);
        }

        var categoryIds = lines.Select(b => b.CategoryId).ToHashSet();
        var accountIds = lines.Select(b => b.AccountId).ToHashSet();

        var categories = await db.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken)
            .ConfigureAwait(false);

        var accounts = await db.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken)
            .ConfigureAwait(false);

        var summaries = lines
            .Select(line => ToSummary(line, categories[line.CategoryId], accounts[line.AccountId]))
            .OrderBy(line => line.CategoryName, StringComparer.CurrentCulture)
            .ToList();

        // Two readings, never one net figure: what the month commits and what it expects to
        // receive answer different questions, and a household that nets them cannot tell a month
        // that spends nothing apart from a month that earns as much as it spends.
        var committed = summaries
            .Where(line => line.Sign == BudgetLineSigns.Expense)
            .Sum(line => line.Amount);

        var income = summaries
            .Where(line => line.Sign == BudgetLineSigns.Income)
            .ToList();

        return new MonthBudget(
            start,
            end,
            household.BaseCurrency,
            Round(committed),
            summaries,
            Round(income.Sum(line => line.Amount)),
            ByOrigin(income));
    }

    /// <summary>Adds the month's income lines up by where the money comes from.</summary>
    /// <param name="income">The month's income lines, as published.</param>
    /// <returns>
    /// One entry per origin the month declares, in the order of the closed list. An origin nobody
    /// declared is left out rather than published at zero: a row of zeros says nothing.
    /// </returns>
    private static List<BudgetIncomeOriginTotal> ByOrigin(
        IReadOnlyList<BudgetLineSummary> income) =>
        BudgetIncomeOrigins.All
            .Select(origin => new BudgetIncomeOriginTotal(
                origin,
                Round(income.Where(line => line.Origin == origin).Sum(line => line.Amount))))
            .Where(total => income.Any(line => line.Origin == total.Origin))
            .ToList();

    /// <summary>Publishes one line, with the names the screen shows it by.</summary>
    /// <param name="line">The stored line.</param>
    /// <param name="category">Its category.</param>
    /// <param name="account">Its account.</param>
    /// <returns>The line as the contract publishes it.</returns>
    internal static BudgetLineSummary ToSummary(
        BudgetLine line,
        Category category,
        Account account)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(account);

        return new BudgetLineSummary(
            line.PublicId,
            line.Sign.ToContractName(),
            category.PublicId,
            category.Name,
            category.ColorIndex,
            account.PublicId,
            account.Name,
            line.PeriodStart,
            line.PeriodEnd,
            Round(line.Amount),
            line.Currency.Value,
            line.AmountMode.ToContractName(),
            line.DailyAmount(),
            line.Origin?.ToContractName(),
            Round(line.SignedAmount()));
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.ToEven);
}
