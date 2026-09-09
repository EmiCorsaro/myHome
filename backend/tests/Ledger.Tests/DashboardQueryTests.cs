using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Contracts.Transfers;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// How the monthly dashboard reports a transfer, as story 011 specifies it (RF-2, RF-3, RF-4,
/// RF-16, RF-18). Every test names the requirement it pins down; the map from RF to test lives in
/// <c>progress/impl_011.md</c>.
/// </summary>
public sealed class DashboardQueryTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 9);

    private readonly LedgerDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // RF-2, RF-3: between two controlled accounts, the transfer is neither income nor expense of
    // the period, and it does not show up in the spend-by-category report.
    [Fact(DisplayName = "A transfer between two controlled accounts is excluded from the reports")]
    public async Task a_transfer_between_two_controlled_accounts_is_excluded_from_the_reports()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        await transfers.RegisterAsync(new RegisterTransferRequest(
            checking.PublicId, savings.PublicId, 200m, Today));

        var summary = await DashboardQueryFor().GetMonthlySummaryAsync(Today);

        Assert.Equal(0m, summary.Income);
        Assert.Equal(0m, summary.Expense);
        Assert.Empty(summary.ByCategory);
    }

    // RF-16: a transfer into an uncontrolled account is classified as an expense, with the
    // category indicated.
    [Fact(DisplayName = "A transfer into an uncontrolled destination appears in the spend-by-category report")]
    public async Task a_transfer_into_an_uncontrolled_destination_appears_in_the_spend_by_category_report()
    {
        var checking = await Existing("Santander conjunta");
        var wallet = await Existing("Efectivo del compañero", AccountType.Cash, isTracked: false);
        var category = await NewExpenseCategory();

        var transfers = TransferRegistrarFor();

        await transfers.RegisterAsync(new RegisterTransferRequest(
            checking.PublicId, wallet.PublicId, 200m, Today, CategoryId: category.PublicId));

        var summary = await DashboardQueryFor().GetMonthlySummaryAsync(Today);

        Assert.Equal(200m, summary.Expense);
        var line = Assert.Single(summary.ByCategory);
        Assert.Equal(category.PublicId, line.CategoryId);
        Assert.Equal(200m, line.Total);
    }

    // RF-18: the destination becoming controlled afterwards does not rewrite a transfer already
    // registered.
    [Fact(DisplayName = "A transfer already registered keeps its category once the destination becomes controlled")]
    public async Task a_transfer_already_registered_keeps_its_category_once_the_destination_becomes_controlled()
    {
        var checking = await Existing("Santander conjunta");
        var wallet = await Existing("Efectivo del compañero", AccountType.Cash, isTracked: false);
        var category = await NewExpenseCategory();

        var transfers = TransferRegistrarFor();

        await transfers.RegisterAsync(new RegisterTransferRequest(
            checking.PublicId, wallet.PublicId, 200m, Today, CategoryId: category.PublicId));

        wallet.SetTracked(true);
        await _database.Context.SaveChangesAsync();

        var summary = await DashboardQueryFor().GetMonthlySummaryAsync(Today);

        Assert.Equal(200m, summary.Expense);
        var line = Assert.Single(summary.ByCategory);
        Assert.Equal(category.PublicId, line.CategoryId);
        Assert.Equal(200m, line.Total);
    }

    // Story 013, RF-4: a voided movement and its reversal are excluded from the spend-by-category
    // report, not merely netted to zero inside it.
    [Fact(DisplayName = "A voided expense and its reversal are excluded from the spend-by-category report")]
    public async Task a_voided_expense_and_its_reversal_are_excluded_from_the_spend_by_category_report()
    {
        var account = await Existing("Santander conjunta");
        var groceries = await NewExpenseCategory();
        var other = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var voided = await expenses.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, groceries.PublicId, 42.35m, Today));
        await expenses.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, other.PublicId, 10m, Today));

        await MovementLifecycleRegistrarFor().VoidAsync(voided.Id);

        var summary = await DashboardQueryFor().GetMonthlySummaryAsync(Today);

        Assert.Equal(10m, summary.Expense);
        var line = Assert.Single(summary.ByCategory);
        Assert.Equal(other.PublicId, line.CategoryId);
        Assert.DoesNotContain(summary.ByCategory, c => c.CategoryId == groceries.PublicId);
    }

    private ExpenseRegistrar ExpenseRegistrarFor() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new RegisterExpenseRequestValidator(),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private MovementLifecycleRegistrar MovementLifecycleRegistrarFor() =>
        new(_database.Context, new TestTenantContext(HouseholdId), TimeProvider.System);

    private TransferRegistrar TransferRegistrarFor() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new RegisterTransferRequestValidator());

    private DashboardQuery DashboardQueryFor() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private async Task<Account> Existing(
        string name,
        AccountType type = AccountType.Checking,
        bool isTracked = true)
    {
        var account = Account.Create(HouseholdId, name, type, CurrencyCode.Euro, isTracked);

        _database.Context.Accounts.Add(account);
        await _database.Context.SaveChangesAsync();

        return account;
    }

    private async Task<Category> NewExpenseCategory()
    {
        var category = Category.Create(
            HouseholdId, $"Gasto {Guid.CreateVersion7()}", CategoryKind.Expense, colorIndex: 1);

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return category;
    }
}
