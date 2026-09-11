using MyHome.Modules.Ledger.Contracts.Budget;

namespace MyHome.Modules.Ledger.Application;

/// <summary>Declaring what the household expects to spend in a category during a month.</summary>
public interface IBudgetLineRegistrar
{
    /// <summary>Declares a budget line without a calendar.</summary>
    /// <param name="request">Category, account, month, amount and mode.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The line as it was stored.</returns>
    Task<BudgetLineSummary> DeclareAsync(
        DeclareBudgetLineRequest request,
        CancellationToken cancellationToken = default);
}
