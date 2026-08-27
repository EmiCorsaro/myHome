using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

public sealed class CategoryBudgetTests
{
    [Fact(DisplayName = "A budget is pinned to the first day of its month")]
    public void a_budget_is_pinned_to_the_first_day_of_its_month()
    {
        var budget = Groceries(new DateOnly(2026, 9, 17));

        Assert.Equal(new DateOnly(2026, 9, 1), budget.PeriodStart);
        Assert.Equal(new DateOnly(2026, 9, 30), budget.PeriodEnd);
    }

    [Fact(DisplayName = "A budget counts the whole category by default")]
    public void a_budget_counts_the_whole_category_by_default()
    {
        Assert.Equal(BudgetScope.Total, Groceries().Scope);
    }

    [Theory(DisplayName = "A budget is a positive allowance")]
    [InlineData(0)]
    [InlineData(-50)]
    public void a_budget_is_a_positive_allowance(decimal amount)
    {
        Assert.Throws<ArgumentException>(
            () => CategoryBudget.Create(
                HouseholdId, Phone("Supermercado"), new DateOnly(2026, 9, 1), Euros(amount)));
    }

    [Fact(DisplayName = "Income is not budgeted with a monthly ceiling")]
    public void income_is_not_budgeted_with_a_monthly_ceiling()
    {
        Assert.Throws<InvalidOperationException>(
            () => CategoryBudget.Create(
                HouseholdId, Salary(), new DateOnly(2026, 9, 1), Euros(300m)));
    }

    [Fact(DisplayName = "A household cannot budget another household's category")]
    public void a_household_cannot_budget_another_households_category()
    {
        var foreign = WithKey(
            Category.Create(HouseholdId + 1, "Their groceries", CategoryKind.Expense, 2));

        Assert.Throws<InvalidOperationException>(
            () => CategoryBudget.Create(
                HouseholdId, foreign, new DateOnly(2026, 9, 1), Euros(300m)));
    }

    [Fact(DisplayName = "Revising a budget changes the allowance for that month")]
    public void revising_a_budget_changes_the_allowance_for_that_month()
    {
        var budget = Groceries();

        budget.Revise(Euros(350m));

        Assert.Equal(350m, budget.Amount);
    }

    [Fact(DisplayName = "A budget cannot be revised into another currency")]
    public void a_budget_cannot_be_revised_into_another_currency()
    {
        Assert.Throws<InvalidOperationException>(
            () => Groceries().Revise(Money.Of(350m, CurrencyCode.UsDollar)));
    }

    private static CategoryBudget Groceries(DateOnly? month = null) => CategoryBudget.Create(
        HouseholdId,
        Phone("Supermercado"),
        month ?? new DateOnly(2026, 9, 1),
        Euros(300m));
}
