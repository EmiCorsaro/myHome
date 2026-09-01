using MyHome.Api.Interfaces.Expenses;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Expenses;

namespace MyHome.Api.Services.Expenses;

internal sealed class ExpenseService(IExpenseRegistrar registrar) : IExpenseService
{
    public async Task<IResult> RegisterExpenseAsync(
        RegisterExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var expense = await registrar
            .RegisterAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return expense.WasAlreadyRegistered
            ? Results.Ok(expense)
            : Results.Created($"/api/expenses/{expense.Id}", expense);
    }
}
