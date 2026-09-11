namespace MyHome.Modules.Ledger.Contracts.Accounts;

/// <summary>
/// What a member of the household says when changing an account's type (story 005, RF-3, RF-11).
/// </summary>
/// <param name="Type">
/// One of the four declarable types: <c>checking</c>, <c>savings</c>, <c>cash</c> or
/// <c>creditCard</c>. Whether the requested transition is actually allowed is decided afterwards:
/// a credit card can never be reached from, or left towards, any other type (RF-11).
/// </param>
public sealed record ChangeAccountTypeRequest(string Type);
