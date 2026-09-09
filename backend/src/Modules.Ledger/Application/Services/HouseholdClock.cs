namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// What day it is where the household lives.
/// </summary>
/// <remarks>
/// "Not in the future" has to mean the same thing to the household as to the ledger: a member in
/// Madrid declaring a balance at 00:30 is not writing about tomorrow. Everything that compares a
/// date the user typed against today goes through here.
/// </remarks>
internal static class HouseholdClock
{
    /// <param name="timeZoneId">IANA or Windows identifier of the household's time zone.</param>
    /// <param name="clock">Source of the current instant.</param>
    /// <returns>Today, as the household would say it.</returns>
    /// <remarks>
    /// An unknown or broken time zone falls back to UTC rather than failing the request: the worst
    /// it costs is a few hours at the edge of a day, and refusing to work at all would be worse.
    /// </remarks>
    public static DateOnly TodayIn(string timeZoneId, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        TimeZoneInfo zone;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).Date);
    }
}
