using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Expenses;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Records expenses.
/// </summary>
/// <remarks>
/// Takes no household identifier: it comes from the request's tenant context inside the
/// implementation. See <see cref="LedgerModule"/>.
/// </remarks>
public interface IExpenseRegistrar
{
    /// <summary>
    /// Records an expense and returns it as it was saved.
    /// </summary>
    /// <param name="request">What the user entered. See <see cref="RegisterExpenseRequest"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recorded expense.</returns>
    /// <exception cref="Modules.Shared.Contracts.ValidationFailedException">
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
