using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using MyHome.Modules.Shared.Tenancy;

namespace MyHome.Modules.Ledger.Application;

internal sealed class AccountRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    IValidator<CreateAccountRequest> validator,
    IHouseholdDirectory households) : IAccountRegistrar
{
    /// <summary>Gap left between the display orders of two consecutive accounts.</summary>
    private const int DisplayOrderStep = 10;

    public async Task<AccountSummary> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await validator.ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!validation.IsValid)
        {
            throw new ValidationFailedException(
                validation.Errors
                    .GroupBy(e => ToFieldName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var householdId = tenant.RequireHouseholdId();
        var name = request.Name.Trim();
        var type = ToDomainType(request.Type);

        // The name is taken whatever the type of the existing account and whether or not it is
        // archived: both are deliberate (RF-5 and its edge case) and AccountRegistrarTests pins
        // them down.
        var alreadyExists = await db.Accounts
            .AnyAsync(NameAlreadyTaken(householdId, name), cancellationToken)
            .ConfigureAwait(false);

        if (alreadyExists)
        {
            throw Invalid("name", DuplicateNameMessage);
        }

        var currency = await HouseholdCurrencyAsync(cancellationToken).ConfigureAwait(false);

        var account = Account.Create(
            householdId,
            name,
            type,
            currency,
            isTracked: true,
            displayOrder: await NextDisplayOrderAsync(householdId, cancellationToken)
                .ConfigureAwait(false));

        db.Accounts.Add(account);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // The row was refused, so it is not there: stop tracking it before asking anything
            // else of this context.
            db.Entry(account).State = EntityState.Detached;

            // Lost the race: another member of the household saved the same name between the
            // check above and this insert, and the unique index refused the row. The caller gets
            // the same error it would have got a millisecond earlier, so losing the race is not
            // something a client has to know about. Any other failed insert is not ours to
            // explain and travels on.
            if (await NameWasTakenMeanwhileAsync(householdId, name, cancellationToken)
                .ConfigureAwait(false))
            {
                throw Invalid("name", DuplicateNameMessage);
            }

            throw;
        }

        // A brand new account holds no postings yet, so its balance is zero; declaring an opening
        // balance is story 003's job, not this one's.
        return AccountDirectory.ToSummary(account, balance: 0m);
    }

    /// <summary>
    /// The currency every account of the household is kept in (RF-7).
    /// </summary>
    /// <returns>The household's base currency.</returns>
    /// <remarks>
    /// Asked of the shared module through its published interface rather than read from a table of
    /// this module: the household is not the ledger's to own. The user is never offered the choice,
    /// so accounts in a second currency cannot appear by accident.
    /// </remarks>
    private async Task<CurrencyCode> HouseholdCurrencyAsync(CancellationToken cancellationToken)
    {
        var household = await households.GetCurrentAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The request resolved to a household that no longer exists.");

        return CurrencyCode.Parse(household.BaseCurrency);
    }

    /// <summary>
    /// Puts a new account at the end of the household's list.
    /// </summary>
    /// <param name="householdId">Household the account belongs to.</param>
    /// <returns>A display order above every account the household can see.</returns>
    private async Task<int> NextDisplayOrderAsync(int householdId, CancellationToken cancellationToken)
    {
        var highest = await db.Accounts
            .Where(a => a.HouseholdId == householdId
                && a.Type != AccountType.Income
                && a.Type != AccountType.Expense)
            .Select(a => (int?)a.DisplayOrder)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        return (highest ?? 0) + DisplayOrderStep;
    }

    /// <summary>
    /// Tells apart the lost race from any other failed insert.
    /// </summary>
    /// <param name="householdId">Household the account would have belonged to.</param>
    /// <param name="name">Name asked for, already trimmed.</param>
    /// <returns><c>true</c> when the name is taken now, after the insert was refused.</returns>
    /// <remarks>
    /// Asking the database again, rather than reading the provider's error code, keeps this free
    /// of PostgreSQL's SQLSTATE and lets the same code path be exercised by a test running on a
    /// different provider.
    /// </remarks>
    private async Task<bool> NameWasTakenMeanwhileAsync(
        int householdId,
        string name,
        CancellationToken cancellationToken) =>
        await db.Accounts
            .AsNoTracking()
            .AnyAsync(NameAlreadyTaken(householdId, name), cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The predicate that decides whether a household already uses an account name.
    /// </summary>
    /// <param name="householdId">Household the account would belong to.</param>
    /// <param name="name">Name asked for, already trimmed.</param>
    /// <returns>A predicate the database provider can translate on its own.</returns>
    /// <remarks>
    /// Case-insensitivity is expressed as <c>lower(name) = lower(@name)</c> instead of
    /// <see cref="string.Equals(string, string, StringComparison)"/>, which no provider can
    /// translate and which therefore only fails once a real database is on the other end.
    /// It is a separate member so a test can assert it still translates to SQL.
    ///
    /// The accounts the system keeps to classify income and expense are left out: the user cannot
    /// create them (RF-9), cannot see them and must not be told a name is taken by a row that only
    /// the ledger knows about. The unique index carries the very same filter.
    /// </remarks>
    internal static Expression<Func<Account, bool>> NameAlreadyTaken(int householdId, string name)
    {
        var comparable = Comparable(name);

#pragma warning disable CA1304, CA1311, CA1862 // Translated by the provider; no .NET culture is involved.
        return a => a.HouseholdId == householdId
            && a.Type != AccountType.Income
            && a.Type != AccountType.Expense
            && a.Name.ToLower() == comparable;
#pragma warning restore CA1304, CA1311, CA1862
    }

    private const string DuplicateNameMessage = "Ya existe una cuenta con ese nombre.";

    private static string Comparable(string name) => name.Trim().ToLowerInvariant();

    private static AccountType ToDomainType(string type) => type switch
    {
        "checking" => AccountType.Checking,
        "savings" => AccountType.Savings,
        "cash" => AccountType.Cash,
        "creditCard" => AccountType.CreditCard,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown account type."),
    };

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
