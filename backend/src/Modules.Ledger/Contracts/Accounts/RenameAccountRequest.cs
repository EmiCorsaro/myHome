namespace MyHome.Modules.Ledger.Contracts.Accounts;

/// <summary>
/// What a member of the household says when renaming an account (story 005, RF-1, RF-2).
/// </summary>
/// <param name="Name">The new name, as typed.</param>
public sealed record RenameAccountRequest(string Name);
