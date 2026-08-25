using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Application;

public interface IAccountDirectory
{
    Task<IReadOnlyList<AccountSummary>> ListRealAccountsAsync(
        CancellationToken cancellationToken = default);
}
