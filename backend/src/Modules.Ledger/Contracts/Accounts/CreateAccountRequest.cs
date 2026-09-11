namespace MyHome.Modules.Ledger.Contracts.Accounts;

/// <summary>
/// What a member of the household says when opening an account.
/// </summary>
/// <param name="Name">Name of the account, as typed.</param>
/// <param name="Type">
/// One of the four types a user may declare: <c>checking</c>, <c>savings</c>, <c>cash</c> or
/// <c>creditCard</c>. The currency is not asked for: the household's own is used.
/// </param>
public sealed record CreateAccountRequest(string Name, string Type);
