using MyHome.Api.Interfaces.Accounts;
using MyHome.Modules.Ledger.Application;

namespace MyHome.Api.Services.Accounts;

internal sealed class AccountService(IAccountDirectory accounts) : IAccountService
{
    public async Task<IResult> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var result = await accounts.ListRealAccountsAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }
}
