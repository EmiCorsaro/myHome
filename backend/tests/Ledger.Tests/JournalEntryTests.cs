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

    [Fact(DisplayName = "An income produces two postings that cancel out")]
    public void income_produces_two_postings_that_cancel_out()
    {
        var entry = RegisterSalary(1500m);

        Assert.Equal(2, entry.Postings.Count);
        Assert.Equal(0m, entry.Postings.Sum(p => p.Amount));
    }

    [Fact(DisplayName = "The money enters the deposit account and leaves the income account")]
    public void money_enters_the_deposit_account_and_leaves_the_income_account()
    {
        var bank = Bank();
        var incomes = Incomes();

        var entry = JournalEntry.RegisterIncome(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Payroll",
            bank,
            incomes,
            Salary(),
            Money.Of(1500m, CurrencyCode.Euro));

        Assert.Equal(1500m, entry.Postings.Single(p => p.AccountId == bank.Id).Amount);
        Assert.Equal(-1500m, entry.Postings.Single(p => p.AccountId == incomes.Id).Amount);
    }

    [Fact(DisplayName = "Only the nominal side of an income carries the category")]
    public void only_the_nominal_side_of_an_income_carries_the_category()
    {
        var category = Salary();
        var entry = RegisterSalary(1500m, category);

        var classified = entry.Postings.Single(p => p.CategoryId is not null);

        Assert.Equal(category.Id, classified.CategoryId);
        Assert.True(classified.Amount < 0m);
    }

    [Theory(DisplayName = "The amount of an income has to be positive")]
    [InlineData(0)]
    [InlineData(-10)]
    public void the_amount_of_an_income_has_to_be_positive(decimal amount)
    {
        var thrown = Assert.Throws<ArgumentException>(() => RegisterSalary(amount));

        Assert.Contains("positive", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "An income cannot be deposited into a nominal account")]
    public void an_income_cannot_be_deposited_into_a_nominal_account()
    {
        Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterIncome(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Payroll",
            depositTo: Incomes(),
            incomeAccount: Incomes(),
            category: Salary(),
            amount: Money.Of(10m, CurrencyCode.Euro)));
    }

    [Fact(DisplayName = "An income cannot be deposited into a credit card")]
    public void an_income_cannot_be_deposited_into_a_credit_card()
    {
        var creditCard = WithKey(
            Account.Create(HouseholdId, "Card", AccountType.CreditCard, CurrencyCode.Euro));

        var thrown = Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterIncome(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Refund",
            depositTo: creditCard,
            incomeAccount: Incomes(),
            category: Salary(),
            amount: Money.Of(10m, CurrencyCode.Euro)));

        Assert.Contains("correction", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "An expense category cannot classify an income")]
    public void an_expense_category_cannot_classify_an_income()
    {
        var groceries = Category.Create(HouseholdId, "Groceries", CategoryKind.Expense, 2);

        Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterIncome(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Payroll",
            Bank(),
            Incomes(),
            groceries,
            Money.Of(10m, CurrencyCode.Euro)));
    }

    [Fact(DisplayName = "An income in a currency the account does not work in is refused")]
    public void an_income_in_a_currency_the_account_does_not_work_in_is_refused()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterIncome(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Payroll",
            Bank(),
            Incomes(),
            Salary(),
            Money.Of(10m, CurrencyCode.UsDollar)));

        Assert.Contains("exchange rate", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static JournalEntry RegisterSalary(decimal amount, Category? category = null) =>
        JournalEntry.RegisterIncome(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Payroll",
            Bank(),
            Incomes(),
            category ?? Salary(),
            Money.Of(amount, CurrencyCode.Euro));

    // Story 011, RF-1: a transfer produces two postings that cancel out.
    [Fact(DisplayName = "A transfer produces two postings that cancel out")]
    public void a_transfer_produces_two_postings_that_cancel_out()
    {
        var entry = RegisterTransfer(200m);

        Assert.Equal(2, entry.Postings.Count);
        Assert.Equal(0m, entry.Postings.Sum(p => p.Amount));
    }

    // RF-1
    [Fact(DisplayName = "The money leaves the origin account and lands on the destination account")]
    public void the_money_leaves_the_origin_account_and_lands_on_the_destination_account()
    {
        var checking = Bank();
        var savings = Savings();

        var entry = JournalEntry.RegisterTransfer(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "To savings",
            checking,
            savings,
            Money.Of(200m, CurrencyCode.Euro));

        Assert.Equal(-200m, entry.Postings.Single(p => p.AccountId == checking.Id).Amount);
        Assert.Equal(200m, entry.Postings.Single(p => p.AccountId == savings.Id).Amount);
    }

    // RF-2, RF-3, RF-4: between two controlled accounts, no posting carries a category, whatever
    // the caller passes.
    [Fact(DisplayName = "A transfer between two controlled accounts carries no category at all")]
    public void a_transfer_between_two_controlled_accounts_carries_no_category_at_all()
    {
        var entry = JournalEntry.RegisterTransfer(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "To savings",
            Bank(),
            Savings(),
            Money.Of(200m, CurrencyCode.Euro),
            category: Groceries());

        Assert.All(entry.Postings, p => Assert.Null(p.CategoryId));
    }

    // RF-14, RF-16: the destination is not controlled, so its own posting carries the category.
    [Fact(DisplayName = "A transfer into an uncontrolled destination carries the category on its posting")]
    public void a_transfer_into_an_uncontrolled_destination_carries_the_category_on_its_posting()
    {
        var checking = Bank();
        var uncontrolled = Uncontrolled();
        var category = Groceries();

        var entry = JournalEntry.RegisterTransfer(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Out of the tracked money",
            checking,
            uncontrolled,
            Money.Of(200m, CurrencyCode.Euro),
            category: category);

        var classified = entry.Postings.Single(p => p.CategoryId is not null);

        Assert.Equal(uncontrolled.Id, classified.AccountId);
        Assert.Equal(category.Id, classified.CategoryId);
        Assert.Equal(200m, classified.Amount);
    }

    // RF-15
    [Fact(DisplayName = "A transfer into an uncontrolled destination requires a category")]
    public void a_transfer_into_an_uncontrolled_destination_requires_a_category()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterTransfer(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Out of the tracked money",
            Bank(),
            Uncontrolled(),
            Money.Of(200m, CurrencyCode.Euro)));

        Assert.Contains("category", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    // RF-17
    [Fact(DisplayName = "An income category cannot classify a transfer")]
    public void an_income_category_cannot_classify_a_transfer()
    {
        var salary = Category.Create(HouseholdId, "Salary", CategoryKind.Income, 2);

        Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterTransfer(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Out of the tracked money",
            Bank(),
            Uncontrolled(),
            Money.Of(200m, CurrencyCode.Euro),
            category: salary));
    }

    // RF-8
    [Fact(DisplayName = "A transfer needs two different accounts")]
    public void a_transfer_needs_two_different_accounts()
    {
        var account = Bank();

        Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterTransfer(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "To itself",
            account,
            account,
            Money.Of(200m, CurrencyCode.Euro)));
    }

    // RF-19
    [Fact(DisplayName = "A credit card cannot be the origin of a transfer")]
    public void a_credit_card_cannot_be_the_origin_of_a_transfer()
    {
        var creditCard = WithKey(
            Account.Create(HouseholdId, "Card", AccountType.CreditCard, CurrencyCode.Euro));

        var thrown = Assert.Throws<InvalidOperationException>(() => JournalEntry.RegisterTransfer(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Cash advance",
            creditCard,
            Bank(),
            Money.Of(200m, CurrencyCode.Euro)));

        Assert.Contains("cash advance", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    // RF-6, RF-20: the destination being a credit card is only a matter of its balance moving,
    // which the postings already guarantee; no special-casing is needed for it to go positive.
    [Fact(DisplayName = "A transfer into a credit card is admitted, whatever it leaves the balance at")]
    public void a_transfer_into_a_credit_card_is_admitted_whatever_it_leaves_the_balance_at()
    {
        var creditCard = WithKey(
            Account.Create(HouseholdId, "Card", AccountType.CreditCard, CurrencyCode.Euro));

        var entry = JournalEntry.RegisterTransfer(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Card payment",
            Bank(),
            creditCard,
            Money.Of(200m, CurrencyCode.Euro));

        Assert.Equal(200m, entry.Postings.Single(p => p.AccountId == creditCard.Id).Amount);
    }

    private static JournalEntry RegisterTransfer(
        decimal amount, Account? from = null, Account? to = null, Category? category = null) =>
        JournalEntry.RegisterTransfer(
            HouseholdId,
            new DateOnly(2026, 8, 14),
            "Transfer",
            from ?? Bank(),
            to ?? Savings(),
            Money.Of(amount, CurrencyCode.Euro),
            category);

    // Story 013, RF-1, RF-2, RF-3: reversing an expense leaves the original intact and marked, and
    // the reversal cancels its postings out.
    [Fact(DisplayName = "Reversing an expense produces a reversal that cancels its postings out")]
    public void reversing_an_expense_produces_a_reversal_that_cancels_its_postings_out()
    {
        var entry = RegisterGroceries(42.35m);
        var bank = entry.Postings.Single(p => p.CategoryId is null).AccountId;
        var expenses = entry.Postings.Single(p => p.CategoryId is not null).AccountId;

        var reversal = entry.Reverse(DateTimeOffset.UtcNow);

        Assert.True(entry.IsVoided);
        Assert.Equal(2, entry.Postings.Count);
        Assert.Equal(-42.35m, reversal.Postings.Single(p => p.AccountId == expenses).Amount);
        Assert.Equal(42.35m, reversal.Postings.Single(p => p.AccountId == bank).Amount);
    }

    // RF-6: the link is visible from either side.
    [Fact(DisplayName = "A reversal points back at the movement it reverses")]
    public void a_reversal_points_back_at_the_movement_it_reverses()
    {
        var entry = RegisterGroceries(42.35m);
        WithKey(entry);

        var reversal = entry.Reverse(DateTimeOffset.UtcNow);

        Assert.Equal(entry.Id, reversal.ReversalOfEntryId);
    }

    // RF-11: the reversal carries the original's date, not today's.
    [Fact(DisplayName = "A reversal is dated the same as the movement it reverses")]
    public void a_reversal_is_dated_the_same_as_the_movement_it_reverses()
    {
        var entry = RegisterGroceries(42.35m);

        var reversal = entry.Reverse(DateTimeOffset.UtcNow);

        Assert.Equal(entry.OccurredOn, reversal.OccurredOn);
    }

    // RF-7: a movement already voided cannot be voided a second time.
    [Fact(DisplayName = "A movement already voided cannot be voided a second time")]
    public void a_movement_already_voided_cannot_be_voided_a_second_time()
    {
        var entry = RegisterGroceries(42.35m);
        entry.Reverse(DateTimeOffset.UtcNow);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => entry.Reverse(DateTimeOffset.UtcNow));

        Assert.Contains("already", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    // RF-8: a reversal is the end of the correction chain.
    [Fact(DisplayName = "A reversal cannot itself be voided")]
    public void a_reversal_cannot_itself_be_voided()
    {
        var entry = RegisterGroceries(42.35m);
        var reversal = entry.Reverse(DateTimeOffset.UtcNow);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => reversal.Reverse(DateTimeOffset.UtcNow));

        Assert.Contains("chain", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    // RF-12: the opening balance has its own correction, not a reversal.
    [Fact(DisplayName = "The opening balance cannot be voided")]
    public void the_opening_balance_cannot_be_voided()
    {
        var account = Bank();
        var entry = JournalEntry.OpenBalance(
            HouseholdId, account, Money.Of(500m, CurrencyCode.Euro), new DateOnly(2026, 8, 1));

        var thrown = Assert.Throws<InvalidOperationException>(
            () => entry.Reverse(DateTimeOffset.UtcNow));

        Assert.Contains("editing", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    // RF-1, RF-10: an income is reversed the same way, negating its own pair of postings.
    [Fact(DisplayName = "Reversing an income produces a reversal that cancels its postings out")]
    public void reversing_an_income_produces_a_reversal_that_cancels_its_postings_out()
    {
        var entry = RegisterSalary(1500m);
        var bank = entry.Postings.Single(p => p.CategoryId is null).AccountId;
        var incomes = entry.Postings.Single(p => p.CategoryId is not null).AccountId;

        var reversal = entry.Reverse(DateTimeOffset.UtcNow);

        Assert.True(entry.IsVoided);
        Assert.Equal(-1500m, reversal.Postings.Single(p => p.AccountId == bank).Amount);
        Assert.Equal(1500m, reversal.Postings.Single(p => p.AccountId == incomes).Amount);
    }

    // RF-1, RF-10: a transfer is reversed the same way, negating its own pair of postings.
    [Fact(DisplayName = "Reversing a transfer produces a reversal that cancels its postings out")]
    public void reversing_a_transfer_produces_a_reversal_that_cancels_its_postings_out()
    {
        var entry = RegisterTransfer(200m);
        var origin = entry.Postings.Single(p => p.Amount < 0m).AccountId;
        var destination = entry.Postings.Single(p => p.Amount > 0m).AccountId;

        var reversal = entry.Reverse(DateTimeOffset.UtcNow);

        Assert.True(entry.IsVoided);
        Assert.Equal(200m, reversal.Postings.Single(p => p.AccountId == origin).Amount);
        Assert.Equal(-200m, reversal.Postings.Single(p => p.AccountId == destination).Amount);
    }

    private static Account Bank() =>
        WithKey(Account.Create(
            HouseholdId, "Joint account", AccountType.Checking, CurrencyCode.Euro));

    private static Account Savings() =>
        WithKey(Account.Create(
            HouseholdId, "Savings", AccountType.Savings, CurrencyCode.Euro));

    private static Account Uncontrolled() =>
        WithKey(Account.Create(
            HouseholdId,
            "Personal wallet",
            AccountType.Savings,
            CurrencyCode.Euro,
            isTracked: false));

    private static Account Expenses() =>
        WithKey(Account.Create(HouseholdId, "Expenses", AccountType.Expense, CurrencyCode.Euro));

    private static Account Incomes() =>
        WithKey(Account.Create(HouseholdId, "Income", AccountType.Income, CurrencyCode.Euro));

    private static Category Groceries() =>
        WithKey(Category.Create(HouseholdId, "Groceries", CategoryKind.Expense, 2));

    private static Category Salary() =>
        WithKey(Category.Create(HouseholdId, "Salary", CategoryKind.Income, 2));

    private static T WithKey<T>(T entity)
    {
        typeof(T).GetProperty(nameof(Account.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetMethod!
            .Invoke(entity, [++_nextKey]);

        return entity;
    }
}
