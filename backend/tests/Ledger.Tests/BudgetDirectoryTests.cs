using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Budget;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Reading the budget of a month, as stories 051 and 052 specify it. The maps from RF to test live
/// in <c>progress/impl_051.md</c> and <c>progress/impl_052.md</c>.
/// </summary>
public sealed class BudgetDirectoryTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    private static readonly TimeProvider Clock =
        new FixedTimeProvider(new DateTimeOffset(Today, new TimeOnly(10, 0), TimeSpan.Zero));

    private static readonly DateOnly ThisMonth = new(2026, 9, 1);

    private readonly LedgerDatabase _database = new();

    private Account _account = null!;

    public void Dispose() => _database.Dispose();

    // RF-13, RF-4
    [Fact(DisplayName = "The month lists each line with its category, amount, mode and account")]
    public async Task the_month_lists_each_line_with_its_category_amount_mode_and_account()
    {
        await SeedAsync(
            ("Supermercado", 300m, PlannedAmountMode.Estimated),
            ("Alquiler", 850m, PlannedAmountMode.Fixed));

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        Assert.Equal(2, budget.Lines.Count);

        var rent = budget.Lines.Single(line => line.CategoryName == "Alquiler");

        Assert.Equal(850m, rent.Amount);
        Assert.Equal(BudgetAmountModes.Fixed, rent.AmountMode);
        Assert.Equal("Cuenta conjunta", rent.AccountName);
        Assert.Equal("EUR", rent.Currency);
        Assert.Equal("expense", rent.Sign);
        Assert.Equal(_account.PublicId, rent.AccountId);

        var groceries = budget.Lines.Single(line => line.CategoryName == "Supermercado");

        Assert.Equal(BudgetAmountModes.Estimated, groceries.AmountMode);
    }

    // RF-14
    [Fact(DisplayName = "The committed total is the sum of the month's lines")]
    public async Task the_committed_total_is_the_sum_of_the_months_lines()
    {
        await SeedAsync(
            ("Supermercado", 300m, PlannedAmountMode.Estimated),
            ("Alquiler", 850m, PlannedAmountMode.Fixed),
            ("Ocio", 120.50m, PlannedAmountMode.Estimated));

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        Assert.Equal(1270.50m, budget.CommittedTotal);
    }

    // RF-14, edge case: a month with nothing declared is not an error
    [Fact(DisplayName = "A month with nothing declared comes back empty and at zero")]
    public async Task a_month_with_nothing_declared_comes_back_empty_and_at_zero()
    {
        await SeedAsync();

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        Assert.Empty(budget.Lines);
        Assert.Equal(0m, budget.CommittedTotal);
        Assert.Equal(ThisMonth, budget.PeriodStart);
        Assert.Equal(new DateOnly(2026, 9, 30), budget.PeriodEnd);
    }

    // RF-13, edge case: months are independent of one another
    [Fact(DisplayName = "A month only lists its own lines")]
    public async Task a_month_only_lists_its_own_lines()
    {
        await SeedAsync(("Supermercado", 300m, PlannedAmountMode.Estimated));

        var nextMonth = new DateOnly(2026, 10, 1);
        var category = await _database.Context.Categories.SingleAsync();

        _database.Context.BudgetLines.Add(BudgetLine.Declare(
            HouseholdId, BudgetLineSign.Expense, category, _account, nextMonth, 999m));

        await _database.Context.SaveChangesAsync();

        var september = await NewDirectory().GetMonthAsync(ThisMonth);
        var october = await NewDirectory().GetMonthAsync(nextMonth);

        Assert.Equal(300m, Assert.Single(september.Lines).Amount);
        Assert.Equal(999m, Assert.Single(october.Lines).Amount);
    }

    // RF-13: no household reads another household's budget
    [Fact(DisplayName = "A household does not see another household's lines")]
    public async Task a_household_does_not_see_another_households_lines()
    {
        await SeedAsync(("Supermercado", 300m, PlannedAmountMode.Estimated));

        var foreignAccount = Account.Create(
            HouseholdId + 1, "Their bank", AccountType.Checking, CurrencyCode.Euro);
        var foreignCategory = Category.Create(
            HouseholdId + 1, "Their groceries", CategoryKind.Expense, 2);

        _database.Context.Accounts.Add(foreignAccount);
        _database.Context.Categories.Add(foreignCategory);
        await _database.Context.SaveChangesAsync();

        _database.Context.BudgetLines.Add(BudgetLine.Declare(
            HouseholdId + 1,
            BudgetLineSign.Expense,
            foreignCategory,
            foreignAccount,
            ThisMonth,
            999m));

        await _database.Context.SaveChangesAsync();

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        Assert.Equal(300m, Assert.Single(budget.Lines).Amount);
        Assert.Equal(300m, budget.CommittedTotal);
    }

    // RF-20: what the month commits does not move as the month runs
    [Fact(DisplayName = "The published daily share does not depend on the day it is read")]
    public async Task the_published_daily_share_does_not_depend_on_the_day_it_is_read()
    {
        await SeedAsync(("Supermercado", 100m, PlannedAmountMode.Estimated));

        var onThe20th = await NewDirectory().GetMonthAsync(ThisMonth);

        var onTheLastDay = await new BudgetDirectory(
                _database.Context,
                new TestTenantContext(HouseholdId),
                new TestHouseholdDirectory(CurrencyCode.Euro),
                new FixedTimeProvider(
                    new DateTimeOffset(new DateOnly(2026, 9, 30), new TimeOnly(23, 0), TimeSpan.Zero)))
            .GetMonthAsync(ThisMonth);

        Assert.Equal(3.33m, Assert.Single(onThe20th.Lines).DailyAmount);
        Assert.Equal(3.33m, Assert.Single(onTheLastDay.Lines).DailyAmount);
    }

    // RF-13: with no month asked for, the month in progress is the one shown
    [Fact(DisplayName = "With no month asked for, the month in progress is the one read")]
    public async Task with_no_month_asked_for_the_month_in_progress_is_the_one_read()
    {
        await SeedAsync(("Supermercado", 300m, PlannedAmountMode.Estimated));

        var budget = await NewDirectory().GetMonthAsync();

        Assert.Equal(ThisMonth, budget.PeriodStart);
        Assert.Single(budget.Lines);
    }

    // ---------------------------------------------------------------------------------------
    // Story 052 — the income sign. The map from RF to test lives in progress/impl_052.md.
    // ---------------------------------------------------------------------------------------

    // 052 RF-13, RF-21: every line publishes its sign, and an income one publishes its origin
    [Fact(DisplayName = "The month publishes the sign of every line and the origin of the income")]
    public async Task the_month_publishes_the_sign_of_every_line_and_the_origin_of_the_income()
    {
        await SeedAsync(("Supermercado", 300m, PlannedAmountMode.Estimated));
        await SeedIncomeAsync(("Nómina", 2530m, BudgetIncomeOrigin.Payroll));

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        var groceries = budget.Lines.Single(line => line.CategoryName == "Supermercado");
        var salary = budget.Lines.Single(line => line.CategoryName == "Nómina");

        Assert.Equal(BudgetLineSigns.Expense, groceries.Sign);
        Assert.Null(groceries.Origin);
        Assert.Equal(-300m, groceries.SignedAmount);

        Assert.Equal(BudgetLineSigns.Income, salary.Sign);
        Assert.Equal(BudgetIncomeOrigins.Payroll, salary.Origin);
        Assert.Equal(2530m, salary.SignedAmount);
    }

    // 052 RF-13: the two totals are published apart, never netted into one figure
    [Fact(DisplayName = "Committed spending and expected income are two separate totals")]
    public async Task committed_spending_and_expected_income_are_two_separate_totals()
    {
        await SeedAsync(
            ("Supermercado", 300m, PlannedAmountMode.Estimated),
            ("Alquiler", 850m, PlannedAmountMode.Fixed));

        await SeedIncomeAsync(
            ("Nómina", 2530m, BudgetIncomeOrigin.Payroll),
            ("Facturación", 800m, BudgetIncomeOrigin.SelfEmployment));

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        Assert.Equal(1150m, budget.CommittedTotal);
        Assert.Equal(3330m, budget.ExpectedIncomeTotal);
        Assert.Equal(4, budget.Lines.Count);
    }

    // 052 RF-13, edge case: a month with income and no spending at all
    [Fact(DisplayName = "A month with income and no spending commits zero and expects more")]
    public async Task a_month_with_income_and_no_spending_commits_zero_and_expects_more()
    {
        await SeedAsync();
        await SeedIncomeAsync(("Nómina", 2530m, BudgetIncomeOrigin.Payroll));

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        Assert.Equal(0m, budget.CommittedTotal);
        Assert.Equal(2530m, budget.ExpectedIncomeTotal);
    }

    // 052 RF-14: a month with only spending is perfectly valid and expects nothing
    [Fact(DisplayName = "A month with no income line declared expects zero, which is not an error")]
    public async Task a_month_with_no_income_line_declared_expects_zero_which_is_not_an_error()
    {
        await SeedAsync(("Supermercado", 300m, PlannedAmountMode.Estimated));

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        Assert.Equal(300m, budget.CommittedTotal);
        Assert.Equal(0m, budget.ExpectedIncomeTotal);
        Assert.Empty(budget.IncomeByOrigin);
    }

    // 052 RF-22
    [Fact(DisplayName = "Expected income is broken down by where the money comes from")]
    public async Task expected_income_is_broken_down_by_where_the_money_comes_from()
    {
        await SeedAsync(("Supermercado", 300m, PlannedAmountMode.Estimated));

        await SeedIncomeAsync(
            ("Nómina", 2530m, BudgetIncomeOrigin.Payroll),
            ("Pagas Extra", 1800m, BudgetIncomeOrigin.Payroll),
            ("Facturación", 800m, BudgetIncomeOrigin.SelfEmployment),
            ("Alquiler piso", 650m, BudgetIncomeOrigin.Rental));

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        // In the order of the closed list, and only the origins the month actually declares.
        Assert.Equal(
            [
                (BudgetIncomeOrigins.Payroll, 4330m),
                (BudgetIncomeOrigins.SelfEmployment, 800m),
                (BudgetIncomeOrigins.Rental, 650m),
            ],
            budget.IncomeByOrigin.Select(total => (total.Origin, total.Amount)));

        // The breakdown adds up to the total it breaks down, and spending stays out of it.
        Assert.Equal(
            budget.ExpectedIncomeTotal,
            budget.IncomeByOrigin.Sum(total => total.Amount));
    }

    // 052 RF-22, edge case: nothing declared at all leaves no breakdown to publish
    [Fact(DisplayName = "A month with nothing declared has no income breakdown")]
    public async Task a_month_with_nothing_declared_has_no_income_breakdown()
    {
        await SeedAsync();

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        Assert.Equal(0m, budget.ExpectedIncomeTotal);
        Assert.Empty(budget.IncomeByOrigin);
    }

    // 052 RF-7, RF-16: income spreads like spending, and the month publishes that share
    [Fact(DisplayName = "The published income line carries the same daily share as spending would")]
    public async Task the_published_income_line_carries_the_same_daily_share_as_spending_would()
    {
        await SeedAsync(("Supermercado", 100m, PlannedAmountMode.Estimated));
        await SeedIncomeAsync(("Facturación", 100m, BudgetIncomeOrigin.SelfEmployment));

        var budget = await NewDirectory().GetMonthAsync(ThisMonth);

        Assert.Equal(
            budget.Lines.Single(line => line.CategoryName == "Supermercado").DailyAmount,
            budget.Lines.Single(line => line.CategoryName == "Facturación").DailyAmount);

        Assert.Equal(3.33m, budget.Lines.Single(line => line.CategoryName == "Facturación").DailyAmount);
    }

    private BudgetDirectory NewDirectory() => new(
        _database.Context,
        new TestTenantContext(HouseholdId),
        new TestHouseholdDirectory(CurrencyCode.Euro),
        Clock);

    private async Task SeedAsync(
        params (string Category, decimal Amount, PlannedAmountMode Mode)[] lines)
    {
        _account = Account.Create(
            HouseholdId, "Cuenta conjunta", AccountType.Checking, CurrencyCode.Euro);

        _database.Context.Accounts.Add(_account);
        await _database.Context.SaveChangesAsync();

        foreach (var (name, amount, mode) in lines)
        {
            var category = Category.Create(HouseholdId, name, CategoryKind.Expense, 2);
            _database.Context.Categories.Add(category);
            await _database.Context.SaveChangesAsync();

            _database.Context.BudgetLines.Add(BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Expense,
                category,
                _account,
                ThisMonth,
                amount,
                mode));
        }

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Declares income lines of this month against the account already seeded.</summary>
    /// <param name="lines">Category name, amount and origin of each line.</param>
    /// <returns>A task that completes once the lines are stored.</returns>
    private async Task SeedIncomeAsync(
        params (string Category, decimal Amount, BudgetIncomeOrigin Origin)[] lines)
    {
        foreach (var (name, amount, origin) in lines)
        {
            var category = Category.Create(HouseholdId, name, CategoryKind.Income, 4);
            _database.Context.Categories.Add(category);
            await _database.Context.SaveChangesAsync();

            _database.Context.BudgetLines.Add(BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Income,
                category,
                _account,
                ThisMonth,
                amount,
                PlannedAmountMode.Estimated,
                origin));
        }

        await _database.Context.SaveChangesAsync();
    }
}
