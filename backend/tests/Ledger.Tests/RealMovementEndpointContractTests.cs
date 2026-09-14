using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MyHome.Api.Endpoints;
using MyHome.Api.ErrorHandling;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Contracts.Incomes;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using MyHome.Modules.Shared.Tenancy;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

public sealed class RealMovementEndpointContractTests : IDisposable
{
    private static readonly DateOnly Early = new(2026, 9, 1);
    private static readonly DateOnly Middle = new(2026, 9, 9);
    private static readonly TimeProvider Clock =
        new FixedTimeProvider(new DateTimeOffset(Middle, new TimeOnly(10, 0), TimeSpan.Zero));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LedgerDatabase database = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task both_routes_return_empty_collections_without_parameters()
    {
        await RunApiAsync(async client =>
        {
            var incomes = await client.GetAsync("/api/incomes");
            var expenses = await client.GetAsync("/api/expenses");

            Assert.Equal(HttpStatusCode.OK, incomes.StatusCode);
            Assert.Equal(HttpStatusCode.OK, expenses.StatusCode);
            Assert.Empty(await DeserializeAsync(incomes));
            Assert.Empty(await DeserializeAsync(expenses));
        });
    }

    [Fact]
    public async Task routes_return_complete_serialized_entries_with_inclusive_filters_and_isolation()
    {
        var account = await Existing("Checking");
        var incomeCategory = await NewCategory("Salary", CategoryKind.Income, colorIndex: 4);
        var expenseCategory = await NewCategory("Food", CategoryKind.Expense, colorIndex: 7);
        var income = await IncomeRegistrarFor().RegisterAsync(
            new RegisterIncomeRequest(account.PublicId, incomeCategory.PublicId, 100m, Early, "pay"));
        var expense = await ExpenseRegistrarFor().RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, expenseCategory.PublicId, 25m, Middle, "lunch"));

        var otherAccount = await Existing("Other checking", householdId: 2);
        var otherIncomeAccount = await Existing("Other income", AccountType.Income, householdId: 2);
        var otherCategory = await NewCategory("Other salary", CategoryKind.Income, householdId: 2);
        database.Context.Entries.Add(JournalEntry.RegisterIncome(
            2, Middle, "other household", otherAccount, otherIncomeAccount, otherCategory, Euros(999m)));
        await database.Context.SaveChangesAsync();

        await RunApiAsync(async client =>
        {
            var incomeResponse = await client.GetAsync("/api/incomes?from=2026-09-01&to=2026-09-01");
            var expenseResponse = await client.GetAsync("/api/expenses?from=2026-09-09&to=2026-09-09");
            var incomes = await DeserializeAsync(incomeResponse);
            var expenses = await DeserializeAsync(expenseResponse);

            Assert.Equal(HttpStatusCode.OK, incomeResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, expenseResponse.StatusCode);
            var incomeLine = Assert.Single(incomes);
            var expenseLine = Assert.Single(expenses);
            Assert.Equal(income.Id, incomeLine.Id);
            Assert.Equal(Early, incomeLine.OccurredOn);
            Assert.Equal("pay", incomeLine.Description);
            Assert.Equal("income", incomeLine.Kind);
            Assert.Equal(100m, incomeLine.Amount);
            Assert.Equal(account.Name, incomeLine.AccountName);
            Assert.Equal(incomeCategory.Name, incomeLine.CategoryName);
            Assert.Equal(4, incomeLine.CategoryColorIndex);
            Assert.False(incomeLine.IsRecurring);
            Assert.Equal(expense.Id, expenseLine.Id);
            Assert.Equal(expenseCategory.Name, expenseLine.CategoryName);
            Assert.DoesNotContain(incomes, line => line.Description == "other household");
        });
    }

    [Fact]
    public async Task routes_reject_invalid_dates_and_inverted_ranges()
    {
        await RunApiAsync(async client =>
        {
            var invalid = await client.GetAsync("/api/incomes?from=not-a-date");
            var inverted = await client.GetAsync("/api/expenses?from=2026-09-10&to=2026-09-01");

            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, inverted.StatusCode);
        });
    }

    private static async Task<List<LedgerEntrySummary>> DeserializeAsync(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<List<LedgerEntrySummary>>(
                await response.Content.ReadAsStringAsync(), JsonOptions)
            ?? throw new InvalidOperationException("The endpoint returned no JSON collection.");
    }

    private async Task RunApiAsync(Func<HttpClient, Task> test)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(LedgerEndpoints).Assembly.GetName().Name,
        });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(database.Context);
        builder.Services.AddSingleton<ITenantContext>(new TestTenantContext(HouseholdId));
        builder.Services.AddSingleton<IHouseholdDirectory>(
            new TestHouseholdDirectory(CurrencyCode.Euro));
        builder.Services.AddSingleton<IDashboardQuery>(serviceProvider =>
            new DashboardQuery(
                serviceProvider.GetRequiredService<LedgerDbContext>(),
                serviceProvider.GetRequiredService<ITenantContext>(),
                serviceProvider.GetRequiredService<IHouseholdDirectory>()));
        foreach (var serviceType in typeof(LedgerEndpoints)
                     .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                     .SelectMany(method => method.GetParameters())
                     .Where(parameter => parameter.ParameterType.IsInterface)
                     .Select(parameter => parameter.ParameterType)
                     .Where(type => type != typeof(IDashboardQuery))
                     .Distinct())
        {
            builder.Services.AddSingleton(serviceType, _ =>
                throw new NotSupportedException("This endpoint is outside the contract under test."));
        }
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ValidationFailedExceptionHandler>();

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.MapLedgerEndpoints();
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        await test(client);
        await app.StopAsync();
    }

    private IncomeRegistrar IncomeRegistrarFor() =>
        new(database.Context, new TestTenantContext(HouseholdId),
            new RegisterIncomeRequestValidator(Clock),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private ExpenseRegistrar ExpenseRegistrarFor() =>
        new(database.Context, new TestTenantContext(HouseholdId),
            new RegisterExpenseRequestValidator(Clock),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private async Task<Account> Existing(
        string name,
        AccountType type = AccountType.Checking,
        int householdId = HouseholdId)
    {
        var account = Account.Create(householdId, name, type, CurrencyCode.Euro);
        database.Context.Accounts.Add(account);
        await database.Context.SaveChangesAsync();
        return account;
    }

    private async Task<Category> NewCategory(
        string name,
        CategoryKind kind,
        int householdId = HouseholdId,
        int colorIndex = 1)
    {
        var category = Category.Create(householdId, name, kind, colorIndex);
        database.Context.Categories.Add(category);
        await database.Context.SaveChangesAsync();
        return category;
    }
}
