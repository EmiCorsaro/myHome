using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Expenses;

namespace MyHome.Modules.Ledger.Application;

public interface IExpenseRegistrar
{
    Task<RegisteredExpense> RegisterAsync(
        RegisterExpenseRequest request,
        CancellationToken cancellationToken = default);
}
