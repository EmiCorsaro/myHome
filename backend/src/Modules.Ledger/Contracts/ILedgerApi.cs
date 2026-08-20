using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Contracts.Expenses;

namespace MyHome.Modules.Ledger.Contracts;

/// <summary>
/// Reads the household's accounts.
/// </summary>
/// <remarks>
/// None of these interfaces takes a household identifier: it comes from the request's tenant
/// context inside the implementation. Reading another household's data by passing the wrong
/// argument is not something a caller can express.
/// </remarks>
public interface IAccountDirectory
{
    /// <summary>
    /// Lists the accounts holding real money, in display order, with their current balances.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The accounts, or an empty list if there are none.</returns>
    /// <example>
    /// <code>
    /// var accounts = await directory.ListRealAccountsAsync(cancellationToken);
    /// </code>
    /// </example>
    Task<IReadOnlyList<AccountSummary>> ListRealAccountsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the household's categories.
/// </summary>
public interface ICategoryDirectory
{
    /// <summary>
    /// Lists the expense categories, in display order.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The categories, or an empty list if there are none.</returns>
    Task<IReadOnlyList<CategorySummary>> ListExpenseCategoriesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Records expenses.
/// </summary>
public interface IExpenseRegistrar
{
    /// <summary>
    /// Records an expense and returns it as it was saved.
    /// </summary>
    /// <param name="request">What the user entered. See <see cref="RegisterExpenseRequest"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recorded expense.</returns>
    /// <exception cref="Modules.Shared.Application.ValidationFailedException">
    /// If the request is not valid: missing fields, non-positive amount, or an account or
    /// category that does not belong to the household.
    /// </exception>
    /// <example>
    /// <code>
    /// var expense = await registrar.RegisterAsync(
    ///     new RegisterExpenseRequest(
    ///         accountId,
    ///         categoryId,
    ///         Amount: 42.35m,
    ///         OccurredOn: DateOnly.FromDateTime(DateTime.Today),
    ///         Description: "Weekly shop"),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<RegisteredExpense> RegisterAsync(
        RegisterExpenseRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the landing screen's figures.
/// </summary>
public interface IDashboardQuery
{
    /// <summary>
    /// Returns the summary for the month containing <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">
    /// Any day in the month of interest. Defaults to today in the household's time zone.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The summary. Never <see langword="null"/>: an empty household returns zeros.</returns>
    /// <remarks>
    /// Zeros rather than nothing, so the screen has one shape with or without data and the empty
    /// case is not a second layout to maintain.
    /// </remarks>
    Task<DashboardSummary> GetMonthlySummaryAsync(
        DateOnly? reference = null,
        CancellationToken cancellationToken = default);
}
