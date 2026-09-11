namespace MyHome.Modules.Ledger.Contracts.Incomes;

public sealed record RegisterIncomeRequest(
    Guid AccountId,
    Guid CategoryId,
    decimal Amount,
    DateOnly OccurredOn,
    string? Description = null,
    Guid? MemberId = null,
    string? ClientMutationId = null);

public sealed record RegisteredIncome(
    Guid Id,
    DateOnly OccurredOn,
    string Description,
    decimal Amount,
    string Currency,
    string AccountName,
    string CategoryName,
    int CategoryColorIndex,
    bool WasAlreadyRegistered);
