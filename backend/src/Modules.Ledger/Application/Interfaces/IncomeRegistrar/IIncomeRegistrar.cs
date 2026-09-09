using MyHome.Modules.Ledger.Contracts.Incomes;

namespace MyHome.Modules.Ledger.Application;

public interface IIncomeRegistrar
{
    Task<RegisteredIncome> RegisterAsync(
        RegisterIncomeRequest request,
        CancellationToken cancellationToken = default);
}
