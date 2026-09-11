using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Contracts.Incomes;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using MyHome.Modules.Ledger.Contracts.Transfers;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

public sealed class RealMovementQueryTests : IDisposable
{
    private static readonly DateOnly Early = new(2026, 9, 1);
    private static readonly DateOnly Middle = new(2026, 9, 9);
    private static readonly TimeProvider Clock =
        new FixedTimeProvider(new DateTimeOffset(Middle, new TimeOnly(10, 0), TimeSpan.Zero));

    private readonly LedgerDatabase database = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task lists_only_the_requested_category_nature_inclusive_of_date_bounds()
    {
        var account = await Existing("Checking");
        var incomeCategory = await NewCategory("Salary", CategoryKind.Income);
        var expenseCategory = await NewCategory("Food", CategoryKind.Expense);
        var incomes = IncomeRegistrarFor();
        var expenses = ExpenseRegistrarFor();

        await incomes.RegisterAsync(new RegisterIncomeRequest(account.PublicId, incomeCategory.PublicId, 100m, Early));
        await expenses.RegisterAsync(new RegisterExpenseRequest(account.PublicId, expenseCategory.PublicId, 20m, Middle));

        var result = await Query().ListRealMovementsAsync(CategoryNature.Income, Early, Early);

        var line = Assert.Single(result);
        Assert.Equal(incomeCategory.Name, line.CategoryName);
        Assert.Equal(100m, line.Amount);
        Assert.Equal(Early, line.OccurredOn);
    }

    [Fact]
    public async Task projects_all_contract_fields_and_orders_same_day_entries_by_creation_time()
    {
        var account = await Existing("Checking");
        var category = await NewCategory("Salary", CategoryKind.Income, colorIndex: 4);
        var incomes = IncomeRegistrarFor();
        var first = await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 12m, Middle, "first"));
        var second = await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 67m, Middle, "second"));

        var result = await Query().ListRealMovementsAsync(CategoryNature.Income);

        Assert.Equal(2, result.Count);
        Assert.Equal(second.Id, result[0].Id);
        Assert.Equal(first.Id, result[1].Id);
        Assert.Equal("second", result[0].Description);
        Assert.Equal("income", result[0].Kind);
        Assert.Equal(67m, result[0].Amount);
        Assert.Equal(account.Name, result[0].AccountName);
        Assert.Equal(category.Name, result[0].CategoryName);
        Assert.Equal(4, result[0].CategoryColorIndex);
        Assert.False(result[0].IsRecurring);
    }

    [Fact]
    public async Task projects_a_real_entry_with_multiple_postings_using_the_complete_contract()
    {
        var account = await Existing("Checking");
        var incomeAccount = await Existing("Income", AccountType.Income);
        var category = await NewCategory("Salary", CategoryKind.Income, colorIndex: 6);
        var entry = JournalEntry.RegisterIncome(
            HouseholdId,
            Middle,
            "multi-posting income",
            account,
            incomeAccount,
            category,
            Euros(42m));
        database.Context.Entries.Add(entry);
        await database.Context.SaveChangesAsync();

        var line = Assert.Single(await Query().ListRealMovementsAsync(CategoryNature.Income));

        Assert.Equal(entry.PublicId, line.Id);
        Assert.Equal(Middle, line.OccurredOn);
        Assert.Equal("multi-posting income", line.Description);
        Assert.Equal("income", line.Kind);
        Assert.Equal(42m, line.Amount);
        Assert.Equal(account.Name, line.AccountName);
        Assert.Equal(category.Name, line.CategoryName);
        Assert.Equal(6, line.CategoryColorIndex);
        Assert.False(line.IsRecurring);
    }

    [Fact]
    public async Task includes_materialized_recurring_entries_but_not_planned_occurrences()
    {
        var account = await Existing("Checking");
        var category = await NewCategory("Subscription", CategoryKind.Expense);
        var materialized = await ExpenseRegistrarFor().RegisterAsync(new RegisterExpenseRequest(
            account.PublicId,
            category.PublicId,
            100m,
            Middle,
            "materialized",
            Recurrence: ExpenseRecurrence.Monthly));

        var result = await Query().ListRealMovementsAsync(CategoryNature.Expense);

        var line = Assert.Single(result);
        Assert.Equal(materialized.Id, line.Id);
        Assert.True(line.IsRecurring);
    }

    [Fact]
    public async Task excludes_reversals_and_classifies_by_category_nature_not_amount_sign()
    {
        var account = await Existing("Checking");
        var incomeCategory = await NewCategory("Refund", CategoryKind.Income);
        var expenseCategory = await NewCategory("Positive expense", CategoryKind.Expense);
        var income = IncomeRegistrarFor();
        var expenses = ExpenseRegistrarFor();
        var incomeEntry = await income.RegisterAsync(
            new RegisterIncomeRequest(account.PublicId, incomeCategory.PublicId, 5m, Middle));
        await expenses.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, expenseCategory.PublicId, 5m, Middle));
        await MovementLifecycleRegistrarFor().VoidAsync(incomeEntry.Id);

        Assert.Empty(await Query().ListRealMovementsAsync(CategoryNature.Income));
        var expenseResult = await Query().ListRealMovementsAsync(CategoryNature.Expense);
        Assert.Single(expenseResult);
        Assert.Equal(expenseCategory.Name, expenseResult[0].CategoryName);
    }

    [Fact]
    public async Task supports_each_optional_bound_and_returns_empty_when_no_entry_matches()
    {
        var account = await Existing("Checking");
        var category = await NewCategory("Food", CategoryKind.Expense);
        var expenses = ExpenseRegistrarFor();
        await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 10m, Early));
        await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 20m, Middle));

        Assert.Single(await Query().ListRealMovementsAsync(CategoryNature.Expense, Early, Early));
        Assert.Single(await Query().ListRealMovementsAsync(CategoryNature.Expense, Middle, null));
        Assert.Single(await Query().ListRealMovementsAsync(CategoryNature.Expense, null, Early));
        Assert.Empty(await Query().ListRealMovementsAsync(
            CategoryNature.Expense, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2)));
    }

    [Fact]
    public async Task excludes_transfers_voided_entries_reversals_and_other_households()
    {
        var account = await Existing("Checking");
        var category = await NewCategory("Food", CategoryKind.Expense);
        var otherAccount = await Existing("Other", AccountType.Checking, householdId: 2);
        var otherExpenseAccount = await Existing("Other expenses", AccountType.Expense, householdId: 2);
        var otherCategory = await NewCategory("Other food", CategoryKind.Expense, householdId: 2);
        var expenses = ExpenseRegistrarFor();

        var kept = await expenses.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 10m, Middle));
        var transferTo = await Existing("Savings", AccountType.Savings);
        await TransferRegistrarFor().RegisterAsync(
            new RegisterTransferRequest(account.PublicId, transferTo.PublicId, 50m, Middle));
        var voided = await expenses.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 20m, Middle));
        await MovementLifecycleRegistrarFor().VoidAsync(voided.Id);
        database.Context.Entries.Add(JournalEntry.RegisterExpense(
            2, Middle, "Other household", otherAccount, otherExpenseAccount,
            category: otherCategory, Euros(30m)));
        await database.Context.SaveChangesAsync();

        var result = await Query().ListRealMovementsAsync(CategoryNature.Expense);

        var line = Assert.Single(result);
        Assert.Equal(kept.Id, line.Id);
        Assert.DoesNotContain(result, movement => movement.Kind == "transfer");
        Assert.Empty(await Query().ListRealMovementsAsync(CategoryNature.Income));
    }

    [Fact]
    public async Task lists_more_than_the_dashboard_limit_and_rejects_an_inverted_range()
    {
        var account = await Existing("Checking");
        var category = await NewCategory("Food", CategoryKind.Expense);
        var expenses = ExpenseRegistrarFor();

        for (var index = 0; index < 201; index++)
        {
            await expenses.RegisterAsync(
                new RegisterExpenseRequest(account.PublicId, category.PublicId, 1m, Middle));
        }

        var result = await Query().ListRealMovementsAsync(CategoryNature.Expense);

        Assert.Equal(201, result.Count);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Query().ListRealMovementsAsync(CategoryNature.Expense, Middle, Early));
    }

    private DashboardQuery Query() =>
        new(database.Context, new TestTenantContext(HouseholdId), new TestHouseholdDirectory(CurrencyCode.Euro));

    private ExpenseRegistrar ExpenseRegistrarFor() =>
        new(database.Context, new TestTenantContext(HouseholdId), new RegisterExpenseRequestValidator(Clock),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private IncomeRegistrar IncomeRegistrarFor() =>
        new(database.Context, new TestTenantContext(HouseholdId), new RegisterIncomeRequestValidator(Clock),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private MovementLifecycleRegistrar MovementLifecycleRegistrarFor() =>
        new(database.Context, new TestTenantContext(HouseholdId), TimeProvider.System);

    private TransferRegistrar TransferRegistrarFor() =>
        new(database.Context, new TestTenantContext(HouseholdId),
            new RegisterTransferRequestValidator(Clock));

    private async Task<Account> Existing(string name, AccountType type = AccountType.Checking, int householdId = HouseholdId)
    {
        var account = Account.Create(householdId, name, type, CurrencyCode.Euro);
        database.Context.Accounts.Add(account);
        await database.Context.SaveChangesAsync();
        return account;
    }

    private async Task<Category> NewCategory(
        string name,
        CategoryKind kind,
        int householdId = HouseholdId,
        int colorIndex = 1)
    {
        var category = Category.Create(householdId, name, kind, colorIndex);
        database.Context.Categories.Add(category);
        await database.Context.SaveChangesAsync();
        return category;
    }
}
