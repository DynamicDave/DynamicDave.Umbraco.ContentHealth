using DynamicDave.Umbraco.ContentHealth.Indexing;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

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
    INotificationHandler<ContentMovedToRecycleBinNotification>,
    INotificationHandler<ContentDeletedNotification>,
    INotificationHandler<ContentEmptiedRecycleBinNotification>
{
    public void Handle(ContentSavedNotification notification) => Reindex(notification.SavedEntities);

    public void Handle(ContentPublishedNotification notification) => Reindex(notification.PublishedEntities);

    public void Handle(ContentUnpublishedNotification notification) => Reindex(notification.UnpublishedEntities);

    public void Handle(ContentMovedNotification notification) => Reindex(notification.MoveInfoCollection.Select(m => m.Entity));

    // Descendants keep their rows until the next full scan; sources in the recycle bin are hidden when querying.
    public void Handle(ContentMovedToRecycleBinNotification notification) => Remove(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    public void Handle(ContentDeletedNotification notification) => Remove(notification.DeletedEntities.Select(c => c.Key));

    public void Handle(ContentEmptiedRecycleBinNotification notification) => Remove(notification.DeletedEntities.Select(c => c.Key));

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
