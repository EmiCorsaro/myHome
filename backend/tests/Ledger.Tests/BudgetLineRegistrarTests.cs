using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Budget;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Declaring a budget line, as stories 051 and 052 specify it. Every test names the requirement it
/// pins down; the maps from RF to test live in <c>progress/impl_051.md</c> and
/// <c>progress/impl_052.md</c>.
/// </summary>
public sealed class BudgetLineRegistrarTests : IDisposable
{
    /// <summary>The day the household is standing on for every test in this class.</summary>
    private static readonly DateOnly Today = new(2026, 9, 20);

    private static readonly TimeProvider Clock =
        new FixedTimeProvider(new DateTimeOffset(Today, new TimeOnly(10, 0), TimeSpan.Zero));

    private static readonly DateOnly ThisMonth = new(2026, 9, 1);

    private readonly LedgerDatabase _database = new();
    private readonly BudgetLineRegistrar _registrar;

    private Account _account = null!;
    private Category _groceries = null!;
    private Category _salary = null!;

    public BudgetLineRegistrarTests()
    {
        _registrar = NewRegistrar();
    }

    public void Dispose() => _database.Dispose();

    // RF-1
    [Fact(DisplayName = "A declared line belongs to the household and to the month asked for")]
    public async Task a_declared_line_belongs_to_the_household_and_to_the_month_asked_for()
    {
        await SeedAsync();

        var declared = await _registrar.DeclareAsync(Declare(amount: 300m));

        var stored = await _database.Context.BudgetLines
            .SingleAsync(b => b.PublicId == declared.Id);

        Assert.Equal(HouseholdId, stored.HouseholdId);
        Assert.Equal(ThisMonth, stored.PeriodStart);
        Assert.Equal(_groceries.Id, stored.CategoryId);
        Assert.Equal(_account.Id, stored.AccountId);
        Assert.Equal(300m, stored.Amount);
        Assert.Equal(BudgetLineSign.Expense, stored.Sign);
    }

    // RF-2, RF-12
    [Fact(DisplayName = "A line is declared without a calendar and plans no dated occurrence")]
    public async Task a_line_is_declared_without_a_calendar_and_plans_no_dated_occurrence()
    {
        await SeedAsync();

        await _registrar.DeclareAsync(Declare(amount: 300m));

        // Nothing in the request says when, and nothing in the database says when either.
        Assert.Empty(await _database.Context.PlannedMovements.ToListAsync());
        Assert.Empty(await _database.Context.RecurringRules.ToListAsync());
    }

    // RF-3, RF-4
    [Theory(DisplayName = "The mode asked for is the mode stored and the mode published")]
    [InlineData(BudgetAmountModes.Fixed, PlannedAmountMode.Fixed)]
    [InlineData(BudgetAmountModes.Estimated, PlannedAmountMode.Estimated)]
    public async Task the_mode_asked_for_is_the_mode_stored_and_the_mode_published(
        string requested,
        PlannedAmountMode expected)
    {
        await SeedAsync();

        var declared = await _registrar.DeclareAsync(Declare(amount: 300m, mode: requested));

        Assert.Equal(requested, declared.AmountMode);
        Assert.Equal(expected, (await _database.Context.BudgetLines.SingleAsync()).AmountMode);
    }

    // RF-5
    [Fact(DisplayName = "Any day of the month declares the whole natural month")]
    public async Task any_day_of_the_month_declares_the_whole_natural_month()
    {
        await SeedAsync();

        var declared = await _registrar.DeclareAsync(
            Declare(month: new DateOnly(2026, 9, 17), amount: 300m));

        Assert.Equal(new DateOnly(2026, 9, 1), declared.PeriodStart);
        Assert.Equal(new DateOnly(2026, 9, 30), declared.PeriodEnd);
    }

    // RF-6
    [Fact(DisplayName = "A category already declared that month takes no second line")]
    public async Task a_category_already_declared_that_month_takes_no_second_line()
    {
        await SeedAsync();

        await _registrar.DeclareAsync(Declare(amount: 300m));

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(Declare(amount: 400m)));

        Assert.Equal(
            "Esa categoría ya tiene una línea de presupuesto en ese mes.",
            Assert.Single(error.Errors["categoryId"]));

        Assert.Equal(1, await _database.Context.BudgetLines.CountAsync());
    }

    // RF-6, edge case: the same category in the next month is a separate declaration
    [Fact(DisplayName = "The same category may be declared again in another month")]
    public async Task the_same_category_may_be_declared_again_in_another_month()
    {
        await SeedAsync();

        await _registrar.DeclareAsync(Declare(amount: 300m));
        await _registrar.DeclareAsync(Declare(month: new DateOnly(2026, 10, 1), amount: 320m));

        Assert.Equal(2, await _database.Context.BudgetLines.CountAsync());
    }

    // RF-7
    [Theory(DisplayName = "An amount of zero or less is refused")]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task an_amount_of_zero_or_less_is_refused(decimal amount)
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(Declare(amount: amount)));

        Assert.Equal(
            "El importe debe ser mayor que cero.",
            Assert.Single(error.Errors["amount"]));

        Assert.Empty(await _database.Context.BudgetLines.ToListAsync());
    }

    // RF-7, edge case: decimals are accepted
    [Fact(DisplayName = "An amount with decimals is declared as typed")]
    public async Task an_amount_with_decimals_is_declared_as_typed()
    {
        await SeedAsync();

        Assert.Equal(312.50m, (await _registrar.DeclareAsync(Declare(amount: 312.50m))).Amount);
    }

    // RF-8
    [Fact(DisplayName = "An archived category is not available to declare against")]
    public async Task an_archived_category_is_not_available_to_declare_against()
    {
        await SeedAsync();

        _groceries.Archive();
        await _database.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(Declare(amount: 300m)));

        Assert.Equal("Esa categoría no está disponible.", Assert.Single(error.Errors["categoryId"]));
    }

    // RF-8
    [Fact(DisplayName = "Another household's category is not available to declare against")]
    public async Task another_households_category_is_not_available_to_declare_against()
    {
        await SeedAsync();

        var foreign = Category.Create(
            HouseholdId + 1, "Their groceries", CategoryKind.Expense, 2);

        _database.Context.Categories.Add(foreign);
        await _database.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(Declare(amount: 300m) with { CategoryId = foreign.PublicId }));

        Assert.Equal("Esa categoría no está disponible.", Assert.Single(error.Errors["categoryId"]));
    }

    // RF-9
    [Fact(DisplayName = "An archived account is not available to declare against")]
    public async Task an_archived_account_is_not_available_to_declare_against()
    {
        await SeedAsync();

        _account.Archive();
        await _database.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(Declare(amount: 300m)));

        Assert.Equal("Esa cuenta no está disponible.", Assert.Single(error.Errors["accountId"]));
    }

    // RF-9
    [Fact(DisplayName = "Another household's account is not available to declare against")]
    public async Task another_households_account_is_not_available_to_declare_against()
    {
        await SeedAsync();

        var foreign = Account.Create(
            HouseholdId + 1, "Their bank", AccountType.Checking, CurrencyCode.Euro);

        _database.Context.Accounts.Add(foreign);
        await _database.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(Declare(amount: 300m) with { AccountId = foreign.PublicId }));

        Assert.Equal("Esa cuenta no está disponible.", Assert.Single(error.Errors["accountId"]));
    }

    // RF-10
    [Fact(DisplayName = "An income category does not match what an expense line declares")]
    public async Task an_income_category_does_not_match_what_an_expense_line_declares()
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(
                Declare(amount: 300m) with { CategoryId = _salary.PublicId }));

        Assert.Equal(
            "Esa categoría clasifica ingresos, no gastos.",
            Assert.Single(error.Errors["categoryId"]));

        Assert.Empty(await _database.Context.BudgetLines.ToListAsync());
    }

    // RF-11, RF-18, RF-19
    [Fact(DisplayName = "The published line carries the daily share of its month")]
    public async Task the_published_line_carries_the_daily_share_of_its_month()
    {
        await SeedAsync();

        // September has 30 days: 100 € is 3,33 € a day with 0,10 € left for the 30th.
        var declared = await _registrar.DeclareAsync(Declare(amount: 100m));

        Assert.Equal(3.33m, declared.DailyAmount);
    }

    // RF-15
    [Fact(DisplayName = "The line takes the currency of the account it was declared against")]
    public async Task the_line_takes_the_currency_of_the_account_it_was_declared_against()
    {
        await SeedAsync(CurrencyCode.UsDollar);

        var declared = await _registrar.DeclareAsync(Declare(amount: 300m));

        Assert.Equal("USD", declared.Currency);
        Assert.Equal(CurrencyCode.UsDollar, (await _database.Context.BudgetLines.SingleAsync()).Currency);
    }

    // RF-16
    [Fact(DisplayName = "Any member of the household may declare a line, with no special role")]
    public async Task any_member_of_the_household_may_declare_a_line_with_no_special_role()
    {
        await SeedAsync();

        // A member that is not the owner and carries no role at all: nothing in the request says
        // who is asking beyond the household, and nothing here consults a role.
        var registrar = new BudgetLineRegistrar(
            _database.Context,
            new TestTenantContext(HouseholdId, memberId: 42),
            new DeclareBudgetLineRequestValidator(Clock));

        var declared = await registrar.DeclareAsync(Declare(amount: 300m));

        Assert.Equal(300m, declared.Amount);
    }

    // RF-21
    [Theory(DisplayName = "A month already past cannot be declared")]
    [InlineData(2026, 8, 31)]
    [InlineData(2026, 1, 1)]
    [InlineData(2025, 12, 15)]
    public async Task a_month_already_past_cannot_be_declared(int year, int month, int day)
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(
                Declare(month: new DateOnly(year, month, day), amount: 300m)));

        Assert.Equal(
            "Un mes ya pasado solo se consulta: no se puede presupuestar.",
            Assert.Single(error.Errors["month"]));

        Assert.Empty(await _database.Context.BudgetLines.ToListAsync());
    }

    // RF-22
    [Theory(DisplayName = "The month in progress and any later month are accepted")]
    [InlineData(2026, 9, 1)]
    [InlineData(2026, 9, 30)]
    [InlineData(2026, 10, 1)]
    [InlineData(2027, 3, 1)]
    public async Task the_month_in_progress_and_any_later_month_are_accepted(
        int year,
        int month,
        int day)
    {
        await SeedAsync();

        var declared = await _registrar.DeclareAsync(
            Declare(month: new DateOnly(year, month, day), amount: 300m));

        Assert.Equal(new DateOnly(year, month, 1), declared.PeriodStart);
    }

    // RF-3: a mode that is neither of the two published ones is refused
    [Fact(DisplayName = "An unknown amount mode is refused")]
    public async Task an_unknown_amount_mode_is_refused()
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(Declare(amount: 300m, mode: "whatever")));

        Assert.Equal(
            "Elige si el importe es fijo o estimado.",
            Assert.Single(error.Errors["amountMode"]));
    }

    // ---------------------------------------------------------------------------------------
    // Story 052 — the income sign. The map from RF to test lives in progress/impl_052.md.
    // ---------------------------------------------------------------------------------------

    // 052 RF-1, RF-2
    [Fact(DisplayName = "A declared income line is stored with its sign, month and origin")]
    public async Task a_declared_income_line_is_stored_with_its_sign_month_and_origin()
    {
        await SeedAsync();

        var declared = await _registrar.DeclareAsync(DeclareIncome(amount: 2530m));

        var stored = await _database.Context.BudgetLines
            .SingleAsync(b => b.PublicId == declared.Id);

        Assert.Equal(HouseholdId, stored.HouseholdId);
        Assert.Equal(ThisMonth, stored.PeriodStart);
        Assert.Equal(_salary.Id, stored.CategoryId);
        Assert.Equal(_account.Id, stored.AccountId);
        Assert.Equal(2530m, stored.Amount);
        Assert.Equal(BudgetLineSign.Income, stored.Sign);
        Assert.Equal(BudgetIncomeOrigin.Payroll, stored.Origin);

        Assert.Equal(BudgetLineSigns.Income, declared.Sign);
        Assert.Equal(BudgetIncomeOrigins.Payroll, declared.Origin);
    }

    // 052 RF-1: the sign is a choice now, and the 051 contract keeps meaning what it meant
    [Fact(DisplayName = "A request that says nothing about the sign declares an expense")]
    public async Task a_request_that_says_nothing_about_the_sign_declares_an_expense()
    {
        await SeedAsync();

        // Exactly the request story 051 published, positional arguments and all.
        var declared = await _registrar.DeclareAsync(
            new DeclareBudgetLineRequest(
                _groceries.PublicId, _account.PublicId, ThisMonth, 300m));

        Assert.Equal(BudgetLineSigns.Expense, declared.Sign);
        Assert.Null(declared.Origin);
        Assert.Equal(
            BudgetLineSign.Expense,
            (await _database.Context.BudgetLines.SingleAsync()).Sign);
    }

    // 052 RF-1: a sign that is neither of the two published ones is refused
    [Fact(DisplayName = "An unknown sign is refused")]
    public async Task an_unknown_sign_is_refused()
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(Declare(amount: 300m) with { Sign = "sideways" }));

        Assert.Equal(
            "Elige si la línea declara un gasto o un ingreso.",
            Assert.Single(error.Errors["sign"]));
    }

    // 052 RF-3, RF-4
    [Theory(DisplayName = "An income of zero or less is refused")]
    [InlineData(0)]
    [InlineData(-2530)]
    public async Task an_income_of_zero_or_less_is_refused(decimal amount)
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(DeclareIncome(amount: amount)));

        Assert.Equal("El importe debe ser mayor que cero.", Assert.Single(error.Errors["amount"]));
        Assert.Empty(await _database.Context.BudgetLines.ToListAsync());
    }

    // 052 RF-5: and the message says what the chosen category really classifies
    [Fact(DisplayName = "An expense category does not match what an income line declares")]
    public async Task an_expense_category_does_not_match_what_an_income_line_declares()
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(
                DeclareIncome(amount: 2530m) with { CategoryId = _groceries.PublicId }));

        Assert.Equal(
            "Esa categoría clasifica gastos, no ingresos.",
            Assert.Single(error.Errors["categoryId"]));

        Assert.Empty(await _database.Context.BudgetLines.ToListAsync());
    }

    // 052 RF-10
    [Theory(DisplayName = "An income line is declared fixed or estimated as asked")]
    [InlineData(BudgetAmountModes.Fixed, PlannedAmountMode.Fixed)]
    [InlineData(BudgetAmountModes.Estimated, PlannedAmountMode.Estimated)]
    public async Task an_income_line_is_declared_fixed_or_estimated_as_asked(
        string requested,
        PlannedAmountMode expected)
    {
        await SeedAsync();

        var declared = await _registrar.DeclareAsync(DeclareIncome(mode: requested));

        Assert.Equal(requested, declared.AmountMode);
        Assert.Equal(expected, (await _database.Context.BudgetLines.SingleAsync()).AmountMode);
    }

    // 052 RF-11: declaring an income is an expectation, not a receipt
    [Fact(DisplayName = "Declaring an income line moves no money and no balance")]
    public async Task declaring_an_income_line_moves_no_money_and_no_balance()
    {
        await SeedAsync();

        await _registrar.DeclareAsync(DeclareIncome(amount: 2530m));

        // A balance in this ledger is the sum of the postings on the account. No entry, no
        // posting, no planned occurrence: nothing declared, nothing moved.
        Assert.Empty(await _database.Context.Entries.ToListAsync());
        Assert.Empty(await _database.Context.Postings.ToListAsync());
        Assert.Empty(await _database.Context.PlannedMovements.ToListAsync());

        Assert.Equal(
            0m,
            await _database.Context.Postings
                .Where(posting => posting.AccountId == _account.Id)
                .SumAsync(posting => posting.Amount));
    }

    // 052 RF-12, edge case: two income sources sharing a category do not fit as two lines
    [Fact(DisplayName = "An income category already declared that month takes no second line")]
    public async Task an_income_category_already_declared_that_month_takes_no_second_line()
    {
        await SeedAsync();

        await _registrar.DeclareAsync(DeclareIncome(amount: 2530m));

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(DeclareIncome(amount: 900m)));

        Assert.Equal(
            "Esa categoría ya tiene una línea de presupuesto en ese mes.",
            Assert.Single(error.Errors["categoryId"]));

        Assert.Equal(1, await _database.Context.BudgetLines.CountAsync());
    }

    // 052 RF-12, edge case: a different origin does not buy a second line either
    [Fact(DisplayName = "A second origin does not break the one line per category and month rule")]
    public async Task a_second_origin_does_not_break_the_one_line_per_category_and_month_rule()
    {
        await SeedAsync();

        await _registrar.DeclareAsync(DeclareIncome(amount: 2530m));

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(
                DeclareIncome(amount: 900m, origin: BudgetIncomeOrigins.Rental)));

        Assert.Equal(
            "Esa categoría ya tiene una línea de presupuesto en ese mes.",
            Assert.Single(error.Errors["categoryId"]));

        Assert.Equal(1, await _database.Context.BudgetLines.CountAsync());
    }

    // 052 RF-14: nothing demands an income line, and an expense-only month is declared normally
    [Fact(DisplayName = "A month takes expense lines with no income line declared")]
    public async Task a_month_takes_expense_lines_with_no_income_line_declared()
    {
        await SeedAsync();

        var declared = await _registrar.DeclareAsync(Declare(amount: 300m));

        Assert.Equal(BudgetLineSigns.Expense, declared.Sign);
        Assert.Equal(1, await _database.Context.BudgetLines.CountAsync());
    }

    // 052 RF-7, RF-15: an income without a calendar carries the same daily share as a spending one
    [Fact(DisplayName = "The published income line carries the daily share of its month")]
    public async Task the_published_income_line_carries_the_daily_share_of_its_month()
    {
        await SeedAsync();

        // September has 30 days: 100 € is 3,33 € a day with 0,10 € left for the 30th.
        var declared = await _registrar.DeclareAsync(DeclareIncome(amount: 100m));

        Assert.Equal(3.33m, declared.DailyAmount);
    }

    // 052 RF-9: the published line says which way it weighs, so nobody re-derives it
    [Fact(DisplayName = "The published line carries its amount already signed")]
    public async Task the_published_line_carries_its_amount_already_signed()
    {
        await SeedAsync();

        var income = await _registrar.DeclareAsync(DeclareIncome(amount: 2530m));
        var expense = await _registrar.DeclareAsync(Declare(amount: 300m));

        Assert.Equal(2530m, income.SignedAmount);
        Assert.Equal(-300m, expense.SignedAmount);
    }

    // 052 RF-17
    [Theory(DisplayName = "Every origin of the closed list may be declared")]
    [InlineData(BudgetIncomeOrigins.Payroll, BudgetIncomeOrigin.Payroll)]
    [InlineData(BudgetIncomeOrigins.SelfEmployment, BudgetIncomeOrigin.SelfEmployment)]
    [InlineData(BudgetIncomeOrigins.Rental, BudgetIncomeOrigin.Rental)]
    [InlineData(BudgetIncomeOrigins.Investment, BudgetIncomeOrigin.Investment)]
    [InlineData(BudgetIncomeOrigins.Benefit, BudgetIncomeOrigin.Benefit)]
    [InlineData(BudgetIncomeOrigins.Refund, BudgetIncomeOrigin.Refund)]
    [InlineData(BudgetIncomeOrigins.Gift, BudgetIncomeOrigin.Gift)]
    [InlineData(BudgetIncomeOrigins.Other, BudgetIncomeOrigin.Other)]
    public async Task every_origin_of_the_closed_list_may_be_declared(
        string requested,
        BudgetIncomeOrigin expected)
    {
        await SeedAsync();

        var declared = await _registrar.DeclareAsync(DeclareIncome(origin: requested));

        Assert.Equal(requested, declared.Origin);
        Assert.Equal(expected, (await _database.Context.BudgetLines.SingleAsync()).Origin);
    }

    // 052 RF-17: the list is closed, so anything outside it is not an origin the household invents
    [Fact(DisplayName = "An origin outside the closed list is refused")]
    public async Task an_origin_outside_the_closed_list_is_refused()
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(DeclareIncome(origin: "lottery")));

        Assert.Equal("Ese origen de ingreso no existe.", Assert.Single(error.Errors["origin"]));
        Assert.Empty(await _database.Context.BudgetLines.ToListAsync());
    }

    // 052 RF-19
    [Fact(DisplayName = "An income line with no origin is refused")]
    public async Task an_income_line_with_no_origin_is_refused()
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(DeclareIncome(origin: null)));

        Assert.Equal("Elige el origen del ingreso.", Assert.Single(error.Errors["origin"]));
        Assert.Empty(await _database.Context.BudgetLines.ToListAsync());
    }

    // 052 RF-20: a different rule from RF-19, with a message of its own
    [Fact(DisplayName = "An expense line declaring an origin is refused")]
    public async Task an_expense_line_declaring_an_origin_is_refused()
    {
        await SeedAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(
                Declare(amount: 300m) with { Origin = BudgetIncomeOrigins.Payroll }));

        Assert.Equal(
            "Una línea de gasto no lleva origen: el origen es del ingreso.",
            Assert.Single(error.Errors["origin"]));

        Assert.Empty(await _database.Context.BudgetLines.ToListAsync());
    }

    // 052 edge case: a one-off income belongs to its month and is not dragged along by itself
    [Fact(DisplayName = "A one-off income belongs to the month it was declared for")]
    public async Task a_one_off_income_belongs_to_the_month_it_was_declared_for()
    {
        await SeedAsync();

        await _registrar.DeclareAsync(
            DeclareIncome(month: new DateOnly(2026, 12, 1), amount: 1800m));

        var lines = await _database.Context.BudgetLines.ToListAsync();

        Assert.Equal(new DateOnly(2026, 12, 1), Assert.Single(lines).PeriodStart);
    }

    private DeclareBudgetLineRequest Declare(
        DateOnly? month = null,
        decimal amount = 300m,
        string mode = BudgetAmountModes.Estimated) =>
        new(_groceries.PublicId, _account.PublicId, month ?? ThisMonth, amount, mode);

    private DeclareBudgetLineRequest DeclareIncome(
        DateOnly? month = null,
        decimal amount = 2530m,
        string mode = BudgetAmountModes.Estimated,
        string? origin = BudgetIncomeOrigins.Payroll) =>
        new(
            _salary.PublicId,
            _account.PublicId,
            month ?? ThisMonth,
            amount,
            mode,
            BudgetLineSigns.Income,
            origin);

    private async Task SeedAsync(CurrencyCode? currency = null)
    {
        _account = Account.Create(
            HouseholdId, "Cuenta conjunta", AccountType.Checking, currency ?? CurrencyCode.Euro);

        _groceries = Category.Create(HouseholdId, "Supermercado", CategoryKind.Expense, 2);
        _salary = Category.Create(HouseholdId, "Nómina", CategoryKind.Income, 4);

        _database.Context.Accounts.Add(_account);
        _database.Context.Categories.Add(_groceries);
        _database.Context.Categories.Add(_salary);

        await _database.Context.SaveChangesAsync();
    }

    private BudgetLineRegistrar NewRegistrar() => new(
        _database.Context,
        new TestTenantContext(HouseholdId),
        new DeclareBudgetLineRequestValidator(Clock));
}
