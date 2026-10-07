using System.Text.Json.Serialization;

namespace DynamicDave.Umbraco.ContentHealth.Scanning;

[JsonConverter(typeof(JsonStringEnumConverter<ScanPhase>))]
public enum ScanPhase
{
    Idle,
    Requested,
    Running,
}

/// <summary>Progress and result of the full scan, stored as JSON in umbracoKeyValue.</summary>
public sealed record ScanState
{
    public ScanPhase Phase { get; init; }
    public DateTime? RequestedUtc { get; init; }
    public DateTime? StartedUtc { get; init; }
    public DateTime? HeartbeatUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public int DocumentsTotal { get; init; }
    public int DocumentsDone { get; init; }
    public int DocumentsFailed { get; init; }
    public string? LastError { get; init; }
}
