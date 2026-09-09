namespace MyHome.Modules.Ledger.Contracts.Accounts;

/// <summary>
/// What a member of the household says when correcting an opening balance already declared.
/// </summary>
/// <param name="Amount">The new amount, or null to keep the one declared.</param>
/// <param name="OccurredOn">The new date, or null to keep the one declared.</param>
/// <remarks>
/// Both are optional, and at least one is expected: an edit may change the amount, the date, or
/// both. It is a different operation from declaring, with different rules, and that is why it is a
/// different contract: this one only applies to an account that already has an opening balance.
/// </remarks>
public sealed record AmendOpeningBalanceRequest(decimal? Amount, DateOnly? OccurredOn);
