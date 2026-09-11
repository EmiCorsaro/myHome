using MyHome.Modules.Ledger.Contracts.Budget;

namespace MyHome.Modules.Ledger.Application;

/// <summary>Reading the budget the household has declared for a month.</summary>
public interface IBudgetDirectory
{
    /// <summary>Lists the budget lines of one month with the total they commit.</summary>
    /// <param name="month">Any day of the month to read. Defaults to the month in progress.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The month's lines and their total. Empty and zero when nothing was declared.</returns>
    Task<MonthBudget> GetMonthAsync(
        DateOnly? month = null,
        CancellationToken cancellationToken = default);
}
