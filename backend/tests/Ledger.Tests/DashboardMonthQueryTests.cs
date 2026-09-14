using System.Text.Json;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Asking the dashboard for a month by year and month, as story 062 specifies it. Every test names
/// the requirement it pins down; the map from RF to test lives in <c>progress/impl_062.md</c>.
/// The same rules seen over HTTP are in <see cref="DashboardEndpointTests"/>.
/// </summary>
public sealed class DashboardMonthQueryTests : IDisposable
{
    private const int OtherHouseholdId = 2;

    /// <summary>
    /// A registrar clock far enough ahead that every date these tests book counts as past: the
    /// periods under test run up to 2100, and "not in the future" is not what is being tested.
    /// </summary>
    private static readonly TimeProvider RegistrarClock =
        new FixedTimeProvider(new DateTimeOffset(2101, 6, 1, 10, 0, 0, TimeSpan.Zero));

    private readonly LedgerDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // RF-1, RF-2, RF-4, RF-5: year and month name a calendar month, first to last day inclusive,
    // including leap and non-leap February, December and January.
    [Theory(DisplayName = "A year and a month return the summary of that calendar month")]
    [InlineData("2026", "9", "2026-09-01", "2026-09-30")]
    [InlineData("2028", "2", "2028-02-01", "2028-02-29")]
    [InlineData("2026", "2", "2026-02-01", "2026-02-28")]
    [InlineData("2026", "12", "2026-12-01", "2026-12-31")]
    [InlineData("2027", "1", "2027-01-01", "2027-01-31")]
    public async Task a_year_and_a_month_return_the_summary_of_that_calendar_month(
        string year, string month, string expectedStart, string expectedEnd)
    {
        var summary = await DashboardFor().GetMonthlySummaryAsync(new DashboardMonthRequest(year, month));

        Assert.Equal(DateOnly.Parse(expectedStart, System.Globalization.CultureInfo.InvariantCulture), summary.PeriodStart);
        Assert.Equal(DateOnly.Parse(expectedEnd, System.Globalization.CultureInfo.InvariantCulture), summary.PeriodEnd);
    }

    // RF-2: February of a leap year ends on the 29th and includes that day's movements; nothing of
    // March 1st gets in.
    [Fact(DisplayName = "February of a leap year includes the 29th and nothing of March")]
    public async Task february_of_a_leap_year_includes_the_29th_and_nothing_of_march()
    {
        var (account, category) = await AccountAndCategory(HouseholdId);

        await Expense(HouseholdId, account, category, 10m, new DateOnly(2028, 2, 29));
        await Expense(HouseholdId, account, category, 99m, new DateOnly(2028, 3, 1));

        var summary = await DashboardFor().GetMonthlySummaryAsync(new DashboardMonthRequest("2028", "2"));

        Assert.Equal(new DateOnly(2028, 2, 29), summary.PeriodEnd);
        Assert.Equal(10m, summary.Expense);
        var entry = Assert.Single(summary.RecentEntries);
        Assert.Equal(new DateOnly(2028, 2, 29), entry.OccurredOn);
    }

    // RF-2: February of a non-leap year ends on the 28th; March 1st is already outside.
    [Fact(DisplayName = "February of a non-leap year ends on the 28th")]
    public async Task february_of_a_non_leap_year_ends_on_the_28th()
    {
        var (account, category) = await AccountAndCategory(HouseholdId);

        await Expense(HouseholdId, account, category, 10m, new DateOnly(2026, 2, 28));
        await Expense(HouseholdId, account, category, 99m, new DateOnly(2026, 3, 1));

        var summary = await DashboardFor().GetMonthlySummaryAsync(new DashboardMonthRequest("2026", "2"));

        Assert.Equal(new DateOnly(2026, 2, 28), summary.PeriodEnd);
        Assert.Equal(10m, summary.Expense);
    }

    // RF-2: December ends on the 31st and takes nothing of January 1st of the following year.
    [Fact(DisplayName = "December includes the 31st and nothing of the next year's January 1st")]
    public async Task december_includes_the_31st_and_nothing_of_the_next_years_january_1st()
    {
        var (account, category) = await AccountAndCategory(HouseholdId);

        await Expense(HouseholdId, account, category, 10m, new DateOnly(2026, 12, 31));
        await Expense(HouseholdId, account, category, 99m, new DateOnly(2027, 1, 1));

        var summary = await DashboardFor().GetMonthlySummaryAsync(new DashboardMonthRequest("2026", "12"));

        Assert.Equal(10m, summary.Expense);
        Assert.Equal(new DateOnly(2026, 12, 31), Assert.Single(summary.RecentEntries).OccurredOn);
    }

    // RF-2: January starts on the 1st and takes nothing of the previous December 31st.
    [Fact(DisplayName = "January includes the 1st and nothing of the previous December 31st")]
    public async Task january_includes_the_1st_and_nothing_of_the_previous_december_31st()
    {
        var (account, category) = await AccountAndCategory(HouseholdId);

        await Expense(HouseholdId, account, category, 99m, new DateOnly(2026, 12, 31));
        await Expense(HouseholdId, account, category, 10m, new DateOnly(2027, 1, 1));

        var summary = await DashboardFor().GetMonthlySummaryAsync(new DashboardMonthRequest("2027", "1"));

        Assert.Equal(10m, summary.Expense);
        Assert.Equal(new DateOnly(2027, 1, 1), Assert.Single(summary.RecentEntries).OccurredOn);
    }

    // RF-7: for the same month, asking by year and month answers exactly what asking by any day of
    // that month answered, field for field and value for value.
    [Theory(DisplayName = "Year and month answer exactly what the date-based query answered")]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(30)]
    public async Task year_and_month_answer_exactly_what_the_date_based_query_answered(int day)
    {
        await SeedSeptember2026();

        var dashboard = DashboardFor();

        var byDate = await dashboard.GetMonthlySummaryAsync(new DateOnly(2026, 9, day));
        var byYearAndMonth = await dashboard.GetMonthlySummaryAsync(new DashboardMonthRequest("2026", "9"));

        Assert.Equal(Json(byDate), Json(byYearAndMonth));
    }

    // RF-6: a leading zero names the same month.
    [Fact(DisplayName = "A month written with a leading zero is the same month")]
    public async Task a_month_written_with_a_leading_zero_is_the_same_month()
    {
        await SeedSeptember2026();

        var dashboard = DashboardFor();

        var padded = await dashboard.GetMonthlySummaryAsync(new DashboardMonthRequest("2026", "09"));
        var plain = await dashboard.GetMonthlySummaryAsync(new DashboardMonthRequest("2026", "9"));

        Assert.Equal(Json(plain), Json(padded));
        Assert.Equal(new DateOnly(2026, 9, 1), padded.PeriodStart);
    }

    // RF-3: with neither year nor month, the current month is the household's, not UTC's. At
    // 23:30 UTC on September 30th it is already October 1st in Madrid.
    [Fact(DisplayName = "Without year and month the household's current month is used, even when UTC is still in another month")]
    public async Task without_year_and_month_the_households_current_month_is_used()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero));
        var dashboard = DashboardFor(clock: clock, timeZoneId: TestTimeZones.Madrid);

        var summary = await dashboard.GetMonthlySummaryAsync(DashboardMonthRequest.CurrentMonth);

        Assert.Equal(new DateOnly(2026, 10, 1), summary.PeriodStart);
        Assert.Equal(new DateOnly(2026, 10, 31), summary.PeriodEnd);
        Assert.Equal(Json(await dashboard.GetMonthlySummaryAsync((DateOnly?)null)), Json(summary));
    }

    // RF-11: January 2015 and December 2100 are the accepted extremes.
    [Theory(DisplayName = "The first and last months of the accepted range are served")]
    [InlineData("2015", "1", "2015-01-01")]
    [InlineData("2100", "12", "2100-12-01")]
    public async Task the_first_and_last_months_of_the_accepted_range_are_served(
        string year, string month, string expectedStart)
    {
        var summary = await DashboardFor().GetMonthlySummaryAsync(new DashboardMonthRequest(year, month));

        Assert.Equal(DateOnly.Parse(expectedStart, System.Globalization.CultureInfo.InvariantCulture), summary.PeriodStart);
    }

    // RF-16: a future month inside the range is served with the figures it has — none recorded
    // yet here, and the one movement dated in it once it exists.
    [Fact(DisplayName = "A future month inside the range is served with the figures that belong to it")]
    public async Task a_future_month_inside_the_range_is_served_with_the_figures_that_belong_to_it()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero));
        var dashboard = DashboardFor(clock: clock);

        var empty = await dashboard.GetMonthlySummaryAsync(new DashboardMonthRequest("2031", "5"));

        Assert.Equal(new DateOnly(2031, 5, 1), empty.PeriodStart);
        Assert.Equal(0m, empty.Income);
        Assert.Equal(0m, empty.Expense);
        Assert.Equal(0m, empty.Net);
        Assert.Empty(empty.ByCategory);
        Assert.Empty(empty.RecentEntries);

        var (account, category) = await AccountAndCategory(HouseholdId);
        await Expense(HouseholdId, account, category, 25m, new DateOnly(2031, 5, 10));

        var withMovement = await dashboard.GetMonthlySummaryAsync(new DashboardMonthRequest("2031", "5"));

        Assert.Equal(25m, withMovement.Expense);
    }

    // RF-8, RF-9, RF-10, RF-11, RF-12, RF-13, RF-15: every invalid combination is refused before
    // any summary is built, and the error names exactly the parameters that caused it.
    [Theory(DisplayName = "Invalid year and month values are refused naming the offending parameters")]
    [InlineData(null, "9", "year")] // RF-8: month without year
    [InlineData("2026", null, "month")] // RF-9: year without month
    [InlineData("2026", "0", "month")] // RF-10
    [InlineData("2026", "13", "month")] // RF-10
    [InlineData("2026", "00", "month")] // RF-10
    [InlineData("2014", "1", "year")] // RF-11
    [InlineData("2101", "12", "year")] // RF-11
    [InlineData("2026", "sept", "month")] // RF-12: text
    [InlineData("dos mil", "9", "year")] // RF-12: text
    [InlineData("2026", "9.5", "month")] // RF-12: decimals
    [InlineData("2026", "9,0", "month")] // RF-12: decimals, comma
    [InlineData("2026.0", "9", "year")] // RF-12: decimals
    [InlineData("2026", "-1", "month")] // RF-10, RF-12: a sign is not a month
    [InlineData("2026", " 9", "month")] // RF-12: not a bare whole number
    [InlineData("2026", "", "month")] // RF-12: present but empty
    [InlineData("26", "9", "year")] // RF-4: not four digits
    [InlineData("02026", "9", "year")] // RF-4: not four digits
    [InlineData("2026", "2026-09-01", "month")] // RF-13: retired date format, with year
    [InlineData(null, "2026-09-01", "month,year")] // RF-13: retired date format alone
    [InlineData("abc", "13", "month,year")] // RF-15: both named when both fail
    public async Task invalid_year_and_month_values_are_refused_naming_the_offending_parameters(
        string? year, string? month, string expectedKeys)
    {
        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => DashboardFor().GetMonthlySummaryAsync(new DashboardMonthRequest(year, month)));

        Assert.Equal(
            expectedKeys.Split(','),
            error.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.All(error.Errors.Values, messages => Assert.NotEmpty(messages));
    }

    // RF-17: year and month choose a month, never a household: another household's movements in
    // the same month stay out, and each household sees only its own.
    [Fact(DisplayName = "Year and month only ever read the current household's data")]
    public async Task year_and_month_only_ever_read_the_current_households_data()
    {
        var (mine, myCategory) = await AccountAndCategory(HouseholdId);
        var (theirs, theirCategory) = await AccountAndCategory(OtherHouseholdId);

        await Expense(HouseholdId, mine, myCategory, 10m, new DateOnly(2026, 9, 5));
        await Expense(OtherHouseholdId, theirs, theirCategory, 700m, new DateOnly(2026, 9, 5));

        var request = new DashboardMonthRequest("2026", "9");

        var mySummary = await DashboardFor(HouseholdId).GetMonthlySummaryAsync(request);
        var theirSummary = await DashboardFor(OtherHouseholdId).GetMonthlySummaryAsync(request);

        Assert.Equal(10m, mySummary.Expense);
        Assert.Equal(myCategory.PublicId, Assert.Single(mySummary.ByCategory).CategoryId);
        Assert.DoesNotContain(mySummary.Accounts, a => a.Id == theirs.PublicId);

        Assert.Equal(700m, theirSummary.Expense);
        Assert.Equal(theirCategory.PublicId, Assert.Single(theirSummary.ByCategory).CategoryId);
    }

    internal static string Json(DashboardSummary summary) =>
        JsonSerializer.Serialize(summary, JsonSerializerOptions.Web);

    private DashboardQuery DashboardFor(
        int householdId = HouseholdId, TimeProvider? clock = null, string? timeZoneId = null) =>
        new(
            _database.Context,
            new TestTenantContext(householdId),
            new TestHouseholdDirectory(CurrencyCode.Euro, timeZoneId: timeZoneId ?? TestTimeZones.Madrid),
            new DashboardMonthRequestValidator(),
            clock ?? TimeProvider.System);

    private async Task SeedSeptember2026()
    {
        var (account, category) = await AccountAndCategory(HouseholdId);

        await Expense(HouseholdId, account, category, 42.35m, new DateOnly(2026, 9, 1));
        await Expense(HouseholdId, account, category, 10m, new DateOnly(2026, 9, 30));
        await Expense(HouseholdId, account, category, 5m, new DateOnly(2026, 10, 1));
    }

    private async Task<(Account Account, Category Category)> AccountAndCategory(int householdId)
    {
        var account = Account.Create(
            householdId, $"Cuenta {Guid.CreateVersion7()}", AccountType.Checking, CurrencyCode.Euro);
        var category = Category.Create(
            householdId, $"Gasto {Guid.CreateVersion7()}", CategoryKind.Expense, colorIndex: 1);

        _database.Context.Accounts.Add(account);
        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return (account, category);
    }

    private async Task Expense(
        int householdId, Account account, Category category, decimal amount, DateOnly occurredOn)
    {
        var registrar = new ExpenseRegistrar(
            _database.Context,
            new TestTenantContext(householdId),
            new RegisterExpenseRequestValidator(RegistrarClock),
            new TestHouseholdDirectory(CurrencyCode.Euro));

        await registrar.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, amount, occurredOn));
    }
}
