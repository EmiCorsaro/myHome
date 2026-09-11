using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Renames an account, changes its type, and archives or unarchives it (story 005).
/// </summary>
/// <remarks>
/// No role is asked for anywhere in here: any member of the household may act on any of the four
/// operations (RF-13). The only thing that is checked is that the account belongs to the household
/// making the request, that its name still fits the household's namespace, and that none of the
/// invariants a credit card or a busy account carry are broken along the way.
/// </remarks>
internal sealed class AccountLifecycleRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    IValidator<RenameAccountRequest> renameValidator,
    IValidator<ChangeAccountTypeRequest> typeValidator) : IAccountLifecycleRegistrar
{
    public async Task<AccountSummary> RenameAsync(
        Guid accountId,
        RenameAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await renameValidator.ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!validation.IsValid)
        {
            throw Failed(validation);
        }

        var householdId = tenant.RequireHouseholdId();
        var account = await RequireAccountAsync(householdId, accountId, cancellationToken)
            .ConfigureAwait(false);

        var name = request.Name.Trim();

        // The new name competes for the very same namespace as a brand new account (RF-2): whether
        // the existing account is archived, or is the very one being renamed, is what tells apart a
        // real conflict from renaming an account to the name it already has.
        var alreadyTaken = await db.Accounts
            .Where(AccountRegistrar.NameAlreadyTaken(householdId, name))
            .AnyAsync(a => a.Id != account.Id, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyTaken)
        {
            throw Invalid("name", DuplicateNameMessage);
        }

        account.Rename(name);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            db.Entry(account).Reload();

            // Lost the race: another member saved the same name between the check above and this
            // update, and the unique index refused the row.
            if (await NameWasTakenMeanwhileAsync(householdId, name, account.Id, cancellationToken)
                .ConfigureAwait(false))
            {
                throw Invalid("name", DuplicateNameMessage);
            }

            throw;
        }

        return await DescribeAsync(account, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountSummary> ChangeTypeAsync(
        Guid accountId,
        ChangeAccountTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await typeValidator.ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!validation.IsValid)
        {
            throw Failed(validation);
        }

        var householdId = tenant.RequireHouseholdId();
        var account = await RequireAccountAsync(householdId, accountId, cancellationToken)
            .ConfigureAwait(false);

        var type = ToDomainType(request.Type);

        // RF-11: absolute, whatever the account's history — checked before the "no movements yet"
        // rule below, which does not apply to this transition at all.
        if (account.Type == AccountType.CreditCard || type == AccountType.CreditCard)
        {
            throw Invalid("type", CreditCardTransitionMessage);
        }

        // RF-3: any other transition is refused once the account has movements of its own.
        if (await OpeningBalances.HasMovementsAsync(db, account.Id, cancellationToken)
            .ConfigureAwait(false))
        {
            throw Invalid("type", HasMovementsMessage);
        }

        account.ChangeType(type);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await DescribeAsync(account, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountSummary> ArchiveAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();
        var account = await RequireAccountAsync(householdId, accountId, cancellationToken)
            .ConfigureAwait(false);

        // RF-8: the account's whole balance, opening balance included, must be zero.
        var balance = await db.Postings
            .Where(p => p.AccountId == account.Id)
            .SumAsync(p => p.AmountBase, cancellationToken)
            .ConfigureAwait(false);

        if (balance != 0m)
        {
            throw Invalid("accountId", NonZeroBalanceMessage);
        }

        // RF-12: nothing scheduled may still be pointing at the account.
        var blockers = await BlockingRecurrencesAsync(account.Id, cancellationToken)
            .ConfigureAwait(false);

        if (blockers.Count > 0)
        {
            throw Invalid(
                "accountId",
                $"{BlockedByRecurrencesMessage} {string.Join(", ", blockers)}.");
        }

        account.Archive();

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await DescribeAsync(account, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountSummary> UnarchiveAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();
        var account = await RequireAccountAsync(householdId, accountId, cancellationToken)
            .ConfigureAwait(false);

        account.Unarchive();

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await DescribeAsync(account, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The account the request names, when the household may act on it.
    /// </summary>
    /// <remarks>
    /// Unlike story 004's own lookup, an archived account is not excluded: renaming it (RF-2's edge
    /// case) and unarchiving it are exactly what this registrar exists to do. An account of another
    /// household, and one the ledger keeps to classify income and expense, get the same answer:
    /// it is not available.
    /// </remarks>
    private async Task<Account> RequireAccountAsync(
        int householdId,
        Guid accountId,
        CancellationToken cancellationToken) =>
        await db.Accounts
            .FirstOrDefaultAsync(
                a => a.PublicId == accountId
                    && a.HouseholdId == householdId
                    && a.Type != AccountType.Income
                    && a.Type != AccountType.Expense,
                cancellationToken)
            .ConfigureAwait(false)
        ?? throw Invalid("accountId", "Esa cuenta no está disponible.");

    /// <summary>
    /// Tells apart the lost race from any other failed update.
    /// </summary>
    private async Task<bool> NameWasTakenMeanwhileAsync(
        int householdId,
        string name,
        int excludeAccountId,
        CancellationToken cancellationToken) =>
        await db.Accounts
            .AsNoTracking()
            .Where(AccountRegistrar.NameAlreadyTaken(householdId, name))
            .AnyAsync(a => a.Id != excludeAccountId, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The active recurring rules and pending planned movements pointing at an account (RF-12).
    /// </summary>
    /// <returns>The description of each one, so the member knows what to fix first.</returns>
    private async Task<List<string>> BlockingRecurrencesAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var activeRules = await db.RecurringRules
            .Where(r => r.AccountId == accountId && r.IsActive)
            .Select(r => r.Description)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pendingPlanned = await db.PlannedMovements
            .Where(p => p.AccountId == accountId && p.Status == PlannedMovementStatus.Pending)
            .Select(p => p.Description)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. activeRules, .. pendingPlanned];
    }

    private async Task<AccountSummary> DescribeAsync(
        Account account,
        CancellationToken cancellationToken)
    {
        var balances = await AccountDirectory
            .BalancesByAccountAsync(db, account.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        return AccountDirectory.ToSummary(account, balances.GetValueOrDefault(account.Id));
    }

    private static AccountType ToDomainType(string type) => type switch
    {
        "checking" => AccountType.Checking,
        "savings" => AccountType.Savings,
        "cash" => AccountType.Cash,
        "creditCard" => AccountType.CreditCard,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown account type."),
    };

    private const string DuplicateNameMessage = "Ya existe una cuenta con ese nombre.";

    private const string CreditCardTransitionMessage =
        "No se puede cambiar una cuenta a tarjeta de crédito, ni una tarjeta de crédito a otro "
            + "tipo: su saldo es deuda, no liquidez, y cambiarle la etiqueta no cambia eso.";

    private const string HasMovementsMessage =
        "No se puede cambiar el tipo de una cuenta que ya tiene movimientos registrados.";

    private const string NonZeroBalanceMessage =
        "No se puede archivar una cuenta con saldo distinto de cero. Déjala a cero primero.";

    private const string BlockedByRecurrencesMessage =
        "No se puede archivar: la están usando";

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private static ValidationFailedException Failed(FluentValidation.Results.ValidationResult validation) =>
        new(
            validation.Errors
                .GroupBy(e => ToFieldName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
