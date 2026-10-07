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
    // These two members are declared on IPublishStatusQueryService in Umbraco 17 but on its new base interface in 18,
    // so a compiled call would throw MissingMethodException on one of the two. Bind them by name at runtime.
    private readonly Func<Guid, string, bool> _isDocumentPublished =
        InterfaceMethod.Bind<Func<Guid, string, bool>>(publishStatus, typeof(IPublishStatusQueryService), nameof(IPublishStatusQueryService.IsDocumentPublished));

    private readonly Func<Guid, bool> _hasPublishedAncestorPath =
        InterfaceMethod.Bind<Func<Guid, bool>>(publishStatus, typeof(IPublishStatusQueryService), nameof(IPublishStatusQueryService.HasPublishedAncestorPath));

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
    public bool IsPublished(Guid documentKey, string? culture) => _isDocumentPublished(documentKey, culture ?? string.Empty);

    public bool HasPublishedAncestorPath(Guid documentKey) => _hasPublishedAncestorPath(documentKey);
}
