namespace MyHome.Modules.Ledger.Contracts.Accounts;

/// <summary>Marks an account as controlled or not (story 004, RF-1, RF-2, RF-9, RF-12).</summary>
/// <param name="IsTracked">
/// <c>true</c> to fold the account back into the disponible real and the projection; <c>false</c>
/// to exclude it while it keeps recording its own movements.
/// </param>
public sealed record SetAccountTrackedRequest(bool IsTracked);
