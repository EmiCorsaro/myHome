using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHome.Api.Endpoints;
using MyHome.Api.ErrorHandling;
using MyHome.Modules.Ledger;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// <c>GET /api/dashboard</c> over real HTTP, as story 062 specifies its query parameters. Kestrel
/// serves the API's own endpoint mapping, JSON options and validation error handler; only the
/// database (SQLite instead of PostgreSQL), the tenant and the clock are stood in for. The map from
/// RF to test lives in <c>progress/impl_062.md</c>.
/// </summary>
public sealed class DashboardEndpointTests : IDisposable
{
    /// <summary>23:30 UTC on September 30th: already October 1st in the household's Madrid.</summary>
    private static readonly TimeProvider EndOfSeptemberUtc =
        new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero));

    private static readonly TimeProvider RegistrarClock =
        new FixedTimeProvider(new DateTimeOffset(2101, 6, 1, 10, 0, 0, TimeSpan.Zero));

    private readonly LedgerDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // RF-1, RF-4, RF-5: year and month as two integer query parameters name the month served.
    // RF-11: the extremes of the accepted range are served too.
    [Theory(DisplayName = "GET /api/dashboard with year and month returns that month's summary")]
    [InlineData("?year=2026&month=9", "2026-09-01", "2026-09-30")]
    [InlineData("?year=2028&month=2", "2028-02-01", "2028-02-29")]
    [InlineData("?year=2015&month=1", "2015-01-01", "2015-01-31")]
    [InlineData("?year=2100&month=12", "2100-12-01", "2100-12-31")]
    [InlineData("?month=3&year=2031", "2031-03-01", "2031-03-31")]
    public async Task get_with_year_and_month_returns_that_months_summary(
        string query, string expectedStart, string expectedEnd)
    {
        await using var host = await DashboardHost.StartAsync(this);

        using var response = await host.Client.GetAsync(new Uri($"/api/dashboard{query}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedStart, body.RootElement.GetProperty("periodStart").GetString());
        Assert.Equal(expectedEnd, body.RootElement.GetProperty("periodEnd").GetString());
    }

    // RF-7: the body served for year and month is byte for byte what the endpoint served for the
    // date-based query of that month — the same summary through the same JSON options.
    // RF-6: "09" and "9" are served the same body.
    [Fact(DisplayName = "GET /api/dashboard by year and month serves exactly the body the date-based query served")]
    public async Task get_by_year_and_month_serves_exactly_the_body_the_date_based_query_served()
    {
        await SeedSeptember2026();
        await using var host = await DashboardHost.StartAsync(this);

        var byDate = await host.Dashboard().GetMonthlySummaryAsync(new DateOnly(2026, 9, 17));
        var expected = JsonSerializer.Serialize(byDate, host.JsonOptions);

        var plain = await host.Client.GetStringAsync(new Uri("/api/dashboard?year=2026&month=9", UriKind.Relative));
        var padded = await host.Client.GetStringAsync(new Uri("/api/dashboard?year=2026&month=09", UriKind.Relative));

        Assert.Equal(expected, plain);
        Assert.Equal(expected, padded);
        Assert.Contains("\"expense\":52.35", plain, StringComparison.Ordinal);
    }

    // RF-3: with no parameters at all the household's current month is still served.
    [Fact(DisplayName = "GET /api/dashboard without parameters serves the household's current month")]
    public async Task get_without_parameters_serves_the_households_current_month()
    {
        await using var host = await DashboardHost.StartAsync(this);

        using var response = await host.Client.GetAsync(new Uri("/api/dashboard", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("2026-10-01", body.RootElement.GetProperty("periodStart").GetString());
        Assert.Equal("2026-10-31", body.RootElement.GetProperty("periodEnd").GetString());
    }

    // RF-8 to RF-13: every invalid query is a 400 with no summary in it. RF-14: in the API's
    // validation problem format. RF-15: naming exactly the offending parameters.
    [Theory(DisplayName = "GET /api/dashboard with invalid parameters is a validation problem naming them")]
    [InlineData("?month=9", "year")] // RF-8
    [InlineData("?year=2026", "month")] // RF-9
    [InlineData("?year=2026&month=0", "month")] // RF-10
    [InlineData("?year=2026&month=13", "month")] // RF-10
    [InlineData("?year=2014&month=1", "year")] // RF-11
    [InlineData("?year=2101&month=12", "year")] // RF-11
    [InlineData("?year=2026&month=sept", "month")] // RF-12
    [InlineData("?year=dos%20mil&month=9", "year")] // RF-12
    [InlineData("?year=2026&month=9.5", "month")] // RF-12
    [InlineData("?year=2026.0&month=9", "year")] // RF-12
    [InlineData("?year=2026&month=", "month")] // RF-9, RF-12: an empty month is no month
    [InlineData("?month=2026-09-01", "month,year")] // RF-13, retired format alone
    [InlineData("?year=2026&month=2026-09-01", "month")] // RF-13, retired format with year
    public async Task get_with_invalid_parameters_is_a_validation_problem_naming_them(
        string query, string expectedKeys)
    {
        await using var host = await DashboardHost.StartAsync(this);

        using var response = await host.Client.GetAsync(new Uri($"/api/dashboard{query}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Equal("Invalid request", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal(
            expectedKeys.Split(','),
            root.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.All(
            root.GetProperty("errors").EnumerateObject(),
            p => Assert.NotEqual(0, p.Value.GetArrayLength()));

        Assert.False(root.TryGetProperty("periodStart", out _));
        Assert.False(root.TryGetProperty("income", out _));
    }

    private DashboardQuery NewDashboard() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new TestHouseholdDirectory(CurrencyCode.Euro, timeZoneId: TestTimeZones.Madrid),
            new DashboardMonthRequestValidator(),
            EndOfSeptemberUtc);

    private async Task SeedSeptember2026()
    {
        var account = Account.Create(HouseholdId, "Santander conjunta", AccountType.Checking, CurrencyCode.Euro);
        var category = Category.Create(HouseholdId, "Supermercado", CategoryKind.Expense, colorIndex: 1);

        _database.Context.Accounts.Add(account);
        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        var registrar = new ExpenseRegistrar(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new RegisterExpenseRequestValidator(RegistrarClock),
            new TestHouseholdDirectory(CurrencyCode.Euro));

        foreach (var (amount, day) in new[] { (42.35m, new DateOnly(2026, 9, 1)), (10m, new DateOnly(2026, 9, 30)), (5m, new DateOnly(2026, 10, 1)) })
        {
            await registrar.RegisterAsync(new RegisterExpenseRequest(account.PublicId, category.PublicId, amount, day));
        }
    }

    /// <summary>
    /// The API's dashboard surface on an ephemeral local port: <c>MapLedgerEndpoints</c>, the JSON
    /// options and exception handlers <c>Program</c> registers, and the ledger module's services
    /// with the dashboard query pointed at the test's database.
    /// </summary>
    private sealed class DashboardHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly DashboardEndpointTests _owner;

        private DashboardHost(WebApplication app, DashboardEndpointTests owner, HttpClient client)
        {
            _app = app;
            _owner = owner;
            Client = client;
        }

        public HttpClient Client { get; }

        public JsonSerializerOptions JsonOptions =>
            _app.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        public DashboardQuery Dashboard() => _owner.NewDashboard();

        public static async Task<DashboardHost> StartAsync(DashboardEndpointTests owner)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });

            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();

            builder.Services.AddLedgerModule();
            builder.Services.AddScoped<IDashboardQuery>(_ => owner.NewDashboard());

            // As Program registers them.
            builder.Services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<ClientDisconnectedExceptionHandler>();
            builder.Services.AddExceptionHandler<ValidationFailedExceptionHandler>();

            var app = builder.Build();

            app.UseExceptionHandler();
            app.UseStatusCodePages();
            app.MapLedgerEndpoints();

            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!
                .Addresses.First();

            return new DashboardHost(app, owner, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
