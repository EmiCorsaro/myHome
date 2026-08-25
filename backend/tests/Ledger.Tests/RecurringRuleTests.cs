using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

public sealed class RecurringRuleTests
{
    private static readonly DateOnly FirstDue = new(2026, 8, 10);

    [Fact(DisplayName = "A rule keeps the declared amount, not what was last paid")]
    public void a_rule_keeps_the_declared_amount()
    {
        var rule = PhoneRule(25m);

        Assert.Equal(25m, rule.Amount);
        Assert.Equal(10, rule.DayOfMonth);
        Assert.Equal(new DateOnly(2026, 8, 1), rule.StartsOn);
    }

    [Fact(DisplayName = "A monthly rule falls on its day every month")]
    public void a_monthly_rule_falls_on_its_day_every_month()
    {
        var due = PhoneRule(25m)
            .OccurrencesBetween(new DateOnly(2026, 8, 1), new DateOnly(2026, 11, 30))
            .ToArray();

        Assert.Equal(
            [
                new DateOnly(2026, 8, 10),
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 10, 10),
                new DateOnly(2026, 11, 10),
            ],
            due);
    }

    [Fact(DisplayName = "A quarterly rule stays on its own quarter when the window starts late")]
    public void a_quarterly_rule_stays_on_its_own_quarter()
    {
        var due = Rule(RecurrenceFrequency.Quarterly)
            .OccurrencesBetween(new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31))
            .ToArray();

        Assert.Equal([new DateOnly(2026, 11, 10)], due);
    }

    [Fact(DisplayName = "A bimonthly rule skips every other month")]
    public void a_bimonthly_rule_skips_every_other_month()
    {
        var due = Rule(RecurrenceFrequency.BiMonthly)
            .OccurrencesBetween(new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31))
            .ToArray();

        Assert.Equal(
            [new DateOnly(2026, 8, 10), new DateOnly(2026, 10, 10), new DateOnly(2026, 12, 10)],
            due);
    }

    [Fact(DisplayName = "An occurrence due inside the window still counts when the window opens mid-month")]
    public void an_occurrence_due_inside_the_window_still_counts()
    {
        var due = PhoneRule(25m)
            .OccurrencesBetween(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 31))
            .ToArray();

        Assert.Equal([new DateOnly(2026, 8, 10)], due);
    }

    [Fact(DisplayName = "An occurrence that already passed when the window opens is left out")]
    public void an_occurrence_that_already_passed_is_left_out()
    {
        var due = PhoneRule(25m)
            .OccurrencesBetween(new DateOnly(2026, 8, 11), new DateOnly(2026, 8, 31))
            .ToArray();

        Assert.Empty(due);
    }

    [Fact(DisplayName = "A rule produces nothing past its end date")]
    public void a_rule_produces_nothing_past_its_end_date()
    {
        var rule = PhoneRule(25m);
        rule.ScheduleEnd(new DateOnly(2026, 10, 1));

        var due = rule
            .OccurrencesBetween(new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31))
            .ToArray();

        Assert.Equal([new DateOnly(2026, 8, 10), new DateOnly(2026, 9, 10)], due);
        Assert.False(rule.OccursIn(new DateOnly(2026, 10, 1)));
    }

    [Fact(DisplayName = "A deactivated rule produces nothing at all")]
    public void a_deactivated_rule_produces_nothing_at_all()
    {
        var rule = PhoneRule(25m);
        rule.Deactivate();

        Assert.Empty(rule.OccurrencesBetween(new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31)));
        Assert.False(rule.OccursIn(new DateOnly(2026, 9, 1)));
    }

    [Fact(DisplayName = "A rule cannot end before its first occurrence")]
    public void a_rule_cannot_end_before_its_first_occurrence()
    {
        Assert.Throws<InvalidOperationException>(() => RecurringRule.Create(
            HouseholdId,
            EntryKind.Expense,
            RecurrenceFrequency.Monthly,
            Bank(),
            Phone(),
            "Telefonía móvil",
            Euros(25m),
            FirstDue,
            endsOn: new DateOnly(2026, 7, 1)));
    }

    [Fact(DisplayName = "Amending a rule changes what to expect from here on")]
    public void amending_a_rule_changes_what_to_expect_from_here_on()
    {
        var rule = PhoneRule(25m);

        rule.Amend(Euros(30m), dayOfMonth: 15);

        Assert.Equal(30m, rule.Amount);
        Assert.Equal(15, rule.DayOfMonth);

        Assert.Equal(new DateOnly(2026, 8, 1), rule.StartsOn);
    }

    [Fact(DisplayName = "A rule cannot be amended into another currency")]
    public void a_rule_cannot_be_amended_into_another_currency()
    {
        Assert.Throws<InvalidOperationException>(
            () => PhoneRule(25m).Amend(Money.Of(30m, CurrencyCode.UsDollar)));
    }

    [Fact(DisplayName = "A rule cannot be paid out of a nominal account")]
    public void a_rule_cannot_be_paid_out_of_a_nominal_account()
    {
        Assert.Throws<InvalidOperationException>(() => RecurringRule.Create(
            HouseholdId,
            EntryKind.Expense,
            RecurrenceFrequency.Monthly,
            Expenses(),
            Phone(),
            "Telefonía móvil",
            Euros(25m),
            FirstDue));
    }

    [Fact(DisplayName = "An income category cannot classify a recurring expense")]
    public void an_income_category_cannot_classify_a_recurring_expense()
    {
        Assert.Throws<InvalidOperationException>(() => RecurringRule.Create(
            HouseholdId,
            EntryKind.Expense,
            RecurrenceFrequency.Monthly,
            Bank(),
            Salary(),
            "Telefonía móvil",
            Euros(25m),
            FirstDue));
    }

    [Fact(DisplayName = "A rule in a currency the account does not work in is refused")]
    public void a_rule_in_a_currency_the_account_does_not_work_in_is_refused()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => RecurringRule.Create(
            HouseholdId,
            EntryKind.Expense,
            RecurrenceFrequency.Monthly,
            Bank(),
            Phone(),
            "Telefonía móvil",
            Money.Of(25m, CurrencyCode.UsDollar),
            FirstDue));

        Assert.Contains("exchange rate", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory(DisplayName = "The amount of a rule has to be positive")]
    [InlineData(0)]
    [InlineData(-10)]
    public void the_amount_of_a_rule_has_to_be_positive(decimal amount)
    {
        Assert.Throws<ArgumentException>(() => PhoneRule(amount));
    }

    [Fact(DisplayName = "A day past the 28th is pulled back so it exists in every month")]
    public void a_day_past_the_28th_is_pulled_back()
    {
        var rule = RecurringRule.Create(
            HouseholdId,
            EntryKind.Expense,
            RecurrenceFrequency.Monthly,
            Bank(),
            Phone(),
            "Alquiler",
            Euros(1439m),
            new DateOnly(2026, 8, 31));

        Assert.Equal(28, rule.DayOfMonth);
    }

    private static RecurringRule PhoneRule(decimal amount) => RecurringRule.Create(
        HouseholdId,
        EntryKind.Expense,
        RecurrenceFrequency.Monthly,
        Bank(),
        Phone(),
        "Telefonía móvil",
        Euros(amount),
        FirstDue);

    private static RecurringRule Rule(RecurrenceFrequency frequency) => RecurringRule.Create(
        HouseholdId,
        EntryKind.Expense,
        frequency,
        Bank(),
        Phone(),
        "Telefonía móvil",
        Euros(25m),
        FirstDue);
}
