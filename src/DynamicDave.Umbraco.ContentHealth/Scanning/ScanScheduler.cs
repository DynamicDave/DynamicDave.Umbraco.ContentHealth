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
        var candidate = DateTime.SpecifyKind(nowLocal.Date + time.ToTimeSpan(), DateTimeKind.Unspecified);

        var utc = ToUtc(candidate, timeZone);
        if (utc > nowUtc)
        {
            utc = ToUtc(candidate.AddDays(-1), timeZone);
        }

        return utc;
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        // A time inside the daylight saving gap does not exist; use the same time one hour later.
        if (timeZone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }
        // An ambiguous time during fall-back exists twice; use the first (earlier) occurrence.
        else if (timeZone.IsAmbiguousTime(local))
        {
            var offsets = timeZone.GetAmbiguousTimeOffsets(local);
            var firstOffset = offsets.Max(); // Maximum offset is the first (earlier) occurrence
            return DateTime.SpecifyKind(local - firstOffset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
