using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using MyHome.Modules.Shared.Tenancy;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Declares and corrects the money an account starts with (story 003).
/// </summary>
/// <remarks>
/// No role is asked for anywhere in here: any member of the household may declare or edit an
/// opening balance (RF-13). The only thing that is checked is that the account belongs to the
/// household making the request.
/// </remarks>
internal sealed class OpeningBalanceRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    IHouseholdDirectory households,
    TimeProvider clock) : IOpeningBalanceRegistrar
{
    public async Task<OpeningBalance> DeclareAsync(
        Guid accountId,
        DeclareOpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var householdId = tenant.RequireHouseholdId();
        var account = await RequireAccountAsync(householdId, accountId, cancellationToken)
            .ConfigureAwait(false);

        await EnsureNotInTheFutureAsync(request.OccurredOn, cancellationToken).ConfigureAwait(false);

        // Declaring is a one-off. The account that already has an opening balance is not an error
        // to hide: the member is told where to go instead (RF-7).
        if (await AlreadyDeclaredAsync(account.Id, cancellationToken).ConfigureAwait(false))
        {
            throw Invalid("openingBalance", AlreadyDeclaredMessage);
        }

        var entry = JournalEntry.OpenBalance(
            householdId,
            account,
            Money.Of(request.Amount, account.Currency),
            request.OccurredOn,
            clock.GetUtcNow());

        db.Entries.Add(entry);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // The rows were refused, so they are not there: stop tracking the entry and the posting
            // that came with it before asking anything else of this context.
            db.ChangeTracker.Clear();

            // Lost the race: another member declared the opening balance of this account between
            // the check above and this insert, and the unique index refused the row. The caller
            // gets the answer it would have got a millisecond earlier.
            if (await AlreadyDeclaredAsync(account.Id, cancellationToken).ConfigureAwait(false))
            {
                throw Invalid("openingBalance", AlreadyDeclaredMessage);
            }

            throw;
        }

        return await DescribeAsync(account, entry, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OpeningBalance> AmendAsync(
        Guid accountId,
        AmendOpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount is null && request.OccurredOn is null)
        {
            throw Invalid("amount", "Indica el nuevo importe, la nueva fecha o ambos.");
        }

        var householdId = tenant.RequireHouseholdId();
        var account = await RequireAccountAsync(householdId, accountId, cancellationToken)
            .ConfigureAwait(false);

        var entry = await db.Entries
            .Include(e => e.Postings)
            .FirstOrDefaultAsync(e => e.OpeningAccountId == account.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("openingBalance", NotDeclaredMessage);

        var amount = request.Amount ?? entry.Postings.Single().Amount;
        var occurredOn = request.OccurredOn ?? entry.OccurredOn;

        await EnsureNotInTheFutureAsync(occurredOn, cancellationToken).ConfigureAwait(false);

        // The mirror image of RF-8: moving the opening balance forward must not strand a movement
        // behind it (RF-12). Editing an account that already has movements is otherwise perfectly
        // normal (RF-10) — only this one date conflict stops it.
        var earliest = await OpeningBalances
            .EarliestMovementAsync(db, account.Id, cancellationToken)
            .ConfigureAwait(false);

        if (earliest is { } first && first < occurredOn)
        {
            throw Invalid("occurredOn", OpeningBalances.MovementBeforeOpeningMessage);
        }

        entry.AmendOpeningBalance(Money.Of(amount, account.Currency), occurredOn);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await DescribeAsync(account, entry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The account the request names, when the household may act on it.
    /// </summary>
    /// <param name="householdId">Household making the request.</param>
    /// <param name="accountId">Public identifier of the account.</param>
    /// <returns>The account.</returns>
    /// <remarks>
    /// An account of another household, an archived one, and one the ledger keeps to classify
    /// income and expense all get the same answer: it is not available. Telling them apart would
    /// say something about accounts the caller is not meant to know exist.
    /// </remarks>
    private async Task<Account> RequireAccountAsync(
        int householdId,
        Guid accountId,
        CancellationToken cancellationToken) =>
        await db.Accounts
            .FirstOrDefaultAsync(
                a => a.PublicId == accountId
                    && a.HouseholdId == householdId
                    && !a.IsArchived
                    && a.Type != AccountType.Income
                    && a.Type != AccountType.Expense,
                cancellationToken)
            .ConfigureAwait(false)
        ?? throw Invalid("accountId", "Esa cuenta no está disponible.");

    /// <summary>
    /// Refuses a date the household has not lived through yet (RF-6, RF-11).
    /// </summary>
    /// <param name="occurredOn">Date asked for.</param>
    /// <remarks>
    /// Today is read in the household's own time zone, so the boundary is the one the member sees
    /// on their calendar. Today itself is fine: the limit is the future.
    /// </remarks>
    private async Task EnsureNotInTheFutureAsync(
        DateOnly occurredOn,
        CancellationToken cancellationToken)
    {
        var household = await households.GetCurrentAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The request resolved to a household that no longer exists.");

        if (occurredOn > HouseholdClock.TodayIn(household.TimeZoneId, clock))
        {
            throw Invalid("occurredOn", "La fecha del saldo inicial no puede ser futura.");
        }
    }

    private async Task<bool> AlreadyDeclaredAsync(int accountId, CancellationToken cancellationToken) =>
        await db.Entries
            .AsNoTracking()
            .AnyAsync(e => e.OpeningAccountId == accountId, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Says back the opening balance in force and what it leaves the account at (RF-1, RF-9).
    /// </summary>
    /// <param name="account">Account the balance belongs to.</param>
    /// <param name="entry">The opening entry, already saved.</param>
    /// <returns>The opening balance and the account balance it produces.</returns>
    private async Task<OpeningBalance> DescribeAsync(
        Account account,
        JournalEntry entry,
        CancellationToken cancellationToken)
    {
        // Added up from the postings rather than from the figure just written, so an account with
        // movements reports the balance the household will actually see.
        var balances = await AccountDirectory
            .BalancesByAccountAsync(db, account.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        return new OpeningBalance(
            account.PublicId,
            account.Name,
            account.Currency.Value,
            Round(entry.Postings.Single().Amount),
            entry.OccurredOn,
            Round(balances.GetValueOrDefault(account.Id)));
    }

    private const string AlreadyDeclaredMessage =
        "Esta cuenta ya tiene un saldo inicial declarado. Edítalo en lugar de declararlo otra vez.";

    private const string NotDeclaredMessage =
        "Esta cuenta todavía no tiene un saldo inicial declarado. Decláralo primero.";

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.ToEven);

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
