using DynamicDave.Umbraco.ContentHealth.Persistence;
using DynamicDave.Umbraco.ContentHealth.Queries;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class IncomingLinkPageTests
{
    private static readonly Dictionary<Guid, string> Names = [];

    private static LinkDto Row(string source, string property = "body", string? culture = null)
    {
        var key = Names.FirstOrDefault(n => n.Value == source).Key;
        if (key == Guid.Empty)
        {
            key = Guid.NewGuid();
            Names[key] = source;
        }

        return new LinkDto { SourceKey = key, PropertyAlias = property, Culture = culture, TargetKey = Guid.NewGuid() };
    }

    private static (int Total, List<string> Rows) Select(IEnumerable<LinkDto> rows, int skip, int take)
    {
        var (total, page) = IncomingLinkPage.Select(rows, key => Names[key], skip, take);
        return (total, page.Select(r => $"{Names[r.SourceKey]}|{r.PropertyAlias}|{r.Culture}").ToList());
    }

    [Fact]
    public void Total_counts_every_row_and_the_page_holds_take_rows()
    {
        var (total, rows) = Select(Enumerable.Range(0, 30).Select(i => Row($"Page {i:00}")), skip: 0, take: 25);

        Assert.Equal(30, total);
        Assert.Equal(25, rows.Count);
    }

    [Fact]
    public void Rows_are_ordered_by_source_name_then_property_alias_then_culture()
    {
        var (_, rows) = Select([Row("b", "text"), Row("A", "text", "nl-NL"), Row("a2"), Row("A", "body"), Row("A", "text", "en-US")], skip: 0, take: 25);

        Assert.Equal(["A|body|", "A|text|en-US", "A|text|nl-NL", "a2|body|", "b|text|"], rows);
    }

    [Fact]
    public void Skip_returns_the_last_partial_page()
    {
        var (total, rows) = Select(Enumerable.Range(0, 30).Select(i => Row($"Page {i:00}")), skip: 25, take: 25);

        Assert.Equal(30, total);
        Assert.Equal(["Page 25", "Page 26", "Page 27", "Page 28", "Page 29"], rows.Select(r => r.Split('|')[0]));
    }

    [Fact]
    public void Skip_beyond_the_end_returns_no_rows_but_the_total()
    {
        var (total, rows) = Select(Enumerable.Range(0, 3).Select(i => Row($"Page {i}")), skip: 25, take: 25);

        Assert.Equal(3, total);
        Assert.Empty(rows);
    }
}

public class PagingTests
{
    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(0, 25, true)]
    [InlineData(500, 100, true)]
    [InlineData(-1, 25, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, 101, false)]
    public void IsValid(int skip, int take, bool expected) => Assert.Equal(expected, Paging.IsValid(skip, take));
}
