using DynamicDave.Umbraco.ContentHealth.Models;
using Microsoft.Extensions.Options;

namespace DynamicDave.Umbraco.ContentHealth.Status;

public sealed class TargetStatusResolver(ITargetStateSource source, IOptionsMonitor<ContentHealthOptions> options)
{
    /// <summary>
    /// Status of a link to <paramref name="targetKey"/> from a property value in <paramref name="culture"/>
    /// (null for invariant properties, which are checked against the default language).
    /// </summary>
    public LinkStatus Resolve(Guid targetKey, LinkTargetType targetType, string? culture)
    {
        switch (source.GetPresence(targetKey, targetType))
        {
            case TargetPresence.Missing:
                return LinkStatus.Missing;
            case TargetPresence.InRecycleBin:
                return LinkStatus.Trashed;
        }

        // Media has no publish state.
        if (targetType == LinkTargetType.Media || !options.CurrentValue.ReportUnpublishedTargets)
        {
            return LinkStatus.Ok;
        }

        return source.IsPublished(targetKey, culture) && source.HasPublishedAncestorPath(targetKey)
            ? LinkStatus.Ok
            : LinkStatus.Unpublished;
    }
}
