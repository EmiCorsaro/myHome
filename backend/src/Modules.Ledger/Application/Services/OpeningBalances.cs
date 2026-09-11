using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// What the rest of the ledger needs to know about the opening balance of an account.
/// </summary>
/// <remarks>
/// The lower bound an opening balance puts on an account is not only this story's business: it is
/// the same rule seen from both sides. RF-8 asks it of a movement being recorded, RF-12 asks it of
/// an opening balance being moved forward, and the spec says outright that both use the same
/// criterion. It lives here, apart from the service, so the story that records movements can hold
/// to it without restating it.
/// </remarks>
internal static class OpeningBalances
{
    /// <summary>
    /// The date an account's opening balance is stated at, if it has one.
    /// </summary>
    /// <param name="db">Ledger database.</param>
    /// <param name="accountId">Internal identifier of the account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The date, or null when the account has no opening balance declared.</returns>
    public static async Task<DateOnly?> DeclaredDateAsync(
        LedgerDbContext db,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var dates = await db.Entries
            .Where(e => e.OpeningAccountId == accountId)
            .Select(e => e.OccurredOn)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return dates.Count == 0 ? null : dates[0];
    }

    /// <summary>
    /// Decides whether a movement would fall before the account's opening balance (RF-8).
    /// </summary>
    /// <param name="db">Ledger database.</param>
    /// <param name="accountId">Internal identifier of the account the movement is recorded on.</param>
    /// <param name="occurredOn">Date of the movement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the movement is earlier than the opening balance in force.</returns>
    /// <remarks>
    /// An account with no opening balance declared has no lower bound at all: a household may start
    /// recording movements before it ever states where the account started, and nothing about that
    /// is an error. A movement on the very day of the opening balance is fine too; only what falls
    /// strictly before it is refused.
    /// </remarks>
    public static async Task<bool> IsBeforeOpeningBalanceAsync(
        LedgerDbContext db,
        int accountId,
        DateOnly occurredOn,
        CancellationToken cancellationToken = default) =>
        await DeclaredDateAsync(db, accountId, cancellationToken).ConfigureAwait(false)
            is { } declared && occurredOn < declared;

    /// <summary>
    /// The earliest movement recorded against an account, leaving its opening balance aside.
    /// </summary>
    /// <param name="db">Ledger database.</param>
    /// <param name="accountId">Internal identifier of the account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The date of the earliest movement, or null when there is none.</returns>
    public static async Task<DateOnly?> EarliestMovementAsync(
        LedgerDbContext db,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var query =
            from posting in db.Postings
            join entry in db.Entries on posting.JournalEntryId equals entry.Id
            where posting.AccountId == accountId && entry.Kind != EntryKind.Opening
            select (DateOnly?)entry.OccurredOn;

        return await query.MinAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Decides whether an account has any movement recorded against it, its opening balance aside
    /// (story 005, RF-3).
    /// </summary>
    /// <param name="db">Ledger database.</param>
    /// <param name="accountId">Internal identifier of the account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when at least one non-opening entry posts against the account.</returns>
    /// <remarks>
    /// An opening balance states where an account starts; it is not something that "happened" to
    /// it, which is exactly the distinction story 003 draws between declaring a balance and
    /// recording a movement. Changing an account's type only needs to be blocked by the latter.
    /// </remarks>
    public static async Task<bool> HasMovementsAsync(
        LedgerDbContext db,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var query =
            from posting in db.Postings
            join entry in db.Entries on posting.JournalEntryId equals entry.Id
            where posting.AccountId == accountId && entry.Kind != EntryKind.Opening
            select entry.Id;

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The message shown whenever a date would leave a movement before the opening balance.</summary>
    public const string MovementBeforeOpeningMessage =
        "La cuenta tiene movimientos anteriores a esa fecha.";
}
