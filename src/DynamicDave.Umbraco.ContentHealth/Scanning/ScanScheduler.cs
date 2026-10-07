namespace DynamicDave.Umbraco.ContentHealth.Scanning;

internal static class ScanScheduler
{
    /// <summary>A running scan without a heartbeat for this long is considered dead and may be taken over.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    public static bool IsStale(ScanState state, DateTime nowUtc)
        => state.HeartbeatUtc is null || nowUtc - state.HeartbeatUtc.Value > StaleAfter;

    public static bool ShouldStart(ScanState? state, DateTime nowUtc, TimeOnly? nightlyTime, TimeZoneInfo timeZone)
    {
        if (state is null)
        {
            return true; // never scanned: first scan after installation
        }

        switch (state.Phase)
        {
            case ScanPhase.Requested:
                return true;
            case ScanPhase.Running:
                return IsStale(state, nowUtc);
        }

        if (nightlyTime is null)
        {
            return false;
        }

        return state.StartedUtc is null || state.StartedUtc.Value < LastScheduledUtc(nowUtc, nightlyTime.Value, timeZone);
    }

    /// <summary>The most recent occurrence of <paramref name="time"/> (server local time) at or before now, in UTC.</summary>
    public static DateTime LastScheduledUtc(DateTime nowUtc, TimeOnly time, TimeZoneInfo timeZone)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var scheduled = DateTime.SpecifyKind(nowLocal.Date + time.ToTimeSpan(), DateTimeKind.Unspecified);
        if (scheduled > nowLocal)
        {
            scheduled = scheduled.AddDays(-1);
        }

        // A time inside the daylight saving gap does not exist; use the same time one hour later.
        if (timeZone.IsInvalidTime(scheduled))
        {
            scheduled = scheduled.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(scheduled, timeZone);
    }
}
