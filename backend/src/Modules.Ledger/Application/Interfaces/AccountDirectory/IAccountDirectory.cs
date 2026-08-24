using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Accounts;

// The service interfaces sit next to the implementations that fulfil them, one per file, each in
// its own folder. The Interfaces/ and Services/ folders do not become namespace segments: a
// segment named after the interface would collide with the implementation class of the same name
// and make every `AccountDirectory` reference ambiguous.
//
// This namespace is public API even though it is the implementation layer. What keeps that safe
// is visibility, not naming: every service here is internal, so from another assembly the only
// reachable types are these interfaces. An architecture test enforces it.
namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Reads the household's accounts.
/// </summary>
/// <remarks>
/// Takes no household identifier: it comes from the request's tenant context inside the
/// implementation. See <see cref="LedgerModule"/> for why that applies to every service here.
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
