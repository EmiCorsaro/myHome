using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Contracts.Expenses;

namespace MyHome.Api.Endpoints;

/// <summary>
/// Ledger endpoints: dashboard, accounts, categories and expense registration.
/// </summary>
/// <remarks>
/// Each endpoint takes the request, calls one service, maps the result to a status code and
/// returns. No domain conditionals, no totals, no database.
/// <para>
/// Validation errors are not handled here either: the service throws and one exception handler
/// turns that into a 400 with the errors per field. Doing it endpoint by endpoint ends with a
/// dozen slightly different error shapes.
/// </para>
/// </remarks>
public static class LedgerEndpoints
{
    /// <summary>
    /// Registers the ledger endpoints.
    /// </summary>
    /// <param name="app">The application's route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <example>
    /// <code>
    /// app.MapLedgerEndpoints();
    /// </code>
    /// </example>
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

        // 200 and not 201 when the key had already been used: nothing was created this time.
        return expense.WasAlreadyRegistered
            ? Results.Ok(expense)
            : Results.Created($"/api/expenses/{expense.Id}", expense);
    }
}
