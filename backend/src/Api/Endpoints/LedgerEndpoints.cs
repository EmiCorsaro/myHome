using MyHome.Api.Interfaces.Accounts;
using MyHome.Api.Interfaces.Categories;
using MyHome.Api.Interfaces.Dashboard;
using MyHome.Api.Interfaces.Expenses;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Contracts.Expenses;

namespace MyHome.Api.Endpoints;

public static class LedgerEndpoints
{
    public static IEndpointRouteBuilder MapLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/dashboard", (IDashboardService dashboard, CancellationToken cancellationToken, DateOnly? month = null) => dashboard.GetDashboardAsync(month, cancellationToken))
            .WithName("GetDashboard")
            .WithTags("Dashboard")
            .WithSummary("Returns the landing screen's figures for one month.")
            .Produces<DashboardSummary>();

        var ledger = app.MapGroup("/api").WithTags("Ledger");

        ledger.MapGet("/accounts", (IAccountService accounts, CancellationToken cancellationToken) => accounts.GetAccountsAsync(cancellationToken))
            .WithName("GetAccounts")
            .WithSummary("Lists the accounts holding real money, with their balances.")
            .Produces<IReadOnlyList<AccountSummary>>();

        ledger.MapGet("/categories/expense", (ICategoryService categories, CancellationToken cancellationToken) => categories.GetExpenseCategoriesAsync(cancellationToken))
            .WithName("GetExpenseCategories")
            .WithSummary("Lists the expense categories.")
            .Produces<IReadOnlyList<CategorySummary>>();

        ledger.MapPost("/expenses", (RegisterExpenseRequest request, IExpenseService expenses, CancellationToken cancellationToken) => expenses.RegisterExpenseAsync(request, cancellationToken))
            .WithName("RegisterExpense")
            .WithSummary("Records an expense.")
            .Produces<RegisteredExpense>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return app;
    }
}
