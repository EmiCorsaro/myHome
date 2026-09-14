using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Contracts.Budget;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Contracts.Incomes;
using MyHome.Modules.Ledger.Contracts.Movements;
using MyHome.Modules.Ledger.Contracts.Transfers;

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

        ledger.MapPost("/accounts", CreateAccountAsync)
            .WithName("CreateAccount")
            .WithSummary("Opens an account for the household.")
            .Produces<AccountSummary>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        // Declaring and editing the opening balance are two operations, not one: the first is
        // refused once the account has an opening balance, the second only works when it has one.
        ledger.MapPost("/accounts/{accountId:guid}/opening-balance", DeclareOpeningBalanceAsync)
            .WithName("DeclareOpeningBalance")
            .WithSummary("States, once, with how much money an account starts.")
            .Produces<OpeningBalance>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        ledger.MapPut("/accounts/{accountId:guid}/opening-balance", AmendOpeningBalanceAsync)
            .WithName("AmendOpeningBalance")
            .WithSummary("Corrects the opening balance already declared for an account.")
            .Produces<OpeningBalance>()
            .ProducesValidationProblem();

        // Marking an account as controlled and fixing its minimum buffer are two independent
        // settings (story 004): each is refused on its own terms, so each gets its own endpoint.
        ledger.MapPut("/accounts/{accountId:guid}/tracked", SetAccountTrackedAsync)
            .WithName("SetAccountTracked")
            .WithSummary("Marks an account as controlled or not.")
            .Produces<AccountSummary>()
            .ProducesValidationProblem();

        ledger.MapPut("/accounts/{accountId:guid}/minimum-buffer", SetMinimumBufferTargetAsync)
            .WithName("SetMinimumBufferTarget")
            .WithSummary("Fixes or retires an account's minimum buffer.")
            .Produces<AccountSummary>()
            .ProducesValidationProblem();

        // Renaming, changing type, archiving and unarchiving are four independent operations
        // (story 005): each is refused on its own terms, so each gets its own endpoint.
        ledger.MapPut("/accounts/{accountId:guid}/name", RenameAccountAsync)
            .WithName("RenameAccount")
            .WithSummary("Renames an account.")
            .Produces<AccountSummary>()
            .ProducesValidationProblem();

        ledger.MapPut("/accounts/{accountId:guid}/type", ChangeAccountTypeAsync)
            .WithName("ChangeAccountType")
            .WithSummary("Changes an account's type.")
            .Produces<AccountSummary>()
            .ProducesValidationProblem();

        ledger.MapPost("/accounts/{accountId:guid}/archive", ArchiveAccountAsync)
            .WithName("ArchiveAccount")
            .WithSummary("Retires an account from circulation, keeping its history intact.")
            .Produces<AccountSummary>()
            .ProducesValidationProblem();

        ledger.MapPost("/accounts/{accountId:guid}/unarchive", UnarchiveAccountAsync)
            .WithName("UnarchiveAccount")
            .WithSummary("Restores an archived account so it can record movements again.")
            .Produces<AccountSummary>()
            .ProducesValidationProblem();

        ledger.MapGet("/categories/expense", GetExpenseCategoriesAsync)
            .WithName("GetExpenseCategories")
            .WithSummary("Lists the expense categories.")
            .Produces<IReadOnlyList<CategorySummary>>();

        ledger.MapGet("/categories/income", GetIncomeCategoriesAsync)
            .WithName("GetIncomeCategories")
            .WithSummary("Lists the income categories.")
            .Produces<IReadOnlyList<CategorySummary>>();

        ledger.MapPost("/categories", CreateCategoryAsync)
            .WithName("CreateCategory")
            .WithSummary("Creates a category.")
            .Produces<CategorySummary>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        ledger.MapGet("/budget", GetMonthBudgetAsync)
            .WithName("GetMonthBudget")
            .WithSummary("Lists the budget lines of one month with what they commit and expect.")
            .Produces<MonthBudget>();

        ledger.MapPost("/budget/lines", DeclareBudgetLineAsync)
            .WithName("DeclareBudgetLine")
            .WithSummary("Declares what the household expects to spend or receive in a category that month.")
            .Produces<BudgetLineSummary>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        ledger.MapPost("/expenses", RegisterExpenseAsync)
            .WithName("RegisterExpense")
            .WithSummary("Records an expense.")
            .Produces<RegisteredExpense>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        ledger.MapPost("/incomes", RegisterIncomeAsync)
            .WithName("RegisterIncome")
            .WithSummary("Records an income.")
            .Produces<RegisteredIncome>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        ledger.MapPost("/transfers", RegisterTransferAsync)
            .WithName("RegisterTransfer")
            .WithSummary("Moves money between two of the household's own accounts.")
            .Produces<RegisteredTransfer>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        ledger.MapPost("/movements/{movementId:guid}/void", VoidMovementAsync)
            .WithName("VoidMovement")
            .WithSummary("Reverses a movement, leaving the balances it touched as they were before it.")
            .Produces<VoidedMovement>()
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

    private static async Task<IResult> CreateAccountAsync(
        CreateAccountRequest request,
        IAccountRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var account = await registrar
            .CreateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created($"/api/accounts/{account.Id}", account);
    }

    private static async Task<IResult> DeclareOpeningBalanceAsync(
        Guid accountId,
        DeclareOpeningBalanceRequest request,
        IOpeningBalanceRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var opening = await registrar
            .DeclareAsync(accountId, request, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created($"/api/accounts/{opening.AccountId}/opening-balance", opening);
    }

    private static async Task<IResult> AmendOpeningBalanceAsync(
        Guid accountId,
        AmendOpeningBalanceRequest request,
        IOpeningBalanceRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var opening = await registrar
            .AmendAsync(accountId, request, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(opening);
    }

    private static async Task<IResult> SetAccountTrackedAsync(
        Guid accountId,
        SetAccountTrackedRequest request,
        IAccountControlRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var account = await registrar
            .SetTrackedAsync(accountId, request, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(account);
    }

    private static async Task<IResult> SetMinimumBufferTargetAsync(
        Guid accountId,
        SetMinimumBufferTargetRequest request,
        IAccountControlRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var account = await registrar
            .SetMinimumBufferTargetAsync(accountId, request, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(account);
    }

    private static async Task<IResult> RenameAccountAsync(
        Guid accountId,
        RenameAccountRequest request,
        IAccountLifecycleRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var account = await registrar
            .RenameAsync(accountId, request, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(account);
    }

    private static async Task<IResult> ChangeAccountTypeAsync(
        Guid accountId,
        ChangeAccountTypeRequest request,
        IAccountLifecycleRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var account = await registrar
            .ChangeTypeAsync(accountId, request, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(account);
    }

    private static async Task<IResult> ArchiveAccountAsync(
        Guid accountId,
        IAccountLifecycleRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var account = await registrar
            .ArchiveAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(account);
    }

    private static async Task<IResult> UnarchiveAccountAsync(
        Guid accountId,
        IAccountLifecycleRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var account = await registrar
            .UnarchiveAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(account);
    }

    private static async Task<IResult> GetExpenseCategoriesAsync(
        ICategoryDirectory categories,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var result = await categories
            .ListExpenseCategoriesAsync(includeArchived, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetIncomeCategoriesAsync(
        ICategoryDirectory categories,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var result = await categories
            .ListIncomeCategoriesAsync(includeArchived, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateCategoryAsync(
        CreateCategoryRequest request,
        ICategoryRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var category = await registrar
            .CreateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created($"/api/categories/{category.Id}", category);
    }

    private static async Task<IResult> GetMonthBudgetAsync(
        IBudgetDirectory budget,
        CancellationToken cancellationToken,
        DateOnly? month = null)
    {
        var result = await budget.GetMonthAsync(month, cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> DeclareBudgetLineAsync(
        DeclareBudgetLineRequest request,
        IBudgetLineRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var line = await registrar.DeclareAsync(request, cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/budget/lines/{line.Id}", line);
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

    private static async Task<IResult> RegisterIncomeAsync(
        RegisterIncomeRequest request,
        IIncomeRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var income = await registrar
            .RegisterAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return income.WasAlreadyRegistered
            ? Results.Ok(income)
            : Results.Created($"/api/incomes/{income.Id}", income);
    }

    private static async Task<IResult> RegisterTransferAsync(
        RegisterTransferRequest request,
        ITransferRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var transfer = await registrar
            .RegisterAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return transfer.WasAlreadyRegistered
            ? Results.Ok(transfer)
            : Results.Created($"/api/transfers/{transfer.Id}", transfer);
    }

    private static async Task<IResult> VoidMovementAsync(
        Guid movementId,
        IMovementLifecycleRegistrar registrar,
        CancellationToken cancellationToken)
    {
        var voided = await registrar
            .VoidAsync(movementId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(voided);
    }
}
