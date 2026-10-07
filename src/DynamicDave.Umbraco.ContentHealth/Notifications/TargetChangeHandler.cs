using DynamicDave.Umbraco.ContentHealth.Indexing;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services.Changes;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Sync;

namespace DynamicDave.Umbraco.ContentHealth.Notifications;

/// <summary>
/// Recalculates the status of links to documents and media that changed. Cache refresher notifications are published
/// after Umbraco has updated its in-memory navigation and publish status, so the status checks see the new state.
/// </summary>
internal sealed class TargetChangeHandler(
    LinkIndexer indexer,
    IDocumentNavigationQueryService documentNavigation,
    IMediaNavigationQueryService mediaNavigation,
    IServerRoleAccessor serverRoleAccessor,
    ILogger<TargetChangeHandler> logger) :
    INotificationHandler<ContentCacheRefresherNotification>,
    INotificationHandler<MediaCacheRefresherNotification>
{
    public void Handle(ContentCacheRefresherNotification notification)
        => Process(
            notification.MessageObject is ContentCacheRefresher.JsonPayload[] payloads
                ? payloads.Where(p => !p.Blueprint).Select(p => new Change(p.Key, p.ChangeTypes)).ToList()
                : null,
            documentNavigation,
            documentNavigation);

    public void Handle(MediaCacheRefresherNotification notification)
        => Process(
            notification.MessageObject is MediaCacheRefresher.JsonPayload[] payloads
                ? payloads.Select(p => new Change(p.Key, p.ChangeTypes)).ToList()
                : null,
            mediaNavigation,
            mediaNavigation);

    /// <param name="changes">Null when the notification has no payloads (e.g. refresh all).</param>
    private void Process(IReadOnlyCollection<Change>? changes, INavigationQueryService tree, IRecycleBinNavigationQueryService bin)
    {
        // Published on every server; one writer is enough and keeps updates from racing.
        if (serverRoleAccessor.CurrentServerRole == ServerRole.Subscriber)
        {
            return;
        }

        try
        {
            // Descendants of a removed item are no longer in the navigation, so recalculate everything. The checks are
            // in-memory and only changed rows are written.
            if (changes is null || changes.Any(c => c.Key is null || (c.Types & (TreeChangeTypes.RefreshAll | TreeChangeTypes.Remove)) != 0))
            {
                indexer.RefreshAllTargets();
                return;
            }

            var keys = new HashSet<Guid>();
            foreach (var change in changes)
            {
                var key = change.Key!.Value;
                keys.Add(key);
                if ((change.Types & TreeChangeTypes.RefreshBranch) != 0
                    && (tree.TryGetDescendantsKeys(key, out var descendants) || bin.TryGetDescendantsKeysInBin(key, out descendants)))
                {
                    keys.UnionWith(descendants);
                }
            }

            if (keys.Count > 0)
            {
                indexer.RefreshTargets(keys);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Content health could not update link statuses after a content or media change; the next full scan repairs them");
        }
    }

    private readonly record struct Change(Guid? Key, TreeChangeTypes Types);
}
