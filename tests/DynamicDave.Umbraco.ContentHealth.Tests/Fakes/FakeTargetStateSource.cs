using DynamicDave.Umbraco.ContentHealth.Models;
using DynamicDave.Umbraco.ContentHealth.Status;

namespace DynamicDave.Umbraco.ContentHealth.Tests.Fakes;

/// <summary>Mimics Umbraco: a null culture means the default language.</summary>
internal sealed class FakeTargetStateSource : ITargetStateSource
{
    public Dictionary<Guid, TargetPresence> Presence { get; } = [];
    public HashSet<(Guid Key, string Culture)> Published { get; } = [];
    public HashSet<Guid> UnpublishedAncestorPath { get; } = [];
    public string DefaultCulture { get; set; } = "en-US";

    public TargetPresence GetPresence(Guid key, LinkTargetType type)
        => Presence.TryGetValue(key, out var presence) ? presence : TargetPresence.Missing;

    public bool IsPublished(Guid documentKey, string? culture)
        => Published.Contains((documentKey, culture ?? DefaultCulture));

    public bool HasPublishedAncestorPath(Guid documentKey) => !UnpublishedAncestorPath.Contains(documentKey);
}
