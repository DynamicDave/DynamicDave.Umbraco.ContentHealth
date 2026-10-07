using DynamicDave.Umbraco.ContentHealth.Scanning;
using DynamicDave.Umbraco.ContentHealth.Tests.Fakes;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class ScanStateStoreTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
    private readonly FakeKeyValueService _values = new();

    private ScanStateStore Store() => new(_values);

    [Fact]
    public void Empty_store_reads_as_never_scanned()
    {
        var (state, raw) = Store().Read();

        Assert.Null(state);
        Assert.Null(raw);
    }

    [Fact]
    public void Request_on_empty_store_requests_a_scan()
    {
        Assert.Equal(ScanRequestResult.Requested, Store().Request(Now));

        var state = Store().Read().State!;
        Assert.Equal(ScanPhase.Requested, state.Phase);
        Assert.Equal(Now, state.RequestedUtc);
    }

    [Fact]
    public void Request_while_running_is_refused_and_keeps_the_state()
    {
        Store().Save(new ScanState { Phase = ScanPhase.Running, HeartbeatUtc = Now.AddMinutes(-1), DocumentsDone = 40 });

        Assert.Equal(ScanRequestResult.AlreadyRunning, Store().Request(Now));
        Assert.Equal(40, Store().Read().State!.DocumentsDone);
    }

    [Fact]
    public void Request_while_a_dead_scan_is_running_requests_a_new_one()
    {
        Store().Save(new ScanState { Phase = ScanPhase.Running, HeartbeatUtc = Now.AddMinutes(-30) });

        Assert.Equal(ScanRequestResult.Requested, Store().Request(Now));
        Assert.Equal(ScanPhase.Requested, Store().Read().State!.Phase);
    }

    [Fact]
    public void Request_twice_is_idempotent()
    {
        Store().Request(Now);

        Assert.Equal(ScanRequestResult.Requested, Store().Request(Now.AddSeconds(5)));
        Assert.Equal(Now, Store().Read().State!.RequestedUtc);
    }

    [Fact]
    public void TryStart_claims_the_scan_and_resets_progress()
    {
        Store().Save(new ScanState { Phase = ScanPhase.Requested, DocumentsDone = 12, DocumentsFailed = 2, LastError = "old" });
        var (state, raw) = Store().Read();

        var running = Store().TryStart(raw, state, Now);

        Assert.NotNull(running);
        Assert.Equal(ScanPhase.Running, running.Phase);
        Assert.Equal(Now, running.StartedUtc);
        Assert.Equal(0, running.DocumentsDone);
        Assert.Null(running.LastError);
        Assert.Equal(running, Store().Read().State);
    }

    [Fact]
    public void TryStart_fails_when_the_state_changed_since_it_was_read()
    {
        Store().Save(new ScanState { Phase = ScanPhase.Requested });
        var (state, raw) = Store().Read();
        Store().Save(new ScanState { Phase = ScanPhase.Running, HeartbeatUtc = Now }); // another process won

        Assert.Null(Store().TryStart(raw, state, Now));
    }

    [Fact]
    public void Corrupt_value_reads_as_never_scanned_and_can_be_replaced()
    {
        _values.SetValue(Constants.ScanStateKey, "{not json");
        var (state, raw) = Store().Read();

        Assert.Null(state);
        Assert.NotNull(Store().TryStart(raw, state, Now));
    }
}
