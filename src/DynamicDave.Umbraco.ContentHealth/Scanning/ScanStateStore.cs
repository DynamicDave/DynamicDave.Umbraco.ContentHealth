using System.Text.Json;
using System.Text.Json.Serialization;
using Umbraco.Cms.Core.Services;

namespace DynamicDave.Umbraco.ContentHealth.Scanning;

public enum ScanRequestResult
{
    Requested,
    AlreadyRunning,
}

/// <summary>
/// Scan state in umbracoKeyValue. Starting and requesting a scan use compare-and-set on the stored value, so two
/// processes never run a scan at the same time.
/// </summary>
public sealed class ScanStateStore(IKeyValueService keyValueService)
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>The state (null when never scanned or unreadable) and the raw value to pass to <see cref="TryStart"/>.</summary>
    public (ScanState? State, string? Raw) Read()
    {
        var raw = keyValueService.GetValue(Constants.ScanStateKey);
        return (Deserialize(raw), raw);
    }

    /// <summary>Asks the scan job to run a full scan. The job runs on the scheduling server and checks every minute.</summary>
    public ScanRequestResult Request(DateTime nowUtc)
    {
        var (current, raw) = Read();
        if (current is { Phase: ScanPhase.Requested })
        {
            return ScanRequestResult.Requested;
        }

        if (current is { Phase: ScanPhase.Running } && !ScanScheduler.IsStale(current, nowUtc))
        {
            return ScanRequestResult.AlreadyRunning;
        }

        var requested = (current ?? new ScanState()) with { Phase = ScanPhase.Requested, RequestedUtc = nowUtc };
        return TryReplace(raw, requested) ? ScanRequestResult.Requested : ScanRequestResult.AlreadyRunning;
    }

    /// <summary>Claims the scan for this process; null when another process changed the state after it was read.</summary>
    public ScanState? TryStart(string? raw, ScanState? current, DateTime nowUtc)
    {
        var running = (current ?? new ScanState()) with
        {
            Phase = ScanPhase.Running,
            StartedUtc = nowUtc,
            HeartbeatUtc = nowUtc,
            CompletedUtc = null,
            DocumentsTotal = 0,
            DocumentsDone = 0,
            DocumentsFailed = 0,
            LastError = null,
        };
        return TryReplace(raw, running) ? running : null;
    }

    /// <summary>Progress and completion of a scan this process owns.</summary>
    public void Save(ScanState state) => keyValueService.SetValue(Constants.ScanStateKey, JsonSerializer.Serialize(state, Json));

    private bool TryReplace(string? raw, ScanState next)
    {
        var json = JsonSerializer.Serialize(next, Json);

        // Compare-and-set needs an existing row; the very first write (fresh install) cannot race in practice.
        if (raw is null)
        {
            keyValueService.SetValue(Constants.ScanStateKey, json);
            return true;
        }

        return keyValueService.TrySetValue(Constants.ScanStateKey, raw, json);
    }

    private static ScanState? Deserialize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScanState>(raw, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
