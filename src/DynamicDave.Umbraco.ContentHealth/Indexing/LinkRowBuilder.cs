namespace DynamicDave.Umbraco.ContentHealth.Indexing;

internal static class LinkRowBuilder
{
    /// <summary>
    /// Turns property values into link rows: one live row per link in the published value, and one draft row per
    /// link that is only in the draft value. A link in both versions is therefore never listed twice.
    /// </summary>
    public static IReadOnlyList<LinkReference> Build(IEnumerable<PropertyValueLinks> values, IReadOnlyCollection<string> excludedProperties)
    {
        var excluded = new HashSet<string>(excludedProperties, StringComparer.OrdinalIgnoreCase);
        var rows = new List<LinkReference>();

        foreach (var value in values)
        {
            if (excluded.Contains(value.PropertyAlias))
            {
                continue;
            }

            var live = value.Live.ToHashSet();
            rows.AddRange(live.Select(t => new LinkReference(value.PropertyAlias, value.Culture, false, t.Key, t.Type)));
            rows.AddRange(value.Edited.Distinct()
                .Where(t => !live.Contains(t))
                .Select(t => new LinkReference(value.PropertyAlias, value.Culture, true, t.Key, t.Type)));
        }

        return rows;
    }
}
