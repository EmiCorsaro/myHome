namespace MyHome.Modules.Ledger.Contracts.Accounts;

public sealed record AccountSummary(
    Guid Id,
    string Name,
    string Type,
    string Currency,
    decimal Balance,
    bool IsTracked,
    decimal? MinimumBufferTarget);
