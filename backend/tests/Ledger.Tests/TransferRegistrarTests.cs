using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Transfers;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Registering a transfer, as story 011 specifies it. Every test names the requirement it pins
/// down; the map from RF to test lives in <c>progress/impl_011.md</c>. RF-1 through RF-6 and RF-8,
/// RF-14 through RF-17 and RF-19, RF-20 (double entry, when a category applies, the credit-card
/// cases) are pinned down at the domain level in <see cref="JournalEntryTests"/>; the tests here
/// confirm the service that sits in front of it resolves accounts and categories, and carries the
/// same guarantees through.
/// </summary>
public sealed class TransferRegistrarTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 9);

    private readonly LedgerDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // RF-1
    [Fact(DisplayName = "Registering a transfer moves the balance from origin to destination")]
    public async Task registering_a_transfer_moves_the_balance_from_origin_to_destination()
    {
        var checking = await Existing("Santander conjunta");
        await Opening(checking, 500m);
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var registered = await transfers.RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, savings.PublicId, 200m, Today));

        Assert.Equal(200m, registered.Amount);
        Assert.Equal(300m, await BalanceOf(checking));
        Assert.Equal(200m, await BalanceOf(savings));
    }

    // RF-6
    [Fact(DisplayName = "A transfer into a credit card reduces its pending debt")]
    public async Task a_transfer_into_a_credit_card_reduces_its_pending_debt()
    {
        var checking = await Existing("Santander conjunta");
        var card = await Existing("Tarjeta", AccountType.CreditCard);
        await Opening(card, -150m);

        var transfers = TransferRegistrarFor();

        await transfers.RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, card.PublicId, 100m, Today));

        Assert.Equal(-50m, await BalanceOf(card));
    }

    // RF-20
    [Fact(DisplayName = "A transfer into a credit card that exceeds its debt leaves it in credit")]
    public async Task a_transfer_into_a_credit_card_that_exceeds_its_debt_leaves_it_in_credit()
    {
        var checking = await Existing("Santander conjunta");
        var card = await Existing("Tarjeta", AccountType.CreditCard);
        await Opening(card, -50m);

        var transfers = TransferRegistrarFor();

        await transfers.RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, card.PublicId, 100m, Today));

        Assert.Equal(50m, await BalanceOf(card));
    }

    // RF-7
    [Fact(DisplayName = "A transfer with no description is accepted")]
    public async Task a_transfer_with_no_description_is_accepted()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var registered = await transfers.RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, savings.PublicId, 200m, Today));

        Assert.Equal(string.Empty, registered.Description);
    }

    // RF-7, edge case
    [Fact(DisplayName = "A transfer description longer than 200 characters is refused")]
    public async Task a_transfer_description_longer_than_200_characters_is_refused()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(new RegisterTransferRequest(
                checking.PublicId, savings.PublicId, 200m, Today, new string('a', 201))));

        Assert.True(error.Errors.ContainsKey("description"));
    }

    // RF-9
    [Theory(DisplayName = "A transfer amount that is not greater than zero is refused")]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task a_transfer_amount_that_is_not_greater_than_zero_is_refused(decimal amount)
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(
                new RegisterTransferRequest(checking.PublicId, savings.PublicId, amount, Today)));

        Assert.True(error.Errors.ContainsKey("amount"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-10
    [Fact(DisplayName = "A transfer dated tomorrow is refused")]
    public async Task a_transfer_dated_tomorrow_is_refused()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(new RegisterTransferRequest(
                checking.PublicId, savings.PublicId, 200m, Today.AddDays(1))));

        Assert.True(error.Errors.ContainsKey("occurredOn"));
    }

    // RF-10, edge case: today is not the future
    [Fact(DisplayName = "A transfer dated today is accepted")]
    public async Task a_transfer_dated_today_is_accepted()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var registered = await transfers.RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, savings.PublicId, 200m, Today));

        Assert.Equal(Today, registered.OccurredOn);
    }

    // RF-8
    [Fact(DisplayName = "The same account as origin and destination is refused")]
    public async Task the_same_account_as_origin_and_destination_is_refused()
    {
        var checking = await Existing("Santander conjunta");

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(
                new RegisterTransferRequest(checking.PublicId, checking.PublicId, 200m, Today)));

        Assert.True(error.Errors.ContainsKey("toAccountId"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-11
    [Fact(DisplayName = "An origin account belonging to another household is refused")]
    public async Task an_origin_account_belonging_to_another_household_is_refused()
    {
        var checking = await Existing("Santander conjunta", householdId: HouseholdId + 1);
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(
                new RegisterTransferRequest(checking.PublicId, savings.PublicId, 200m, Today)));

        Assert.True(error.Errors.ContainsKey("fromAccountId"));
    }

    // RF-11
    [Fact(DisplayName = "A destination account belonging to another household is refused")]
    public async Task a_destination_account_belonging_to_another_household_is_refused()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings, householdId: HouseholdId + 1);

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(
                new RegisterTransferRequest(checking.PublicId, savings.PublicId, 200m, Today)));

        Assert.True(error.Errors.ContainsKey("toAccountId"));
    }

    // RF-12
    [Fact(DisplayName = "An archived origin account is refused")]
    public async Task an_archived_origin_account_is_refused()
    {
        var checking = await Existing("Santander conjunta");
        checking.Archive();
        await _database.Context.SaveChangesAsync();
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(
                new RegisterTransferRequest(checking.PublicId, savings.PublicId, 200m, Today)));

        Assert.True(error.Errors.ContainsKey("fromAccountId"));
    }

    // RF-12
    [Fact(DisplayName = "An archived destination account is refused")]
    public async Task an_archived_destination_account_is_refused()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);
        savings.Archive();
        await _database.Context.SaveChangesAsync();

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(
                new RegisterTransferRequest(checking.PublicId, savings.PublicId, 200m, Today)));

        Assert.True(error.Errors.ContainsKey("toAccountId"));
    }

    // RF-13
    [Fact(DisplayName = "Resending the same mutation does not duplicate the transfer")]
    public async Task resending_the_same_mutation_does_not_duplicate_the_transfer()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var request = new RegisterTransferRequest(
            checking.PublicId, savings.PublicId, 200m, Today, "Ahorro", ClientMutationId: "abc-123");

        var first = await transfers.RegisterAsync(request);
        var second = await transfers.RegisterAsync(request);

        Assert.Equal(first.Id, second.Id);
        Assert.False(first.WasAlreadyRegistered);
        Assert.True(second.WasAlreadyRegistered);
        Assert.Equal(1, await _database.Context.Entries.CountAsync(e => e.Kind == EntryKind.Transfer));
    }

    // RF-2, RF-3, RF-4: between two controlled accounts, no category is asked for or stored.
    [Fact(DisplayName = "A transfer between two controlled accounts needs no category")]
    public async Task a_transfer_between_two_controlled_accounts_needs_no_category()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var registered = await transfers.RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, savings.PublicId, 200m, Today));

        Assert.Null(registered.CategoryName);
    }

    // RF-14, RF-15
    [Fact(DisplayName = "A transfer into an uncontrolled destination without a category is refused")]
    public async Task a_transfer_into_an_uncontrolled_destination_without_a_category_is_refused()
    {
        var checking = await Existing("Santander conjunta");
        var wallet = await Existing("Efectivo del compañero", AccountType.Cash, isTracked: false);

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(
                new RegisterTransferRequest(checking.PublicId, wallet.PublicId, 200m, Today)));

        Assert.True(error.Errors.ContainsKey("categoryId"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-14, RF-16
    [Fact(DisplayName = "A transfer into an uncontrolled destination with a category is registered")]
    public async Task a_transfer_into_an_uncontrolled_destination_with_a_category_is_registered()
    {
        var checking = await Existing("Santander conjunta");
        var wallet = await Existing("Efectivo del compañero", AccountType.Cash, isTracked: false);
        var category = await NewExpenseCategory();

        var transfers = TransferRegistrarFor();

        var registered = await transfers.RegisterAsync(new RegisterTransferRequest(
            checking.PublicId, wallet.PublicId, 200m, Today, CategoryId: category.PublicId));

        Assert.Equal(category.Name, registered.CategoryName);
        Assert.Equal(200m, await BalanceOf(wallet));
    }

    // RF-17
    [Fact(DisplayName = "An income category is refused for a transfer")]
    public async Task an_income_category_is_refused_for_a_transfer()
    {
        var checking = await Existing("Santander conjunta");
        var wallet = await Existing("Efectivo del compañero", AccountType.Cash, isTracked: false);
        var category = await NewIncomeCategory();

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(new RegisterTransferRequest(
                checking.PublicId, wallet.PublicId, 200m, Today, CategoryId: category.PublicId)));

        Assert.True(error.Errors.ContainsKey("categoryId"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-19
    [Fact(DisplayName = "A credit card origin is refused")]
    public async Task a_credit_card_origin_is_refused()
    {
        var card = await Existing("Tarjeta", AccountType.CreditCard);
        var checking = await Existing("Santander conjunta");

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(
                new RegisterTransferRequest(card.PublicId, checking.PublicId, 100m, Today)));

        Assert.True(error.Errors.ContainsKey("fromAccountId"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-21
    [Fact(DisplayName = "A transfer dated before the origin account's opening balance is refused")]
    public async Task a_transfer_dated_before_the_origin_accounts_opening_balance_is_refused()
    {
        var checking = await Existing("Santander conjunta");
        await Opening(checking, 100m, Today.AddDays(-10));
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(new RegisterTransferRequest(
                checking.PublicId, savings.PublicId, 10m, Today.AddDays(-11))));

        Assert.True(error.Errors.ContainsKey("occurredOn"));
    }

    // RF-21
    [Fact(DisplayName = "A transfer dated before the destination account's opening balance is refused")]
    public async Task a_transfer_dated_before_the_destination_accounts_opening_balance_is_refused()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);
        await Opening(savings, 50m, Today.AddDays(-5));

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(new RegisterTransferRequest(
                checking.PublicId, savings.PublicId, 10m, Today.AddDays(-6))));

        Assert.True(error.Errors.ContainsKey("occurredOn"));
    }

    // RF-22
    [Fact(DisplayName = "A transfer amount of exactly two decimals is accepted")]
    public async Task a_transfer_amount_of_exactly_two_decimals_is_accepted()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var registered = await transfers.RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, savings.PublicId, 10.99m, Today));

        Assert.Equal(10.99m, registered.Amount);
    }

    // RF-23
    [Fact(DisplayName = "A transfer amount with more than two decimals is refused, not rounded")]
    public async Task a_transfer_amount_with_more_than_two_decimals_is_refused_not_rounded()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var transfers = TransferRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => transfers.RegisterAsync(
                new RegisterTransferRequest(checking.PublicId, savings.PublicId, 10.999m, Today)));

        Assert.True(error.Errors.ContainsKey("amount"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-24
    [Fact(DisplayName = "Any member of the household may register a transfer")]
    public async Task any_member_of_the_household_may_register_a_transfer()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);

        var byOneMember = await TransferRegistrarFor(memberId: 7).RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, savings.PublicId, 10m, Today));
        Assert.NotNull(byOneMember);

        var byAnother = await TransferRegistrarFor(memberId: 42).RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, savings.PublicId, 10m, Today));
        Assert.NotNull(byAnother);

        var byAnonymous = await TransferRegistrarFor(memberId: null).RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, savings.PublicId, 10m, Today));
        Assert.NotNull(byAnonymous);
    }

    private TransferRegistrar TransferRegistrarFor(int? memberId = null) =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId, memberId),
            new RegisterTransferRequestValidator());

    private async Task<Account> Existing(
        string name,
        AccountType type = AccountType.Checking,
        int householdId = HouseholdId,
        bool isTracked = true)
    {
        var account = Account.Create(householdId, name, type, CurrencyCode.Euro, isTracked);

        _database.Context.Accounts.Add(account);
        await _database.Context.SaveChangesAsync();

        return account;
    }

    private async Task<Category> NewExpenseCategory()
    {
        var category = Category.Create(
            HouseholdId, $"Gasto {Guid.CreateVersion7()}", CategoryKind.Expense, colorIndex: 1);

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return category;
    }

    private async Task<Category> NewIncomeCategory()
    {
        var category = Category.Create(
            HouseholdId, $"Ingreso {Guid.CreateVersion7()}", CategoryKind.Income, colorIndex: 2);

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return category;
    }

    private async Task Opening(Account account, decimal amount, DateOnly? occurredOn = null)
    {
        _database.Context.Entries.Add(JournalEntry.OpenBalance(
            account.HouseholdId, account, Euros(amount), occurredOn ?? Today.AddDays(-30)));

        await _database.Context.SaveChangesAsync();
    }

    private async Task<decimal> BalanceOf(Account account) =>
        await _database.Context.Postings
            .Where(p => p.AccountId == account.Id)
            .SumAsync(p => p.AmountBase);
}
