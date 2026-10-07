using DynamicDave.Umbraco.ContentHealth.Scanning;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class ScanSchedulerTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TimeOnly Three = new(3, 0);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static ScanState Idle(DateTime startedUtc) => new() { Phase = ScanPhase.Idle, StartedUtc = startedUtc, CompletedUtc = startedUtc.AddMinutes(1) };

    [Fact]
    public void No_state_starts_the_first_scan()
        => Assert.True(ScanScheduler.ShouldStart(null, Now, null, Utc));

    [Fact]
    public void Requested_scan_starts()
        => Assert.True(ScanScheduler.ShouldStart(new ScanState { Phase = ScanPhase.Requested }, Now, null, Utc));

    [Fact]
    public void Running_scan_with_recent_heartbeat_is_left_alone()
        => Assert.False(ScanScheduler.ShouldStart(new ScanState { Phase = ScanPhase.Running, HeartbeatUtc = Now.AddMinutes(-2) }, Now, Three, Utc));

    [Fact]
    public void Running_scan_with_stale_heartbeat_is_taken_over()
        => Assert.True(ScanScheduler.ShouldStart(new ScanState { Phase = ScanPhase.Running, HeartbeatUtc = Now.AddMinutes(-11) }, Now, Three, Utc));

    [Fact]
    public void Idle_without_nightly_time_does_not_start()
        => Assert.False(ScanScheduler.ShouldStart(Idle(Now.AddDays(-5)), Now, null, Utc));

    [Fact]
    public void Idle_starts_when_no_scan_started_since_the_last_scheduled_time()
        => Assert.True(ScanScheduler.ShouldStart(Idle(new DateTime(2026, 10, 6, 3, 1, 0, DateTimeKind.Utc)), Now, Three, Utc));

    [Fact]
    public void Idle_does_not_start_when_a_scan_started_after_the_last_scheduled_time()
        => Assert.False(ScanScheduler.ShouldStart(Idle(new DateTime(2026, 10, 7, 3, 1, 0, DateTimeKind.Utc)), Now, Three, Utc));

    [Fact]
    public void Before_todays_time_the_previous_day_counts()
    {
        var now = new DateTime(2026, 10, 7, 2, 0, 0, DateTimeKind.Utc);

        Assert.False(ScanScheduler.ShouldStart(Idle(new DateTime(2026, 10, 6, 3, 1, 0, DateTimeKind.Utc)), now, Three, Utc));
        Assert.True(ScanScheduler.ShouldStart(Idle(new DateTime(2026, 10, 6, 2, 59, 0, DateTimeKind.Utc)), now, Three, Utc));
    }

    [Fact]
    public void Scheduled_time_is_server_local_time()
    {
        var amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");

        // 03:00 CEST on 7 October 2026 is 01:00 UTC.
        Assert.Equal(new DateTime(2026, 10, 7, 1, 0, 0, DateTimeKind.Utc), ScanScheduler.LastScheduledUtc(Now, Three, amsterdam));
    }

    [Fact]
    public void Nightly_time_in_daylight_saving_gap_does_not_throw()
    {
        var amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
        var now = new DateTime(2026, 3, 29, 5, 0, 0, DateTimeKind.Utc); // clocks went from 02:00 to 03:00 that night

        var scheduled = ScanScheduler.LastScheduledUtc(now, new TimeOnly(2, 30), amsterdam);

        Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), scheduled); // shifted to 03:30 CEST
        Assert.True(ScanScheduler.ShouldStart(Idle(new DateTime(2026, 3, 28, 1, 0, 0, DateTimeKind.Utc)), now, new TimeOnly(2, 30), amsterdam));
    }

    [Fact]
    public void Spring_forward_scheduled_time_never_after_now()
    {
        var amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
        var now = new DateTime(2026, 3, 29, 1, 10, 0, DateTimeKind.Utc); // 03:10 CEST (clocks went from 02:00 to 03:00)

        var scheduled = ScanScheduler.LastScheduledUtc(now, new TimeOnly(2, 30), amsterdam);

        // Scheduled 02:30 (invalid, shifts to 03:30 CEST) on today would be 01:30 UTC > now
        // So it should go back to yesterday's 03:30 CET = 01:30 UTC
        Assert.Equal(new DateTime(2026, 3, 28, 1, 30, 0, DateTimeKind.Utc), scheduled);
        Assert.True(scheduled <= now);

        // Scan started at 01:00 UTC (03:00 CEST) should not start again before 02:30 (03:30 CEST = 01:30 UTC)
        Assert.False(ScanScheduler.ShouldStart(Idle(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc)), now, new TimeOnly(2, 30), amsterdam));
    }

    [Fact]
    public void Fall_back_scheduled_time_uses_first_occurrence()
    {
        var amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
        var now = new DateTime(2026, 10, 25, 0, 40, 0, DateTimeKind.Utc); // 02:40 CEST (first occurrence)

        var scheduled = ScanScheduler.LastScheduledUtc(now, new TimeOnly(2, 30), amsterdam);

        // 02:30 is ambiguous on fall-back day; use the first (earlier) occurrence: 02:30 CEST = 00:30 UTC
        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), scheduled);
    }

    [Fact]
    public void Fall_back_no_repeat_in_second_hour()
    {
        var amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
        var now = new DateTime(2026, 10, 25, 1, 40, 0, DateTimeKind.Utc); // 02:40 CET (second occurrence, after clocks went back)

        // Even in the second occurrence, the last scheduled time is at 02:30 CEST (first occurrence) = 00:30 UTC
        var scheduled = ScanScheduler.LastScheduledUtc(now, new TimeOnly(2, 30), amsterdam);
        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), scheduled);

        // Scan started at 00:40 UTC (02:40 CEST, first occurrence) is after the last scheduled 00:30 UTC, so no repeat
        Assert.False(ScanScheduler.ShouldStart(Idle(new DateTime(2026, 10, 25, 0, 40, 0, DateTimeKind.Utc)), now, new TimeOnly(2, 30), amsterdam));
    }
}
