namespace MyHome.Modules.Ledger.Contracts.Accounts;

/// <summary>Fixes or retires an account's minimum buffer (story 004, RF-5, RF-6, RF-7, RF-13).</summary>
/// <param name="Amount">
/// The liquidity floor to alert below, zero included (RF-6); <c>null</c> retires it (RF-13).
/// </param>
public sealed record SetMinimumBufferTargetRequest(decimal? Amount);
