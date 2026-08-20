namespace MyHome.Modules.Ledger.Contracts.Accounts;

/// <summary>
/// An account as the outside world sees it: enough to list it, pick it and read its balance.
/// </summary>
/// <param name="Id">Account identifier.</param>
/// <param name="Name">Visible name.</param>
/// <param name="Type">Kind of account, lower-cased: <c>checking</c>, <c>savings</c>, <c>cash</c>,
/// <c>creditCard</c>, <c>income</c>, <c>expense</c>.</param>
/// <param name="Currency">Three-letter ISO 4217 code.</param>
/// <param name="Balance">
/// Current balance, in major currency units. Summed from the postings on read; there is no stored
/// balance that could drift from them.
/// </param>
/// <param name="IsTracked">Whether the account takes part in balance projection.</param>
/// <param name="MinimumBufferTarget">
/// Balance below which the account is considered at risk, or <see langword="null"/> if no floor
/// has been set.
/// </param>
public sealed record AccountSummary(
    Guid Id,
    string Name,
    string Type,
    string Currency,
    decimal Balance,
    bool IsTracked,
    decimal? MinimumBufferTarget);
