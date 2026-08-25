using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

public sealed class PlannedMovementTests
{
    private static readonly DateOnly Due = new(2026, 9, 3);

    [Fact(DisplayName = "Settling an expense above its budget reports a positive variance")]
    public void settling_an_expense_above_its_budget_reports_a_positive_variance()
    {
        var planned = PhoneBill();

        planned.Settle(Expense(27.40m), Euros(27.40m));

        Assert.Equal(2.40m, planned.Variance);
        Assert.True(planned.IsSettled);
    }

    [Fact(DisplayName = "Settling an expense below its budget reports a negative variance")]
    public void settling_an_expense_below_its_budget_reports_a_negative_variance()
    {
        var planned = PhoneBill();

        planned.Settle(Expense(22m), Euros(22m));

        Assert.Equal(-3m, planned.Variance);
    }

    [Fact(DisplayName = "An income that pays less than expected also reports a positive variance")]
    public void an_income_that_pays_less_than_expected_also_reports_a_positive_variance()
    {
        var planned = Payroll(2530m);

        planned.Settle(IncomeEntry(2400m), Euros(2400m));

        Assert.Equal(130m, planned.Variance);
    }

    [Fact(DisplayName = "A pending movement has no variance yet")]
    public void a_pending_movement_has_no_variance_yet()
    {
        Assert.Null(PhoneBill().Variance);
    }

    [Fact(DisplayName = "The same movement cannot be settled twice")]
    public void the_same_movement_cannot_be_settled_twice()
    {
        var planned = PhoneBill();
        planned.Settle(Expense(25m), Euros(25m));

        var thrown = Assert.Throws<InvalidOperationException>(
            () => planned.Settle(Expense(25m), Euros(25m)));

        Assert.Contains("already been settled", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "A skipped movement is not expecting an entry")]
    public void a_skipped_movement_is_not_expecting_an_entry()
    {
        var planned = PhoneBill();
        planned.Skip();

        Assert.Throws<InvalidOperationException>(() => planned.Settle(Expense(25m), Euros(25m)));
    }

    [Fact(DisplayName = "A settled movement cannot be skipped")]
    public void a_settled_movement_cannot_be_skipped()
    {
        var planned = PhoneBill();
        planned.Settle(Expense(25m), Euros(25m));

        Assert.Throws<InvalidOperationException>(planned.Skip);
    }

    [Fact(DisplayName = "Unsettling puts the movement back where it was")]
    public void unsettling_puts_the_movement_back_where_it_was()
    {
        var planned = PhoneBill();
        planned.Settle(Expense(27.40m), Euros(27.40m));

        planned.Unsettle();

        Assert.False(planned.IsSettled);
        Assert.Null(planned.Variance);
        Assert.Null(planned.JournalEntryId);
        Assert.Null(planned.SettledAt);
    }

    [Fact(DisplayName = "An entry in another currency cannot settle the movement")]
    public void an_entry_in_another_currency_cannot_settle_the_movement()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => PhoneBill().Settle(Expense(25m), Money.Of(25m, CurrencyCode.UsDollar)));

        Assert.Contains("exchange rate", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "An income entry cannot settle a planned expense")]
    public void an_income_entry_cannot_settle_a_planned_expense()
    {
        Assert.Throws<InvalidOperationException>(
            () => PhoneBill().Settle(IncomeEntry(25m), Euros(25m)));
    }

    [Fact(DisplayName = "An entry from another household cannot settle the movement")]
    public void an_entry_from_another_household_cannot_settle_the_movement()
    {
        var foreign = JournalEntry.RegisterExpense(
            HouseholdId + 1,
            Due,
            "Someone else's phone",
            WithKey(Account.Create(
                HouseholdId + 1, "Their bank", AccountType.Checking, CurrencyCode.Euro)),
            WithKey(Account.Create(
                HouseholdId + 1, "Their expenses", AccountType.Expense, CurrencyCode.Euro)),
            WithKey(Category.Create(HouseholdId + 1, "Their mobile", CategoryKind.Expense, 3)),
            Euros(25m));

        Assert.Throws<InvalidOperationException>(() => PhoneBill().Settle(foreign, Euros(25m)));
    }

    [Theory(DisplayName = "An entry within the tolerance window is a candidate")]
    [InlineData(2026, 8, 29)]
    [InlineData(2026, 9, 3)]
    [InlineData(2026, 9, 8)]
    public void an_entry_within_the_tolerance_window_is_a_candidate(int year, int month, int day)
    {
        var planned = PhoneBill();

        Assert.True(planned.IsCandidateFor(
            planned.AccountId, planned.CategoryId, new DateOnly(year, month, day)));
    }

    [Theory(DisplayName = "An entry outside the tolerance window is not a candidate")]
    [InlineData(2026, 8, 28)]
    [InlineData(2026, 9, 9)]
    public void an_entry_outside_the_tolerance_window_is_not_a_candidate(
        int year, int month, int day)
    {
        var planned = PhoneBill();

        Assert.False(planned.IsCandidateFor(
            planned.AccountId, planned.CategoryId, new DateOnly(year, month, day)));
    }

    [Fact(DisplayName = "Matching ignores the amount, which is the whole point of the variance")]
    public void matching_ignores_the_amount()
    {
        var planned = PhoneBill();

        Assert.True(planned.IsCandidateFor(planned.AccountId, planned.CategoryId, Due));

        planned.Settle(Expense(27.40m), Euros(27.40m));

        Assert.Equal(2.40m, planned.Variance);
    }

    [Fact(DisplayName = "An entry in another category is never a candidate")]
    public void an_entry_in_another_category_is_never_a_candidate()
    {
        var planned = PhoneBill();

        Assert.False(planned.IsCandidateFor(planned.AccountId, Phone("Internet").Id, Due));
    }

    [Fact(DisplayName = "A settled movement is no longer a candidate for anything")]
    public void a_settled_movement_is_no_longer_a_candidate()
    {
        var planned = PhoneBill();
        planned.Settle(Expense(25m), Euros(25m));

        Assert.False(planned.IsCandidateFor(planned.AccountId, planned.CategoryId, Due));
    }

    [Theory(DisplayName = "A movement is overdue only once its tolerance has run out")]
    [InlineData(2026, 9, 8, false)]
    [InlineData(2026, 9, 9, true)]
    public void a_movement_is_overdue_only_once_its_tolerance_has_run_out(
        int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, PhoneBill().IsOverdueOn(new DateOnly(year, month, day)));
    }

    [Fact(DisplayName = "A settled movement is never overdue")]
    public void a_settled_movement_is_never_overdue()
    {
        var planned = PhoneBill();
        planned.Settle(Expense(25m), Euros(25m));

        Assert.False(planned.IsOverdueOn(new DateOnly(2027, 1, 1)));
    }

    [Fact(DisplayName = "Re-estimating a pending movement moves the expectation, not the fact")]
    public void reestimating_a_pending_movement_moves_the_expectation()
    {
        var planned = PhoneBill();

        planned.Reestimate(Euros(94m));

        Assert.Equal(94m, planned.ExpectedAmount);
        Assert.Null(planned.Variance);
    }

    [Fact(DisplayName = "A settled movement cannot be re-estimated or rescheduled")]
    public void a_settled_movement_cannot_be_reestimated_or_rescheduled()
    {
        var planned = PhoneBill();
        planned.Settle(Expense(27.40m), Euros(27.40m));

        Assert.Throws<InvalidOperationException>(() => planned.Reestimate(Euros(27.40m)));
        Assert.Throws<InvalidOperationException>(() => planned.Reschedule(new DateOnly(2026, 9, 4)));
    }

    [Theory(DisplayName = "The expected amount of a planned movement has to be positive")]
    [InlineData(0)]
    [InlineData(-10)]
    public void the_expected_amount_has_to_be_positive(decimal amount)
    {
        Assert.Throws<ArgumentException>(() => PlannedMovement.Create(
            HouseholdId,
            EntryKind.Expense,
            Bank(),
            Phone(),
            "Telefonía móvil",
            Euros(amount),
            Due));
    }

    [Fact(DisplayName = "An income category cannot classify a planned expense")]
    public void an_income_category_cannot_classify_a_planned_expense()
    {
        Assert.Throws<InvalidOperationException>(() => PlannedMovement.Create(
            HouseholdId,
            EntryKind.Expense,
            Bank(),
            Salary(),
            "Telefonía móvil",
            Euros(25m),
            Due));
    }

    [Fact(DisplayName = "Nothing is ever expected to move through a nominal account")]
    public void nothing_is_ever_expected_to_move_through_a_nominal_account()
    {
        Assert.Throws<InvalidOperationException>(() => PlannedMovement.Create(
            HouseholdId,
            EntryKind.Expense,
            Expenses(),
            Phone(),
            "Telefonía móvil",
            Euros(25m),
            Due));
    }

    [Fact(DisplayName = "A transfer is not budgeted")]
    public void a_transfer_is_not_budgeted()
    {
        Assert.Throws<InvalidOperationException>(() => PlannedMovement.Create(
            HouseholdId,
            EntryKind.Transfer,
            Bank(),
            Phone(),
            "Traspaso",
            Euros(25m),
            Due));
    }

    [Fact(DisplayName = "An account from another household cannot be planned against")]
    public void an_account_from_another_household_cannot_be_planned_against()
    {
        var foreign = WithKey(Account.Create(
            HouseholdId + 1, "Their bank", AccountType.Checking, CurrencyCode.Euro));

        Assert.Throws<InvalidOperationException>(() => PlannedMovement.Create(
            HouseholdId,
            EntryKind.Expense,
            foreign,
            Phone(),
            "Telefonía móvil",
            Euros(25m),
            Due));
    }

    private static PlannedMovement PhoneBill() =>
        WithKey(PlannedMovement.Create(
            HouseholdId,
            EntryKind.Expense,
            Bank(),
            Phone(),
            "Telefonía móvil",
            Euros(25m),
            Due));

    private static JournalEntry Expense(decimal amount) =>
        WithKey(JournalEntry.RegisterExpense(
            HouseholdId, Due, "Telefonía móvil", Bank(), Expenses(), Phone(), Euros(amount)));

    private static PlannedMovement Payroll(decimal amount) =>
        WithKey(PlannedMovement.Create(
            HouseholdId,
            EntryKind.Income,
            Bank(),
            Salary(),
            "Nómina",
            Euros(amount),
            Due));

    private static JournalEntry IncomeEntry(decimal amount)
    {
        var entry = JournalEntry.RegisterExpense(
            HouseholdId, Due, "Nómina", Bank(), Expenses(), Phone(), Euros(amount));

        typeof(JournalEntry)
            .GetProperty(nameof(JournalEntry.Kind))!
            .SetMethod!
            .Invoke(entry, [EntryKind.Income]);

        return WithKey(entry);
    }
}
