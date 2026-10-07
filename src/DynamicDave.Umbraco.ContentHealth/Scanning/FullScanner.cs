using DynamicDave.Umbraco.ContentHealth.Indexing;
using DynamicDave.Umbraco.ContentHealth.Models;
using DynamicDave.Umbraco.ContentHealth.Persistence;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace DynamicDave.Umbraco.ContentHealth.Scanning;

public sealed class FullScanner(
    IContentService contentService,
    IDocumentNavigationQueryService documentNavigation,
    LinkIndexer indexer,
    LinkIndexRepository repository,
    ScanStateStore store,
    TimeProvider timeProvider,
    ILogger<FullScanner> logger)
{
    private const int PageSize = 200;

    /// <summary>Reindexes every document, then removes the rows of documents that no longer exist or are in the recycle bin.</summary>
    public void Run(ScanState running)
    {
        var visited = new HashSet<Guid>();
        int done = 0, indexed = 0, failed = 0;
        long pageIndex = 0, total;
        var state = running;

        do
        {
            var page = contentService.GetPagedDescendants(UmbracoConstants.System.Root, pageIndex++, PageSize, out total).ToList();
            foreach (var content in page)
            {
                done++;
                if (content.Trashed)
                {
                    continue;
                }

                // A document that fails keeps its previous rows.
                visited.Add(content.Key);
                try
                {
                    indexer.ReindexSource(content);
                    indexed++;
                }
                catch (Exception exception)
                {
                    failed++;
                    logger.LogWarning(exception, "Content health could not check document {DocumentKey}", content.Key);
                }
            }

            state = state with { DocumentsTotal = (int)total, DocumentsDone = done, DocumentsFailed = failed, HeartbeatUtc = UtcNow() };
            store.Save(state);
        }
        while (pageIndex * PageSize < total);

        var stale = SelectStaleSources(repository.GetDistinctSourceKeys(), visited, key => documentNavigation.TryGetParentKey(key, out _));
        if (stale.Count > 0)
        {
            repository.RemoveSources(stale);
        }

        var removed = stale.Count;
        var completed = state with { Phase = ScanPhase.Idle, CompletedUtc = UtcNow(), HeartbeatUtc = UtcNow(), LastError = null };
        store.Save(completed);
        LogSummary(completed, indexed, removed);
    }

    /// <summary>
    /// Offset paging can skip documents when content changes during the scan, so a source is only stale when it was
    /// not visited and no longer exists outside the recycle bin.
    /// </summary>
    internal static IReadOnlyList<Guid> SelectStaleSources(IEnumerable<Guid> indexedSourceKeys, IReadOnlySet<Guid> visited, Func<Guid, bool> existsInTree)
        => indexedSourceKeys.Where(key => !visited.Contains(key) && !existsInTree(key)).ToList();

    private void LogSummary(ScanState completed, int indexed, int removedSources)
    {
        var problems = repository.GetProblems();
        int Count(LinkStatus status, bool draft) => problems.Count(p => p.Status == (int)status && p.IsDraft == draft);

        logger.LogInformation(
            "Content health scan finished in {Seconds}s: {Indexed} documents indexed, {Failed} failed, {Removed} stale sources removed. Problems (live/draft): Missing {MissingLive}/{MissingDraft}, Trashed {TrashedLive}/{TrashedDraft}, Unpublished {UnpublishedLive}/{UnpublishedDraft}",
            (int)(completed.CompletedUtc!.Value - completed.StartedUtc!.Value).TotalSeconds,
            indexed,
            completed.DocumentsFailed,
            removedSources,
            Count(LinkStatus.Missing, false),
            Count(LinkStatus.Missing, true),
            Count(LinkStatus.Trashed, false),
            Count(LinkStatus.Trashed, true),
            Count(LinkStatus.Unpublished, false),
            Count(LinkStatus.Unpublished, true));
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
