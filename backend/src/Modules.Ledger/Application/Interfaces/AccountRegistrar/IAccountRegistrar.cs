using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Application;

public interface IAccountRegistrar
{
    Task<AccountSummary> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default);
}
