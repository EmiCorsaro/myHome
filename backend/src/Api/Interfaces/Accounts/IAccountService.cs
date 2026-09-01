namespace MyHome.Api.Interfaces.Accounts;

public interface IAccountService
{
    Task<IResult> GetAccountsAsync(CancellationToken cancellationToken);
}
