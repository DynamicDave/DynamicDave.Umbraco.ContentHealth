using DynamicDave.Umbraco.ContentHealth.Scanning;

namespace DynamicDave.Umbraco.ContentHealth.Models;

public sealed class BrokenLinkModel
{
    public required Guid SourceKey { get; init; }
    public required string SourceName { get; init; }
    /// <summary>Names from the root to the source, e.g. "Home / About / Team".</summary>
    public required string SourcePath { get; init; }
    public required string SourceDocumentTypeAlias { get; init; }
    public string? Culture { get; init; }
    public required string PropertyAlias { get; init; }
    public required string PropertyName { get; init; }
    /// <summary>True: the link is only in the draft, not in the published version.</summary>
    public required bool IsDraft { get; init; }
    public required Guid TargetKey { get; init; }
    public required LinkTargetType TargetType { get; init; }
    /// <summary>Null when the target no longer exists.</summary>
    public string? TargetName { get; init; }
    public required LinkStatus Status { get; init; }
    public required DateTime CheckedUtc { get; init; }
}

public sealed class BrokenLinksResponse
{
    public required int Total { get; init; }
    public required IReadOnlyList<BrokenLinkModel> Items { get; init; }
}

public sealed class IncomingLinkModel
{
    public required Guid SourceKey { get; init; }
    public required string SourceName { get; init; }
    public string? Culture { get; init; }
    public required string PropertyName { get; init; }
    public required bool IsDraft { get; init; }
    public required LinkStatus Status { get; init; }
}

public sealed class DocumentLinkHealthResponse
{
    public required IReadOnlyList<BrokenLinkModel> Outgoing { get; init; }
    /// <summary>One page of the visible links to the document, ordered by linking page.</summary>
    public required IReadOnlyList<IncomingLinkModel> Incoming { get; init; }
    /// <summary>Number of visible links to the document, across all pages.</summary>
    public required int IncomingTotal { get; init; }
    /// <summary>Number of linking pages the user may not see.</summary>
    public required int IncomingHiddenCount { get; init; }
}

public sealed class StatusCountModel
{
    public required LinkStatus Status { get; init; }
    public required int Live { get; init; }
    public required int Draft { get; init; }
}

public sealed class DocumentTypeOptionModel
{
    public required string Alias { get; init; }
    public required string Name { get; init; }
}

public sealed class ScanStatusModel
{
    public required ScanPhase Phase { get; init; }
    public required bool NeverScanned { get; init; }
    public DateTime? StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public required int DocumentsTotal { get; init; }
    public required int DocumentsDone { get; init; }
    public required int DocumentsFailed { get; init; }
    public string? LastError { get; init; }

    public static ScanStatusModel From(ScanState? state) => new()
    {
        Phase = state?.Phase ?? ScanPhase.Idle,
        NeverScanned = state?.StartedUtc is null,
        StartedUtc = AsUtc(state?.StartedUtc),
        CompletedUtc = AsUtc(state?.CompletedUtc),
        DocumentsTotal = state?.DocumentsTotal ?? 0,
        DocumentsDone = state?.DocumentsDone ?? 0,
        DocumentsFailed = state?.DocumentsFailed ?? 0,
        LastError = state?.LastError,
    };

    private static DateTime? AsUtc(DateTime? value) => value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}

public sealed class LinkHealthSummaryResponse
{
    /// <summary>Missing, Trashed and Unpublished, in that order.</summary>
    public required IReadOnlyList<StatusCountModel> Counts { get; init; }
    /// <summary>Document types of the pages with problems, for the filter.</summary>
    public required IReadOnlyList<DocumentTypeOptionModel> DocumentTypes { get; init; }
    public required ScanStatusModel Scan { get; init; }
    public required bool CanStartScan { get; init; }
}
