using DynamicDave.Umbraco.ContentHealth.Models;

namespace DynamicDave.Umbraco.ContentHealth.Queries;

public enum LinkVersionFilter
{
    All,
    Live,
    Draft,
}

public enum BrokenLinkOrder
{
    Source,
    Status,
}

/// <param name="Statuses">Empty: every problem status.</param>
public sealed record BrokenLinkQuery(
    IReadOnlyCollection<LinkStatus> Statuses,
    LinkVersionFilter Version,
    string? DocumentType,
    BrokenLinkOrder OrderBy,
    bool Descending);

internal static class BrokenLinkFilter
{
    private static readonly StringComparer Names = StringComparer.CurrentCultureIgnoreCase;

    public static IReadOnlyList<BrokenLinkModel> Apply(IEnumerable<BrokenLinkModel> links, BrokenLinkQuery query)
    {
        var result = links.Where(l => l.Status != LinkStatus.Ok);
        if (query.Statuses.Count > 0)
        {
            result = result.Where(l => query.Statuses.Contains(l.Status));
        }

        result = query.Version switch
        {
            LinkVersionFilter.Live => result.Where(l => !l.IsDraft),
            LinkVersionFilter.Draft => result.Where(l => l.IsDraft),
            _ => result,
        };

        if (!string.IsNullOrWhiteSpace(query.DocumentType))
        {
            result = result.Where(l => string.Equals(l.SourceDocumentTypeAlias, query.DocumentType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query.OrderBy == BrokenLinkOrder.Status
            ? (query.Descending ? result.OrderByDescending(l => l.Status) : result.OrderBy(l => l.Status)).ThenBy(l => l.SourceName, Names)
            : query.Descending ? result.OrderByDescending(l => l.SourceName, Names) : result.OrderBy(l => l.SourceName, Names);

        // Stable order for paging.
        return ordered
            .ThenBy(l => l.SourceKey)
            .ThenBy(l => l.PropertyName, Names)
            .ThenBy(l => l.Culture, StringComparer.Ordinal)
            .ThenBy(l => l.TargetKey)
            .ToList();
    }

    public static BrokenLinksResponse Page(IReadOnlyList<BrokenLinkModel> links, int skip, int take)
        => new() { Total = links.Count, Items = links.Skip(skip).Take(take).ToList() };
}
