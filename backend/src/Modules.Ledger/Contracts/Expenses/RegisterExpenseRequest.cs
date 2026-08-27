namespace MyHome.Modules.Ledger.Contracts.Expenses;

public enum ExpenseRecurrence
{
    Once = 0,

    Monthly = 1,

    BiMonthly = 2,

    Quarterly = 3,
}

public sealed record RegisterExpenseRequest(
    Guid AccountId,
    Guid CategoryId,
    decimal Amount,
    DateOnly OccurredOn,
    string Description,
    Guid? MemberId = null,
    ExpenseRecurrence Recurrence = ExpenseRecurrence.Once,
    string? ClientMutationId = null);

public sealed record RegisteredExpense(
    Guid Id,
    DateOnly OccurredOn,
    string Description,
    decimal Amount,
    string Currency,
    string AccountName,
    string CategoryName,
    int CategoryColorIndex,
    ExpenseRecurrence Recurrence,
    bool WasAlreadyRegistered);
