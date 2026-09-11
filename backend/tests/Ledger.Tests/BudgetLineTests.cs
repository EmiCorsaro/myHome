using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// The shape of a budget line and how a line without a calendar spreads its amount, as story 051
/// specifies it. The map from RF to test lives in <c>progress/impl_051.md</c>.
/// </summary>
public sealed class BudgetLineTests
{
    // RF-1, RF-5
    [Fact(DisplayName = "A line covers the whole natural month, starting on day 1")]
    public void a_line_covers_the_whole_natural_month_starting_on_day_1()
    {
        var line = Groceries(new DateOnly(2026, 9, 17));

        Assert.Equal(HouseholdId, line.HouseholdId);
        Assert.Equal(new DateOnly(2026, 9, 1), line.PeriodStart);
        Assert.Equal(new DateOnly(2026, 9, 30), line.PeriodEnd);
        Assert.Equal(30, line.DaysInPeriod);
    }

    // RF-1
    [Fact(DisplayName = "A line records the category and the account it was declared against")]
    public void a_line_records_the_category_and_the_account_it_was_declared_against()
    {
        var category = Phone("Supermercado");
        var account = Bank();

        var line = BudgetLine.Declare(
            HouseholdId,
            BudgetLineSign.Expense,
            category,
            account,
            new DateOnly(2026, 9, 1),
            300m);

        Assert.Equal(category.Id, line.CategoryId);
        Assert.Equal(account.Id, line.AccountId);
        Assert.Equal(BudgetLineSign.Expense, line.Sign);
    }

    // RF-3
    [Theory(DisplayName = "A line's amount is either fixed or estimated")]
    [InlineData(PlannedAmountMode.Fixed)]
    [InlineData(PlannedAmountMode.Estimated)]
    public void a_lines_amount_is_either_fixed_or_estimated(PlannedAmountMode mode)
    {
        Assert.Equal(mode, Groceries(mode: mode).AmountMode);
    }

    // RF-7
    [Theory(DisplayName = "A line declares a positive amount")]
    [InlineData(0)]
    [InlineData(-50)]
    public void a_line_declares_a_positive_amount(decimal amount)
    {
        Assert.Throws<ArgumentException>(() => Groceries(amount: amount));
    }

    // RF-7, edge case: an amount with decimals is fine
    [Fact(DisplayName = "An amount with decimals is accepted")]
    public void an_amount_with_decimals_is_accepted()
    {
        Assert.Equal(312.50m, Groceries(amount: 312.50m).Amount);
    }

    // RF-8
    [Fact(DisplayName = "A household cannot budget another household's category")]
    public void a_household_cannot_budget_another_households_category()
    {
        var foreign = WithKey(
            Category.Create(HouseholdId + 1, "Their groceries", CategoryKind.Expense, 2));

        Assert.Throws<InvalidOperationException>(
            () => BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Expense,
                foreign,
                Bank(),
                new DateOnly(2026, 9, 1),
                300m));
    }

    // RF-8
    [Fact(DisplayName = "An archived category takes no new budget line")]
    public void an_archived_category_takes_no_new_budget_line()
    {
        var category = Phone("Supermercado");
        category.Archive();

        Assert.Throws<InvalidOperationException>(
            () => BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Expense,
                category,
                Bank(),
                new DateOnly(2026, 9, 1),
                300m));
    }

    // RF-9
    [Fact(DisplayName = "A household cannot budget against another household's account")]
    public void a_household_cannot_budget_against_another_households_account()
    {
        var foreign = WithKey(Account.Create(
            HouseholdId + 1, "Their bank", AccountType.Checking, CurrencyCode.Euro));

        Assert.Throws<InvalidOperationException>(
            () => BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Expense,
                Phone("Supermercado"),
                foreign,
                new DateOnly(2026, 9, 1),
                300m));
    }

    // RF-9
    [Fact(DisplayName = "An archived account takes no new budget line")]
    public void an_archived_account_takes_no_new_budget_line()
    {
        var account = Bank();
        account.Archive();

        Assert.Throws<InvalidOperationException>(
            () => BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Expense,
                Phone("Supermercado"),
                account,
                new DateOnly(2026, 9, 1),
                300m));
    }

    // RF-10
    [Fact(DisplayName = "An expense line cannot be declared against an income category")]
    public void an_expense_line_cannot_be_declared_against_an_income_category()
    {
        Assert.Throws<InvalidOperationException>(
            () => BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Expense,
                Salary(),
                Bank(),
                new DateOnly(2026, 9, 1),
                300m));
    }

    // RF-10
    [Fact(DisplayName = "An income line cannot be declared against an expense category")]
    public void an_income_line_cannot_be_declared_against_an_expense_category()
    {
        Assert.Throws<InvalidOperationException>(
            () => BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Income,
                Phone("Supermercado"),
                Bank(),
                new DateOnly(2026, 9, 1),
                2530m));
    }

    // RF-15
    [Fact(DisplayName = "A line takes the currency of its account")]
    public void a_line_takes_the_currency_of_its_account()
    {
        var account = WithKey(Account.Create(
            HouseholdId, "Dollar account", AccountType.Checking, CurrencyCode.UsDollar));

        var line = BudgetLine.Declare(
            HouseholdId,
            BudgetLineSign.Expense,
            Phone("Supermercado"),
            account,
            new DateOnly(2026, 9, 1),
            300m);

        Assert.Equal(CurrencyCode.UsDollar, line.Currency);
        Assert.Equal(CurrencyCode.UsDollar, line.Allowance().Currency);
    }

    // RF-11
    [Fact(DisplayName = "An amount that divides exactly gives every day the same share")]
    public void an_amount_that_divides_exactly_gives_every_day_the_same_share()
    {
        var line = Groceries(new DateOnly(2026, 9, 1), amount: 300m);

        Assert.All(line.Spread(), day => Assert.Equal(10m, day.Amount));
        Assert.Equal(300m, line.Spread().Sum(day => day.Amount));
    }

    // RF-11, RF-18
    [Fact(DisplayName = "The spread starts on day 1 however late the line is declared")]
    public void the_spread_starts_on_day_1_however_late_the_line_is_declared()
    {
        // Declared on the 20th, a fortnight into the month it covers.
        var line = Groceries(
            new DateOnly(2026, 9, 20),
            createdAt: new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero));

        var spread = line.Spread();

        Assert.Equal(new DateOnly(2026, 9, 1), spread[0].Day);
        Assert.Equal(10m, spread[0].Amount);
        Assert.Equal(10m, line.AmountForDay(new DateOnly(2026, 9, 1)));
    }

    // RF-19
    [Fact(DisplayName = "The leftover of an amount that does not divide lands on the last day")]
    public void the_leftover_of_an_amount_that_does_not_divide_lands_on_the_last_day()
    {
        // 100 € across 30 days: 3,33 € a day and 0,10 € left over.
        var line = Groceries(new DateOnly(2026, 9, 1), amount: 100m);

        var spread = line.Spread();

        Assert.All(spread.Take(29), day => Assert.Equal(3.33m, day.Amount));
        Assert.Equal(3.43m, spread[^1].Amount);
        Assert.Equal(new DateOnly(2026, 9, 30), spread[^1].Day);
        Assert.Equal(100m, spread.Sum(day => day.Amount));
    }

    // RF-20
    [Fact(DisplayName = "A day carries the same share however often it is asked about")]
    public void a_day_carries_the_same_share_however_often_it_is_asked_about()
    {
        var line = Groceries(new DateOnly(2026, 9, 1), amount: 100m);

        foreach (var day in line.Spread())
        {
            Assert.Equal(day.Amount, line.AmountForDay(day.Day));
            Assert.Equal(day.Amount, line.AmountForDay(day.Day));
        }
    }

    // Edge case: 28, 29, 30 and 31-day months
    [Theory(DisplayName = "The month's amount holds whatever the length of the month")]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 9, 30)]
    [InlineData(2026, 10, 31)]
    public void the_months_amount_holds_whatever_the_length_of_the_month(
        int year,
        int month,
        int days)
    {
        var line = Groceries(new DateOnly(year, month, 1), amount: 300m);

        var spread = line.Spread();

        Assert.Equal(days, spread.Count);
        Assert.Equal(300m, spread.Sum(day => day.Amount));
    }

    [Fact(DisplayName = "A day outside the month is not part of the spread")]
    public void a_day_outside_the_month_is_not_part_of_the_spread()
    {
        var line = Groceries(new DateOnly(2026, 9, 1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => line.AmountForDay(new DateOnly(2026, 10, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => line.AmountForDay(new DateOnly(2026, 8, 31)));
    }

    // ---------------------------------------------------------------------------------------
    // Story 052 — the income sign. The map from RF to test lives in progress/impl_052.md.
    // ---------------------------------------------------------------------------------------

    // 052 RF-1
    [Theory(DisplayName = "A budget line declares one of two signs")]
    [InlineData(BudgetLineSign.Expense)]
    [InlineData(BudgetLineSign.Income)]
    public void a_budget_line_declares_one_of_two_signs(BudgetLineSign sign)
    {
        Assert.Equal(sign, Line(sign).Sign);
    }

    // 052 RF-1: a sign is exactly one of the two, so nothing else can reach the model
    [Fact(DisplayName = "Expense and income are the only signs there are")]
    public void expense_and_income_are_the_only_signs_there_are()
    {
        Assert.Equal(
            [BudgetLineSign.Expense, BudgetLineSign.Income],
            Enum.GetValues<BudgetLineSign>());
    }

    // 052 RF-2
    [Fact(DisplayName = "An income line records its household, month, category and account")]
    public void an_income_line_records_its_household_month_category_and_account()
    {
        var category = Salary();
        var account = Bank();

        var line = BudgetLine.Declare(
            HouseholdId,
            BudgetLineSign.Income,
            category,
            account,
            new DateOnly(2026, 9, 25),
            2530m,
            origin: BudgetIncomeOrigin.Payroll);

        Assert.Equal(HouseholdId, line.HouseholdId);
        Assert.Equal(new DateOnly(2026, 9, 1), line.PeriodStart);
        Assert.Equal(new DateOnly(2026, 9, 30), line.PeriodEnd);
        Assert.Equal(category.Id, line.CategoryId);
        Assert.Equal(account.Id, line.AccountId);
        Assert.Equal(2530m, line.Amount);
    }

    // 052 RF-3, RF-4
    [Theory(DisplayName = "An income line declares a positive amount, just like an expense one")]
    [InlineData(0)]
    [InlineData(-2530)]
    public void an_income_line_declares_a_positive_amount(decimal amount)
    {
        Assert.Throws<ArgumentException>(() => Line(BudgetLineSign.Income, amount: amount));
    }

    // 052 RF-10
    [Theory(DisplayName = "An income line's amount is either fixed or estimated")]
    [InlineData(PlannedAmountMode.Fixed)]
    [InlineData(PlannedAmountMode.Estimated)]
    public void an_income_lines_amount_is_either_fixed_or_estimated(PlannedAmountMode mode)
    {
        Assert.Equal(mode, Line(BudgetLineSign.Income, mode: mode).AmountMode);
    }

    // 052 RF-7, RF-16: the same spread, with no prudence correction of any kind
    [Fact(DisplayName = "An income line spreads exactly like an expense line of the same amount")]
    public void an_income_line_spreads_exactly_like_an_expense_line_of_the_same_amount()
    {
        var income = Line(BudgetLineSign.Income, amount: 300m);
        var expense = Line(BudgetLineSign.Expense, amount: 300m);

        Assert.Equal(
            expense.Spread().Select(day => (day.Day, day.Amount)),
            income.Spread().Select(day => (day.Day, day.Amount)));

        Assert.All(income.Spread(), day => Assert.Equal(10m, day.Amount));
        Assert.Equal(300m, income.Spread().Sum(day => day.Amount));
    }

    // 052 RF-7, edge case: declared on the 20th, it still starts on day 1
    [Fact(DisplayName = "An income line spreads from day 1 however late it is declared")]
    public void an_income_line_spreads_from_day_1_however_late_it_is_declared()
    {
        var line = Line(
            BudgetLineSign.Income,
            month: new DateOnly(2026, 9, 20),
            amount: 300m,
            createdAt: new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero));

        var spread = line.Spread();

        Assert.Equal(new DateOnly(2026, 9, 1), spread[0].Day);
        Assert.Equal(10m, spread[0].Amount);
    }

    // 052 RF-15
    [Fact(DisplayName = "The leftover of an income that does not divide lands on the last day")]
    public void the_leftover_of_an_income_that_does_not_divide_lands_on_the_last_day()
    {
        // 100 € across 30 days: 3,33 € a day and 0,10 € left over.
        var spread = Line(BudgetLineSign.Income, amount: 100m).Spread();

        Assert.All(spread.Take(29), day => Assert.Equal(3.33m, day.Amount));
        Assert.Equal(3.43m, spread[^1].Amount);
        Assert.Equal(new DateOnly(2026, 9, 30), spread[^1].Day);
        Assert.Equal(100m, spread.Sum(day => day.Amount));
    }

    // 052 RF-9: income adds, expense subtracts, and the line says which without being asked twice
    [Fact(DisplayName = "An income line weighs positive on a balance and an expense one negative")]
    public void an_income_line_weighs_positive_on_a_balance_and_an_expense_one_negative()
    {
        Assert.Equal(300m, Line(BudgetLineSign.Income, amount: 300m).SignedAmount());
        Assert.Equal(-300m, Line(BudgetLineSign.Expense, amount: 300m).SignedAmount());

        // The declared amount itself stays positive either way: the direction is the sign's job.
        Assert.Equal(300m, Line(BudgetLineSign.Income, amount: 300m).Amount);
        Assert.Equal(300m, Line(BudgetLineSign.Expense, amount: 300m).Amount);
    }

    // 052 RF-9: day by day, so whatever draws the curve adds a figure and decides nothing
    [Fact(DisplayName = "A month of income and a month of expense cancel out day by day")]
    public void a_month_of_income_and_a_month_of_expense_cancel_out_day_by_day()
    {
        var income = Line(BudgetLineSign.Income, amount: 100m);
        var expense = Line(BudgetLineSign.Expense, amount: 100m);

        foreach (var day in income.Spread())
        {
            Assert.Equal(day.Amount, income.SignedAmountForDay(day.Day));
            Assert.Equal(-day.Amount, expense.SignedAmountForDay(day.Day));
            Assert.Equal(0m, income.SignedAmountForDay(day.Day) + expense.SignedAmountForDay(day.Day));
        }

        Assert.Equal(100m, income.Spread().Sum(day => income.SignedAmountForDay(day.Day)));
        Assert.Equal(-100m, expense.Spread().Sum(day => expense.SignedAmountForDay(day.Day)));
    }

    // 052 RF-17
    [Theory(DisplayName = "An income line takes any origin of the closed list")]
    [InlineData(BudgetIncomeOrigin.Payroll)]
    [InlineData(BudgetIncomeOrigin.SelfEmployment)]
    [InlineData(BudgetIncomeOrigin.Rental)]
    [InlineData(BudgetIncomeOrigin.Investment)]
    [InlineData(BudgetIncomeOrigin.Benefit)]
    [InlineData(BudgetIncomeOrigin.Refund)]
    [InlineData(BudgetIncomeOrigin.Gift)]
    [InlineData(BudgetIncomeOrigin.Other)]
    public void an_income_line_takes_any_origin_of_the_closed_list(BudgetIncomeOrigin origin)
    {
        Assert.Equal(origin, Line(BudgetLineSign.Income, origin: origin).Origin);
    }

    // 052 RF-17: the list is closed, and these eight are the whole of it
    [Fact(DisplayName = "The list of income origins is exactly the eight the household may choose")]
    public void the_list_of_income_origins_is_exactly_the_eight_the_household_may_choose()
    {
        Assert.Equal(
            [
                BudgetIncomeOrigin.Payroll,
                BudgetIncomeOrigin.SelfEmployment,
                BudgetIncomeOrigin.Rental,
                BudgetIncomeOrigin.Investment,
                BudgetIncomeOrigin.Benefit,
                BudgetIncomeOrigin.Refund,
                BudgetIncomeOrigin.Gift,
                BudgetIncomeOrigin.Other,
            ],
            Enum.GetValues<BudgetIncomeOrigin>());
    }

    // 052 RF-18: the origin is its own attribute, not something read off the category
    [Fact(DisplayName = "The origin is an attribute of the line, independent of its category")]
    public void the_origin_is_an_attribute_of_the_line_independent_of_its_category()
    {
        var category = Salary();

        var payroll = BudgetLine.Declare(
            HouseholdId,
            BudgetLineSign.Income,
            category,
            Bank(),
            new DateOnly(2026, 9, 1),
            2530m,
            origin: BudgetIncomeOrigin.Payroll);

        var rental = BudgetLine.Declare(
            HouseholdId,
            BudgetLineSign.Income,
            category,
            Bank(),
            new DateOnly(2026, 10, 1),
            700m,
            origin: BudgetIncomeOrigin.Rental);

        // Same category, two origins: the category does not decide the origin and the origin does
        // not change the category.
        Assert.Equal(payroll.CategoryId, rental.CategoryId);
        Assert.Equal(BudgetIncomeOrigin.Payroll, payroll.Origin);
        Assert.Equal(BudgetIncomeOrigin.Rental, rental.Origin);
    }

    // 052 RF-19
    [Fact(DisplayName = "An income line with no origin is not a complete declaration")]
    public void an_income_line_with_no_origin_is_not_a_complete_declaration()
    {
        var error = Assert.Throws<ArgumentException>(
            () => BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Income,
                Salary(),
                Bank(),
                new DateOnly(2026, 9, 1),
                2530m));

        Assert.Equal("origin", error.ParamName);
    }

    // 052 RF-20
    [Fact(DisplayName = "An expense line takes no origin")]
    public void an_expense_line_takes_no_origin()
    {
        var error = Assert.Throws<ArgumentException>(
            () => BudgetLine.Declare(
                HouseholdId,
                BudgetLineSign.Expense,
                Phone("Supermercado"),
                Bank(),
                new DateOnly(2026, 9, 1),
                300m,
                origin: BudgetIncomeOrigin.Payroll));

        Assert.Equal("origin", error.ParamName);
        Assert.Null(Line(BudgetLineSign.Expense).Origin);
    }

    // 052 edge case: an income declared against a foreign currency account follows its account
    [Fact(DisplayName = "An income line takes the currency of its account")]
    public void an_income_line_takes_the_currency_of_its_account()
    {
        var account = WithKey(Account.Create(
            HouseholdId, "Dollar account", AccountType.Checking, CurrencyCode.UsDollar));

        var line = BudgetLine.Declare(
            HouseholdId,
            BudgetLineSign.Income,
            Salary(),
            account,
            new DateOnly(2026, 9, 1),
            2530m,
            origin: BudgetIncomeOrigin.Payroll);

        Assert.Equal(CurrencyCode.UsDollar, line.Currency);
    }

    /// <summary>A line of either sign, with the origin an income one needs.</summary>
    /// <param name="sign">Whether money is expected out or in.</param>
    /// <param name="month">Any day of the month covered.</param>
    /// <param name="amount">Amount declared for the month.</param>
    /// <param name="mode">Whether the amount is fixed or estimated.</param>
    /// <param name="origin">Origin for an income line; ignored for an expense one.</param>
    /// <param name="createdAt">Instant the line was declared.</param>
    /// <returns>The declared line.</returns>
    private static BudgetLine Line(
        BudgetLineSign sign,
        DateOnly? month = null,
        decimal amount = 300m,
        PlannedAmountMode mode = PlannedAmountMode.Estimated,
        BudgetIncomeOrigin origin = BudgetIncomeOrigin.Payroll,
        DateTimeOffset? createdAt = null) =>
        BudgetLine.Declare(
            HouseholdId,
            sign,
            sign == BudgetLineSign.Income ? Salary() : Phone("Supermercado"),
            Bank(),
            month ?? new DateOnly(2026, 9, 1),
            amount,
            mode,
            sign == BudgetLineSign.Income ? origin : null,
            createdAt);

    private static BudgetLine Groceries(
        DateOnly? month = null,
        decimal amount = 300m,
        PlannedAmountMode mode = PlannedAmountMode.Estimated,
        DateTimeOffset? createdAt = null) =>
        BudgetLine.Declare(
            HouseholdId,
            BudgetLineSign.Expense,
            Phone("Supermercado"),
            Bank(),
            month ?? new DateOnly(2026, 9, 1),
            amount,
            mode,
            createdAt: createdAt);
}
