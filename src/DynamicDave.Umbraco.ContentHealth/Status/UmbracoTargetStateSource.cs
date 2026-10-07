using DynamicDave.Umbraco.ContentHealth.Models;
using Umbraco.Cms.Core.Services.Navigation;

namespace DynamicDave.Umbraco.ContentHealth.Status;

/// <summary>
/// Reads Umbraco's in-memory navigation and publish status. Both are updated by the content and media cache
/// refreshers before ContentCacheRefresherNotification / MediaCacheRefresherNotification is published.
/// </summary>
public sealed class UmbracoTargetStateSource(
    IDocumentNavigationQueryService documentNavigation,
    IMediaNavigationQueryService mediaNavigation,
    IPublishStatusQueryService publishStatus) : ITargetStateSource
{
    public TargetPresence GetPresence(Guid key, LinkTargetType type)
    {
        bool inTree, inBin;
        if (type == LinkTargetType.Media)
        {
            inTree = mediaNavigation.TryGetParentKey(key, out _);
            inBin = !inTree && mediaNavigation.TryGetParentKeyInBin(key, out _);
        }
        else
        {
            inTree = documentNavigation.TryGetParentKey(key, out _);
            inBin = !inTree && documentNavigation.TryGetParentKeyInBin(key, out _);
        }

        return inTree ? TargetPresence.InTree : inBin ? TargetPresence.InRecycleBin : TargetPresence.Missing;
    }

    // An empty culture makes Umbraco use the default language; an invariant document that is published counts as
    // published in every language.
    public bool IsPublished(Guid documentKey, string? culture) => publishStatus.IsDocumentPublished(documentKey, culture ?? string.Empty);

    public bool HasPublishedAncestorPath(Guid documentKey) => publishStatus.HasPublishedAncestorPath(documentKey);
}
