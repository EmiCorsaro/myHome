namespace MyHome.Modules.Ledger.Contracts.Accounts;

/// <summary>
/// What a member of the household says when declaring, once, with how much money an account starts.
/// </summary>
/// <param name="Amount">
/// The money the account holds at <paramref name="OccurredOn"/>, in the account's own currency.
/// Negative states debt, which is what a credit card starts with; zero is a valid statement.
/// </param>
/// <param name="OccurredOn">The date the balance is stated at. Never in the future.</param>
public sealed record DeclareOpeningBalanceRequest(decimal Amount, DateOnly OccurredOn);
