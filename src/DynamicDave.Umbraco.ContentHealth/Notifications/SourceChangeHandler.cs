using DynamicDave.Umbraco.ContentHealth.Indexing;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace DynamicDave.Umbraco.ContentHealth.Notifications;

/// <summary>
/// Keeps the rows of a source document up to date. These notifications fire only on the server that made the change.
/// Failures are logged and never block saving or publishing; the next full scan repairs the index.
/// </summary>
internal sealed class SourceChangeHandler(LinkIndexer indexer, ILogger<SourceChangeHandler> logger) :
    INotificationHandler<ContentSavedNotification>,
    INotificationHandler<ContentPublishedNotification>,
    INotificationHandler<ContentUnpublishedNotification>,
    INotificationHandler<ContentMovedNotification>,
    INotificationHandler<ContentCopiedNotification>,
    INotificationHandler<ContentMovedToRecycleBinNotification>,
    INotificationHandler<ContentDeletedNotification>,
    INotificationHandler<ContentEmptiedRecycleBinNotification>
{
    public void Handle(ContentSavedNotification notification) => Reindex(notification.SavedEntities);

    public void Handle(ContentPublishedNotification notification) => Reindex(notification.PublishedEntities);

    public void Handle(ContentUnpublishedNotification notification) => Reindex(notification.UnpublishedEntities);

    // A move within the tree changes neither the links nor the publish state of a document; only a document restored
    // from the recycle bin lost its rows (when it was trashed). Descendants are in the collection too.
    public void Handle(ContentMovedNotification notification)
        => Reindex(notification.MoveInfoCollection.Where(m => WasInRecycleBin(m.OriginalPath)).Select(m => m.Entity));

    // A copy is saved without ContentSavedNotification; descendants raise their own notification.
    public void Handle(ContentCopiedNotification notification) => Reindex([notification.Copy]);

    // Descendants are in the collection too, so their rows are removed right away.
    public void Handle(ContentMovedToRecycleBinNotification notification) => Remove(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    public void Handle(ContentDeletedNotification notification) => Remove(notification.DeletedEntities.Select(c => c.Key));

    public void Handle(ContentEmptiedRecycleBinNotification notification) => Remove(notification.DeletedEntities.Select(c => c.Key));

    internal static bool WasInRecycleBin(string originalPath)
        => originalPath.Split(',').Contains(UmbracoConstants.System.RecycleBinContentString);

    private void Reindex(IEnumerable<IContent> documents)
    {
        foreach (var document in documents)
        {
            try
            {
                indexer.ReindexSource(document);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Content health could not index document {DocumentKey}; the next full scan retries it", document.Key);
            }
        }
    }

    private void Remove(IEnumerable<Guid> keys)
    {
        try
        {
            indexer.RemoveSources(keys.ToList());
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Content health could not remove links of deleted documents; the next full scan retries it");
        }
    }
}
