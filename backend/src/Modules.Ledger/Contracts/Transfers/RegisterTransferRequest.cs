namespace MyHome.Modules.Ledger.Contracts.Transfers;

public sealed record RegisterTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateOnly OccurredOn,
    string? Description = null,
    Guid? CategoryId = null,
    string? ClientMutationId = null);

public sealed record RegisteredTransfer(
    Guid Id,
    DateOnly OccurredOn,
    string Description,
    decimal Amount,
    string Currency,
    string FromAccountName,
    string ToAccountName,
    string? CategoryName,
    int? CategoryColorIndex,
    bool WasAlreadyRegistered);
