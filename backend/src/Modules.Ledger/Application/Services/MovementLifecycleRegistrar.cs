using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Contracts.Movements;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Voids a movement — a gasto, an ingreso or a transferencia alike (story 013).
/// </summary>
/// <remarks>
/// No role is asked for anywhere in here: any member of the household may void any movement of its
/// own (RF-14). Correcting a movement is not a separate operation this registrar offers: story 013
/// defines it as voiding the original and registering a new one with the corrected data (RF-5,
/// RF-13), and the second half of that is exactly what <see cref="IExpenseRegistrar"/>,
/// <see cref="IIncomeRegistrar"/> and <see cref="ITransferRegistrar"/> already do. A caller
/// correcting a movement calls this method once and then the matching registrar again, with the
/// date it wants for the correction — which may differ from the original's (RF-13).
/// </remarks>
internal sealed class MovementLifecycleRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    TimeProvider clock) : IMovementLifecycleRegistrar
{
    public async Task<VoidedMovement> VoidAsync(
        Guid movementId,
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();

        var entry = await RequireMovementAsync(householdId, movementId, cancellationToken)
            .ConfigureAwait(false);

        // RF-12: an opening balance states a position, not something that happened; story 003
        // already gave it its own way to be corrected.
        if (entry.Kind == EntryKind.Opening)
        {
            throw Invalid("movementId", OpeningBalanceMessage);
        }

        // RF-8: a reversal is the end of the correction chain, not one more movement to undo.
        if (entry.ReversalOfEntryId is not null)
        {
            throw Invalid("movementId", ReversalOfReversalMessage);
        }

        // RF-7: only one reversal ever exists for a movement.
        if (entry.IsVoided)
        {
            throw Invalid("movementId", AlreadyVoidedMessage);
        }

        var reversal = entry.Reverse(clock.GetUtcNow());

        db.Entries.Add(reversal);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Lost the race: another member voided this very movement between the check above and
            // this insert, and the unique index on the reversal link refused the row.
            db.ChangeTracker.Clear();

            var current = await RequireMovementAsync(householdId, movementId, cancellationToken)
                .ConfigureAwait(false);

            if (current.IsVoided)
            {
                throw Invalid("movementId", AlreadyVoidedMessage);
            }

            throw;
        }

        return new VoidedMovement(
            entry.PublicId,
            reversal.PublicId,
            reversal.OccurredOn,
            entry.Kind.ToContractName());
    }

    private async Task<JournalEntry> RequireMovementAsync(
        int householdId,
        Guid movementId,
        CancellationToken cancellationToken) =>
        await db.Entries
            .Include(e => e.Postings)
            .FirstOrDefaultAsync(
                e => e.PublicId == movementId && e.HouseholdId == householdId,
                cancellationToken)
            .ConfigureAwait(false)
        ?? throw Invalid("movementId", "That movement is not available.");

    /// <summary>Shown when the movement named is the household's opening balance (RF-12).</summary>
    private const string OpeningBalanceMessage =
        "The opening balance is not voided: correct it by editing it instead.";

    /// <summary>Shown when the movement named is itself a reversal (RF-8).</summary>
    private const string ReversalOfReversalMessage =
        "A reversal cannot itself be voided: it is already the end of the correction chain.";

    /// <summary>Shown when the movement named has already been voided (RF-7).</summary>
    private const string AlreadyVoidedMessage = "This movement has already been voided.";

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
