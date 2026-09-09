using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// The two things a household does with the opening balance of an account.
/// </summary>
/// <remarks>
/// Declaring and editing are kept apart on purpose. Declaring happens once and is refused if the
/// account already has an opening balance (RF-7); editing only applies to an account that already
/// has one, and is allowed even after movements have been recorded (RF-10).
/// </remarks>
public interface IOpeningBalanceRegistrar
{
    /// <summary>
    /// States, once, with how much money an account starts.
    /// </summary>
    /// <param name="accountId">Public identifier of the account.</param>
    /// <param name="request">The amount and the date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The opening balance in force and the resulting balance of the account.</returns>
    Task<OpeningBalance> DeclareAsync(
        Guid accountId,
        DeclareOpeningBalanceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects the opening balance already declared for an account.
    /// </summary>
    /// <param name="accountId">Public identifier of the account.</param>
    /// <param name="request">The new amount, the new date, or both.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The opening balance in force and the recomputed balance of the account.</returns>
    Task<OpeningBalance> AmendAsync(
        Guid accountId,
        AmendOpeningBalanceRequest request,
        CancellationToken cancellationToken = default);
}
