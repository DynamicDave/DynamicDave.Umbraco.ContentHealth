using DynamicDave.Umbraco.ContentHealth.Models;

namespace DynamicDave.Umbraco.ContentHealth.Status;

public enum TargetPresence
{
    Missing,
    InTree,
    InRecycleBin,
}

/// <summary>Current state of documents and media, as seen by the backoffice caches.</summary>
public interface ITargetStateSource
{
    TargetPresence GetPresence(Guid key, LinkTargetType type);

    /// <summary>Whether the document is published in <paramref name="culture"/>; null means the default language.</summary>
    bool IsPublished(Guid documentKey, string? culture);

    /// <summary>False when an ancestor of the document is not published in any culture.</summary>
    bool HasPublishedAncestorPath(Guid documentKey);
}
