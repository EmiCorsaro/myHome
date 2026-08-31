using MyHome.Modules.Ledger.Contracts.Income;

namespace MyHome.Modules.Ledger.Application.Interfaces.IncomeRegister;

public interface IIncomeRegister
{
    Task<RegisteredIncome> RegisterAsync(
        RegisterIncomeRequest request,
        CancellationToken cancellationToken = default);
}
