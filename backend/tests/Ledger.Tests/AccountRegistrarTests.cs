using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Opening an account, as story 002 specifies it. Every test names the requirement it pins down;
/// the map from RF to test lives in <c>progress/impl_002.md</c>.
/// </summary>
public sealed class AccountRegistrarTests : IDisposable
{
    private const int OtherHouseholdId = HouseholdId + 1;

    private readonly LedgerDatabase _database = new();
    private readonly AccountRegistrar _registrar;

    public AccountRegistrarTests()
    {
        _registrar = NewRegistrar();
    }

    public void Dispose() => _database.Dispose();

    // RF-1
    [Fact(DisplayName = "A new account belongs to the household that asked for it")]
    public async Task a_new_account_belongs_to_the_household_that_asked_for_it()
    {
        var created = await _registrar.CreateAsync(New("Santander conjunta"));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.PublicId == created.Id);

        Assert.Equal(HouseholdId, stored.HouseholdId);
        Assert.Equal("Santander conjunta", stored.Name);
        Assert.Equal(AccountType.Checking, stored.Type);
        Assert.Equal("Santander conjunta", created.Name);
        Assert.Equal("checking", created.Type);
        Assert.Equal(0m, created.Balance);
    }

    // RF-2
    [Theory(DisplayName = "The four declarable types are accepted")]
    [InlineData("checking", AccountType.Checking)]
    [InlineData("savings", AccountType.Savings)]
    [InlineData("cash", AccountType.Cash)]
    [InlineData("creditCard", AccountType.CreditCard)]
    public async Task the_four_declarable_types_are_accepted(string type, AccountType expected)
    {
        var created = await _registrar.CreateAsync(New("Cuenta", type));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.PublicId == created.Id);

        Assert.Equal(expected, stored.Type);
        Assert.Equal(type, created.Type);
        Assert.True(stored.IsReal);
    }

    // RF-2
    [Theory(DisplayName = "A type outside the four declarable ones is refused")]
    [InlineData("")]
    [InlineData("current")]
    [InlineData("Checking")]
    [InlineData("credit_card")]
    public async Task a_type_outside_the_four_declarable_ones_is_refused(string type)
    {
        var error = await Rejected(New("Cuenta", type));

        Assert.Equal(
            "Elige un tipo de cuenta válido (checking, savings, cash o creditCard).",
            Assert.Single(error.Errors["type"]));
        Assert.Empty(_database.Context.Accounts);
    }

    // RF-3
    [Theory(DisplayName = "A name that is blank is refused")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task a_name_that_is_blank_is_refused(string blank)
    {
        var error = await Rejected(New(blank));

        Assert.Equal("Escribe un nombre para la cuenta.", Assert.Single(error.Errors["name"]));
        Assert.Empty(_database.Context.Accounts);
    }

    // RF-4
    [Fact(DisplayName = "A name longer than 80 characters is refused")]
    public async Task a_name_longer_than_80_characters_is_refused()
    {
        var error = await Rejected(New(new string('a', 81)));

        Assert.Equal(
            "El nombre no puede superar los 80 caracteres.",
            Assert.Single(error.Errors["name"]));
        Assert.Empty(_database.Context.Accounts);
    }

    // RF-4, edge case: exactly 80 characters, with and without outer spaces
    [Theory(DisplayName = "A name of exactly 80 characters is accepted")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_name_of_exactly_80_characters_is_accepted(bool padded)
    {
        var name = new string('a', 80);

        var created = await _registrar.CreateAsync(New(padded ? $"  {name}  " : name));

        Assert.Equal(name, created.Name);
    }

    // RF-4, edge case: outer spaces are gone before the name is measured and stored
    [Fact(DisplayName = "A name is stored without its outer spaces")]
    public async Task a_name_is_stored_without_its_outer_spaces()
    {
        var created = await _registrar.CreateAsync(New("  Efectivo  "));

        Assert.Equal("Efectivo", created.Name);
        Assert.Equal("Efectivo", (await _database.Context.Accounts.SingleAsync()).Name);
    }

    // RF-5
    [Theory(DisplayName = "A name already used in the household is refused")]
    [InlineData("Efectivo")]
    [InlineData("EFECTIVO")]
    [InlineData("efectivo")]
    [InlineData("  Efectivo  ")]
    public async Task a_name_already_used_in_the_household_is_refused(string attempted)
    {
        await Existing("Efectivo");

        var error = await Rejected(New(attempted));

        Assert.Equal("Ya existe una cuenta con ese nombre.", Assert.Single(error.Errors["name"]));
        Assert.Equal(1, await _database.Context.Accounts.CountAsync());
    }

    // RF-5
    [Fact(DisplayName = "A name used by an account of another type is taken all the same")]
    public async Task a_name_used_by_an_account_of_another_type_is_taken_all_the_same()
    {
        await Existing("Santander", AccountType.Savings);

        var error = await Rejected(New("Santander", "creditCard"));

        Assert.True(error.Errors.ContainsKey("name"));
    }

    // RF-5, edge case: archived account
    [Fact(DisplayName = "An archived account keeps its name taken")]
    public async Task an_archived_account_keeps_its_name_taken()
    {
        await Existing("Santander conjunta", archived: true);

        var error = await Rejected(New("Santander conjunta"));

        Assert.Equal("Ya existe una cuenta con ese nombre.", Assert.Single(error.Errors["name"]));
        Assert.Equal(1, await _database.Context.Accounts.CountAsync());
    }

    // RF-5
    [Fact(DisplayName = "A name used by another household is still free")]
    public async Task a_name_used_by_another_household_is_still_free()
    {
        await Existing("Efectivo", householdId: OtherHouseholdId);

        var created = await _registrar.CreateAsync(New("Efectivo"));

        Assert.Equal("Efectivo", created.Name);
    }

    // RF-6
    [Theory(DisplayName = "A new account is created as a tracked one")]
    [InlineData("checking")]
    [InlineData("savings")]
    [InlineData("cash")]
    [InlineData("creditCard")]
    public async Task a_new_account_is_created_as_a_tracked_one(string type)
    {
        var created = await _registrar.CreateAsync(New("Cuenta", type));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.PublicId == created.Id);

        Assert.True(stored.IsTracked);
        Assert.True(created.IsTracked);
        Assert.Null(stored.MinimumBufferTarget);
    }

    // RF-7
    [Fact(DisplayName = "A new account is given the household's currency, unasked")]
    public async Task a_new_account_is_given_the_households_currency_unasked()
    {
        var registrar = NewRegistrar(currency: CurrencyCode.UsDollar);

        var created = await registrar.CreateAsync(New("Cuenta en dólares"));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.PublicId == created.Id);

        Assert.Equal(CurrencyCode.UsDollar, stored.Currency);
        Assert.Equal("USD", created.Currency);
    }

    // RF-8
    [Fact(DisplayName = "A household may open as many accounts as it wants")]
    public async Task a_household_may_open_as_many_accounts_as_it_wants()
    {
        for (var i = 0; i < 40; i++)
        {
            await _registrar.CreateAsync(New(string.Create(null, $"Cuenta {i}")));
        }

        Assert.Equal(40, await _database.Context.Accounts.CountAsync());
    }

    // RF-9
    [Theory(DisplayName = "The types the system keeps for income and expense cannot be declared")]
    [InlineData("income")]
    [InlineData("expense")]
    public async Task the_types_the_system_keeps_cannot_be_declared(string internalType)
    {
        var error = await Rejected(New("Gastos", internalType));

        Assert.True(error.Errors.ContainsKey("type"));
        Assert.Empty(_database.Context.Accounts);
    }

    // RF-9
    [Fact(DisplayName = "An account the system keeps does not take a name away from the user")]
    public async Task an_account_the_system_keeps_does_not_take_a_name_away_from_the_user()
    {
        // The ledger opens these itself to classify movements. The user cannot see them, so being
        // told "Gastos" is taken by one of them would be unexplainable.
        await Existing("Gastos", AccountType.Expense);

        var created = await _registrar.CreateAsync(New("Gastos"));

        Assert.Equal("Gastos", created.Name);
    }

    // RF-10
    [Fact(DisplayName = "Any member of the household may open an account")]
    public async Task any_member_of_the_household_may_open_an_account()
    {
        var first = NewRegistrar(memberId: 7);
        var second = NewRegistrar(memberId: 42);
        var anonymous = NewRegistrar(memberId: null);

        await first.CreateAsync(New("Santander conjunta"));
        await second.CreateAsync(New("Ahorro", "savings"));
        await anonymous.CreateAsync(New("Efectivo", "cash"));

        Assert.Equal(3, await _database.Context.Accounts.CountAsync());
    }

    // RF-1: a new account lands at the end of the household's list
    [Fact(DisplayName = "Each new account is placed after the ones already there")]
    public async Task each_new_account_is_placed_after_the_ones_already_there()
    {
        var first = await _registrar.CreateAsync(New("Santander conjunta"));
        var second = await _registrar.CreateAsync(New("Ahorro", "savings"));

        var orders = await _database.Context.Accounts
            .OrderBy(a => a.DisplayOrder)
            .Select(a => a.PublicId)
            .ToListAsync();

        Assert.Equal([first.Id, second.Id], orders);
    }

    private static CreateAccountRequest New(string name, string type = "checking") =>
        new(name, type);

    private AccountRegistrar NewRegistrar(
        int? memberId = null, CurrencyCode? currency = null) =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId, memberId),
            new CreateAccountRequestValidator(),
            new TestHouseholdDirectory(currency ?? CurrencyCode.Euro));

    private async Task<ValidationFailedException> Rejected(CreateAccountRequest request) =>
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.CreateAsync(request));

    private async Task<Account> Existing(
        string name,
        AccountType type = AccountType.Checking,
        int householdId = HouseholdId,
        bool archived = false)
    {
        var account = Account.Create(householdId, name, type, CurrencyCode.Euro);

        if (archived)
        {
            account.Archive();
        }

        _database.Context.Accounts.Add(account);
        await _database.Context.SaveChangesAsync();

        return account;
    }
}
