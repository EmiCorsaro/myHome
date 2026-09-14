namespace MyHome.Modules.Ledger.Contracts.Dashboard;

/// <summary>
/// The month the dashboard is asked for, as the caller typed it (story 062).
/// </summary>
/// <param name="Year">The <c>year</c> query parameter, untouched; <see langword="null"/> when absent.</param>
/// <param name="Month">The <c>month</c> query parameter, untouched; <see langword="null"/> when absent.</param>
/// <remarks>
/// Both values travel as text on purpose. Binding them as integers would let the web framework
/// refuse "sept", "9.5" or the retired "2026-09-01" on its own, with its own error shape and
/// without saying which parameter failed. Keeping them raw lets the module refuse them with the
/// validation format the rest of the API already uses, naming the parameter (RF-12 to RF-15).
/// </remarks>
public sealed record DashboardMonthRequest(string? Year, string? Month)
{
    /// <summary>No month and no year: the household's current month (RF-3).</summary>
    public static DashboardMonthRequest CurrentMonth { get; } = new(null, null);
}
