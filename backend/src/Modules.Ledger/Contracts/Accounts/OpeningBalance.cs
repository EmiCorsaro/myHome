namespace MyHome.Modules.Ledger.Contracts.Accounts;

/// <summary>
/// The opening balance of an account, as the household sees it after declaring or editing it.
/// </summary>
/// <param name="AccountId">Public identifier of the account.</param>
/// <param name="AccountName">Name of the account, to say it back to whoever asked.</param>
/// <param name="Currency">Currency the account works in.</param>
/// <param name="Amount">The opening balance in force.</param>
/// <param name="OccurredOn">The date it is stated at.</param>
/// <param name="AccountBalance">
/// The balance of the account once the opening balance and every movement recorded against it are
/// added up. Equal to <paramref name="Amount"/> while the account has no movements.
/// </param>
public sealed record OpeningBalance(
    Guid AccountId,
    string AccountName,
    string Currency,
    decimal Amount,
    DateOnly OccurredOn,
    decimal AccountBalance);
