using DynamicDave.Umbraco.ContentHealth.Indexing;
using DynamicDave.Umbraco.ContentHealth.Notifications;
using DynamicDave.Umbraco.ContentHealth.Persistence;
using DynamicDave.Umbraco.ContentHealth.Scanning;
using DynamicDave.Umbraco.ContentHealth.Status;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace DynamicDave.Umbraco.ContentHealth.Composers;

public class ContentHealthComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<ContentHealthOptions>().Bind(builder.Config.GetSection(Constants.OptionsSection));
        builder.Services.TryAddSingleton(TimeProvider.System);

        builder.Services.AddSingleton<ITargetStateSource, UmbracoTargetStateSource>();
        builder.Services.AddSingleton<TargetStatusResolver>();
        builder.Services.AddSingleton<ContentLinkReader>();
        builder.Services.AddSingleton<LinkIndexRepository>();
        builder.Services.AddSingleton<LinkIndexer>();

        builder.Services.AddSingleton<ScanStateStore>();
        builder.Services.AddSingleton<FullScanner>();
        builder.Services.AddRecurringBackgroundJob<FullScanJob>();

        builder.AddNotificationHandler<ContentSavedNotification, SourceChangeHandler>();
        builder.AddNotificationHandler<ContentPublishedNotification, SourceChangeHandler>();
        builder.AddNotificationHandler<ContentUnpublishedNotification, SourceChangeHandler>();
        builder.AddNotificationHandler<ContentMovedNotification, SourceChangeHandler>();
        builder.AddNotificationHandler<ContentMovedToRecycleBinNotification, SourceChangeHandler>();
        builder.AddNotificationHandler<ContentDeletedNotification, SourceChangeHandler>();
        builder.AddNotificationHandler<ContentEmptiedRecycleBinNotification, SourceChangeHandler>();

        builder.AddNotificationHandler<ContentCacheRefresherNotification, TargetChangeHandler>();
        builder.AddNotificationHandler<MediaCacheRefresherNotification, TargetChangeHandler>();
    }
}
