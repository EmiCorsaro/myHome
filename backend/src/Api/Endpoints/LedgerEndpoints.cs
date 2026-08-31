using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Application.Interfaces.IncomeRegister;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Contracts.Income;

namespace MyHome.Api.Endpoints;

public static class LedgerEndpoints
{
    public static IEndpointRouteBuilder MapLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/dashboard", GetDashboardAsync)
            .WithName("GetDashboard")
            .WithTags("Dashboard")
            .WithSummary("Returns the landing screen's figures for one month.")
            .Produces<DashboardSummary>();

        var ledger = app.MapGroup("/api").WithTags("Ledger");

        ledger.MapGet("/accounts", GetAccountsAsync)
            .WithName("GetAccounts")
            .WithSummary("Lists the accounts holding real money, with their balances.")
            .Produces<IReadOnlyList<AccountSummary>>();

        ledger.MapGet("/categories/expense", GetExpenseCategoriesAsync)
            .WithName("GetExpenseCategories")
            .WithSummary("Lists the expense categories.")
            .Produces<IReadOnlyList<CategorySummary>>();

        ledger.MapPost("/expenses", RegisterExpenseAsync)
            .WithName("RegisterExpense")
            .WithSummary("Records an expense.")
            .Produces<RegisteredExpense>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        ledger.MapGet("/categories/income", GetIncomeCategoriesAsync)
            .WithName("GetIncomeCategories")
            .WithSummary("Lists the income categories.")
            .Produces<IReadOnlyList<CategorySummary>>();

        ledger.MapPost("/income", RegisterIncomeAsync)
            .WithName("RegisterIncome")
            .WithSummary("Records an income.")
            .Produces<RegisteredIncome>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> GetDashboardAsync(
        IDashboardQuery dashboard,
        CancellationToken cancellationToken,
        DateOnly? month = null)
    {
        var summary = await dashboard
            .GetMonthlySummaryAsync(month, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(summary);
    }

    private static async Task<IResult> GetAccountsAsync(
        IAccountDirectory accounts,
        CancellationToken cancellationToken)
    {
        var result = await accounts
            .ListRealAccountsAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetExpenseCategoriesAsync(
        ICategoryDirectory categories,
        CancellationToken cancellationToken)
    {
        var result = await categories
            .ListExpenseCategoriesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> RegisterExpenseAsync(
        RegisterExpenseRequest request,
        IExpenseRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var expense = await registrar
            .RegisterAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return expense.WasAlreadyRegistered
            ? Results.Ok(expense)
            : Results.Created($"/api/expenses/{expense.Id}", expense);
    }

    private static async Task<IResult> GetIncomeCategoriesAsync(
        ICategoryDirectory categories,
        CancellationToken cancellationToken)
    {
        var result = await categories.ListIncomeCategoriesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> RegisterIncomeAsync(
        RegisterIncomeRequest request,
        IIncomeRegister registrar,
        CancellationToken cancellationToken)
    {
        var income = await registrar
            .RegisterAsync(request, cancellationToken)
            .ConfigureAwait(false);

        // 200 and not 201 when the key had already been used: nothing was created this time.
        return income.WasAlreadyRegistered
            ? Results.Ok(income)
            : Results.Created($"/api/income/{income.Id}", income);
    }
}
