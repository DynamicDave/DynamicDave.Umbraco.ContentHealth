using DynamicDave.Umbraco.ContentHealth.Persistence;

namespace DynamicDave.Umbraco.ContentHealth.Queries;

/// <summary>One page of the links to a document, ordered before the page is cut so only that page needs full models.</summary>
public static class IncomingLinkPage
{
    public static (int Total, List<LinkDto> Rows) Select(IEnumerable<LinkDto> rows, Func<Guid, string> sourceName, int skip, int take)
    {
        var ordered = rows
            .OrderBy(r => sourceName(r.SourceKey), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => r.PropertyAlias, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Culture, StringComparer.Ordinal)
            .ThenBy(r => r.IsDraft)
            .ToList();
        return (ordered.Count, ordered.Skip(skip).Take(take).ToList());
    }
}
