using DynamicDave.Umbraco.ContentHealth.Indexing;
using DynamicDave.Umbraco.ContentHealth.Status;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Changes;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Sync;

namespace DynamicDave.Umbraco.ContentHealth.Notifications;

/// <summary>
/// Recalculates the status of links to documents and media that changed. Cache refresher notifications are published
/// after Umbraco has updated its in-memory navigation and publish status, so the status checks see the new state
/// (except after a restore from the recycle bin on Umbraco 17.2+, which this handler repairs first).
/// </summary>
internal sealed class TargetChangeHandler(
    LinkIndexer indexer,
    IDocumentNavigationQueryService documentNavigation,
    IMediaNavigationQueryService mediaNavigation,
    IPublishStatusManagementService publishStatusManagement,
    IServerRoleAccessor serverRoleAccessor,
    ILogger<TargetChangeHandler> logger) :
    INotificationHandler<ContentCacheRefresherNotification>,
    INotificationHandler<MediaCacheRefresherNotification>
{
    public void Handle(ContentCacheRefresherNotification notification)
    {
        var payloads = notification.MessageObject as ContentCacheRefresher.JsonPayload[];
        Process(
            payloads?.Where(p => !p.Blueprint).Select(p => new Change(p.Key, p.ChangeTypes)).ToList(),
            documentNavigation,
            documentNavigation,
            payloads is null ? null : () => ReloadPublishStatus(payloads));
    }

    public void Handle(MediaCacheRefresherNotification notification)
        => Process(
            notification.MessageObject is MediaCacheRefresher.JsonPayload[] payloads
                ? payloads.Select(p => new Change(p.Key, p.ChangeTypes)).ToList()
                : null,
            mediaNavigation,
            mediaNavigation);

    /// <summary>
    /// The keys of tree changes that can change the publish status without telling Umbraco's publish status cache: a
    /// branch refresh without cultures, which is what a restore from the recycle bin raises.
    /// </summary>
    internal static IReadOnlyList<Guid> KeysNeedingPublishStatusReload(IEnumerable<ContentCacheRefresher.JsonPayload> payloads)
        => payloads
            .Where(p => !p.Blueprint
                && p.Key is not null
                && (p.ChangeTypes & TreeChangeTypes.RefreshBranch) != 0
                && (p.ChangeTypes & (TreeChangeTypes.Remove | TreeChangeTypes.RefreshAll)) == 0
                && (p.PublishedCultures?.Length ?? 0) == 0
                && (p.UnpublishedCultures?.Length ?? 0) == 0)
            .Select(p => p.Key!.Value)
            .ToList();

    // Umbraco 17.2+ does not refresh its publish status cache for tree changes without cultures. A restore from the
    // recycle bin unpublishes the document without them, so the cache would keep saying "published".
    // Failures are logged per key, so the status recalculation still runs.
    private void ReloadPublishStatus(IEnumerable<ContentCacheRefresher.JsonPayload> payloads)
    {
        var keys = KeysNeedingPublishStatusReload(payloads);
        if (keys.Count == 0)
        {
            return;
        }

        Func<Guid, CancellationToken, Task> reload;
        try
        {
            // Declared on IPublishStatusManagementService in Umbraco 17 but on its new base interface in 18, so bind
            // it by name, only when needed.
            reload = InterfaceMethod.Bind<Func<Guid, CancellationToken, Task>>(
                publishStatusManagement,
                typeof(IPublishStatusManagementService),
                nameof(IPublishStatusManagementService.AddOrUpdateStatusWithDescendantsAsync));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Content health could not reload Umbraco's publish status after a restore; links to restored documents may count as published until they change again");
            return;
        }

        foreach (var key in keys)
        {
            try
            {
                reload(key, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Content health could not reload Umbraco's publish status of document {DocumentKey}", key);
            }
        }
    }

    /// <param name="changes">Null when the notification has no payloads (e.g. refresh all).</param>
    /// <param name="prepare">Runs before the statuses are recalculated, on the server that does the recalculation.</param>
    private void Process(
        IReadOnlyCollection<Change>? changes,
        INavigationQueryService tree,
        IRecycleBinNavigationQueryService bin,
        Action? prepare = null)
    {
        // Published on every server; one writer is enough and keeps updates from racing.
        if (serverRoleAccessor.CurrentServerRole == ServerRole.Subscriber)
        {
            return;
        }

        try
        {
            prepare?.Invoke();

            if (changes is null || changes.Any(c => (c.Types & TreeChangeTypes.RefreshAll) != 0))
            {
                indexer.RefreshAllTargets();
                return;
            }

            // Descendants of a removed item are no longer in the navigation, so look for every target that vanished.
            if (changes.Any(c => c.Key is null || (c.Types & TreeChangeTypes.Remove) != 0))
            {
                indexer.RefreshVanishedTargets();
            }

            var keys = new HashSet<Guid>();
            foreach (var change in changes.Where(c => c.Key is not null && (c.Types & TreeChangeTypes.Remove) == 0))
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
