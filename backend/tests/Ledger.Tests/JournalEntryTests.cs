using System.Reflection;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;

namespace MyHome.Ledger.Tests;

public sealed class JournalEntryTests
{
    private const int HouseholdId = 1;

    private static int _nextKey;

    [Fact(DisplayName = "An expense produces two postings that cancel out")]
    public void expense_produces_two_postings_that_cancel_out()
    {
        var entry = RegisterGroceries(42.35m);

        Assert.Equal(2, entry.Postings.Count);
        Assert.Equal(0m, entry.Postings.Sum(p => p.Amount));
    }

    [Fact(DisplayName = "The money leaves the paying account and lands on the expense account")]
    public void money_leaves_the_paying_account_and_lands_on_the_expense_account()
    {
        var bank = Bank();
        var expenses = Expenses();

        var entry = JournalEntry.RegisterExpense(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Weekly shop",
            bank,
            expenses,
            Groceries(),
            Money.Of(42.35m, CurrencyCode.Euro));

        Assert.Equal(-42.35m, entry.Postings.Single(p => p.AccountId == bank.Id).Amount);
        Assert.Equal(42.35m, entry.Postings.Single(p => p.AccountId == expenses.Id).Amount);
    }

    [Fact(DisplayName = "Only the nominal side carries the category")]
    public void only_the_nominal_side_carries_the_category()
    {
        var category = Groceries();
        var entry = RegisterGroceries(42.35m, category);

        var classified = entry.Postings.Single(p => p.CategoryId is not null);

        Assert.Equal(category.Id, classified.CategoryId);
        Assert.True(classified.Amount > 0m);
    }

    [Theory(DisplayName = "The amount of an expense has to be positive")]
    [InlineData(0)]
    [InlineData(-10)]
    public void the_amount_of_an_expense_has_to_be_positive(decimal amount)
    {
        var thrown = Assert.Throws<ArgumentException>(() => RegisterGroceries(amount));

        Assert.Contains("positive", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "An expense cannot be paid out of a nominal account")]
    public void an_expense_cannot_be_paid_out_of_a_nominal_account()
    {
        Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterExpense(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Weekly shop",
            paidFrom: Expenses(),
            expenseAccount: Expenses(),
            category: Groceries(),
            amount: Money.Of(10m, CurrencyCode.Euro)));
    }

    [Fact(DisplayName = "An income category cannot classify an expense")]
    public void an_income_category_cannot_classify_an_expense()
    {
        var salary = Category.Create(HouseholdId, "Salary", CategoryKind.Income, 2);

        Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterExpense(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Weekly shop",
            Bank(),
            Expenses(),
            salary,
            Money.Of(10m, CurrencyCode.Euro)));
    }

    [Fact(DisplayName = "An account from another household cannot be used")]
    public void an_account_from_another_household_cannot_be_used()
    {
        var foreignBank = Account.Create(
            HouseholdId + 1,
            "Someone else's account",
            AccountType.Checking,
            CurrencyCode.Euro);

        Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterExpense(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Weekly shop",
            foreignBank,
            Expenses(),
            Groceries(),
            Money.Of(10m, CurrencyCode.Euro)));
    }

    [Fact(DisplayName = "Spending in a currency the account does not work in is refused")]
    public void spending_in_a_currency_the_account_does_not_work_in_is_refused()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterExpense(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Weekly shop",
            Bank(),
            Expenses(),
            Groceries(),
            Money.Of(10m, CurrencyCode.UsDollar)));

        Assert.Contains("exchange rate", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "A nominal account is never tracked, whatever the caller asks for")]
    public void a_nominal_account_is_never_tracked()
    {
        var expenses = Account.Create(
            HouseholdId,
            "Expenses",
            AccountType.Expense,
            CurrencyCode.Euro,
            isTracked: true);

        Assert.False(expenses.IsTracked);
        Assert.False(expenses.IsReal);
    }

    private static JournalEntry RegisterGroceries(decimal amount, Category? category = null) =>
        JournalEntry.RegisterExpense(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Weekly shop",
            Bank(),
            Expenses(),
            category ?? Groceries(),
            Money.Of(amount, CurrencyCode.Euro));

    private static Account Bank() =>
        WithKey(Account.Create(
            HouseholdId, "Joint account", AccountType.Checking, CurrencyCode.Euro));

    private static Account Expenses() =>
        WithKey(Account.Create(HouseholdId, "Expenses", AccountType.Expense, CurrencyCode.Euro));

    private static Category Groceries() =>
        WithKey(Category.Create(HouseholdId, "Groceries", CategoryKind.Expense, 2));

    private static T WithKey<T>(T entity)
    {
        typeof(T).GetProperty(nameof(Account.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetMethod!
            .Invoke(entity, [++_nextKey]);

        return entity;
    }
}
