using MyHome.Modules.Ledger.Contracts.Expenses;

namespace MyHome.Api.Interfaces.Expenses;

public interface IExpenseService
{
    Task<IResult> RegisterExpenseAsync(
        RegisterExpenseRequest request,
        CancellationToken cancellationToken);
}
