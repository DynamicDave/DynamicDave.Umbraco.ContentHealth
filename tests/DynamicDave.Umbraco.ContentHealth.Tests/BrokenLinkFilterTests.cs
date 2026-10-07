using DynamicDave.Umbraco.ContentHealth.Models;
using DynamicDave.Umbraco.ContentHealth.Queries;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class BrokenLinkFilterTests
{
    private static BrokenLinkModel Link(string source, LinkStatus status, bool isDraft = false, string type = "page") => new()
    {
        SourceKey = Guid.NewGuid(),
        SourceName = source,
        SourcePath = source,
        SourceDocumentTypeAlias = type,
        PropertyAlias = "body",
        PropertyName = "Body",
        IsDraft = isDraft,
        TargetKey = Guid.NewGuid(),
        TargetType = LinkTargetType.Document,
        Status = status,
        CheckedUtc = DateTime.UtcNow,
    };

    private static BrokenLinkQuery Query(
        LinkStatus[]? statuses = null,
        LinkVersionFilter version = LinkVersionFilter.All,
        string? type = null,
        BrokenLinkOrder orderBy = BrokenLinkOrder.Source,
        bool descending = false)
        => new(statuses ?? [], version, type, orderBy, descending);

    [Fact]
    public void Ok_links_are_never_listed()
    {
        var result = BrokenLinkFilter.Apply([Link("A", LinkStatus.Ok), Link("B", LinkStatus.Missing)], Query());

        Assert.Equal(["B"], result.Select(l => l.SourceName));
    }

    [Fact]
    public void No_status_filter_lists_every_problem()
        => Assert.Equal(3, BrokenLinkFilter.Apply([Link("A", LinkStatus.Missing), Link("B", LinkStatus.Trashed), Link("C", LinkStatus.Unpublished)], Query()).Count);

    [Fact]
    public void Status_filter_keeps_only_the_selected_statuses()
    {
        var result = BrokenLinkFilter.Apply(
            [Link("A", LinkStatus.Missing), Link("B", LinkStatus.Trashed), Link("C", LinkStatus.Unpublished)],
            Query(statuses: [LinkStatus.Missing, LinkStatus.Unpublished]));

        Assert.Equal(["A", "C"], result.Select(l => l.SourceName));
    }

    [Theory]
    [InlineData(LinkVersionFilter.Live, "Live")]
    [InlineData(LinkVersionFilter.Draft, "Draft")]
    public void Version_filter(LinkVersionFilter version, string expected)
    {
        var result = BrokenLinkFilter.Apply([Link("Live", LinkStatus.Missing), Link("Draft", LinkStatus.Missing, isDraft: true)], Query(version: version));

        Assert.Equal([expected], result.Select(l => l.SourceName));
    }

    [Fact]
    public void Document_type_filter_is_case_insensitive()
    {
        var result = BrokenLinkFilter.Apply([Link("A", LinkStatus.Missing, type: "newsItem"), Link("B", LinkStatus.Missing, type: "page")], Query(type: "NEWSITEM"));

        Assert.Equal(["A"], result.Select(l => l.SourceName));
    }

    [Fact]
    public void Sorts_by_source_name()
    {
        BrokenLinkModel[] links = [Link("beta", LinkStatus.Missing), Link("Alpha", LinkStatus.Missing), Link("gamma", LinkStatus.Missing)];

        Assert.Equal(["Alpha", "beta", "gamma"], BrokenLinkFilter.Apply(links, Query()).Select(l => l.SourceName));
        Assert.Equal(["gamma", "beta", "Alpha"], BrokenLinkFilter.Apply(links, Query(descending: true)).Select(l => l.SourceName));
    }

    [Fact]
    public void Sorts_by_status_then_source_name()
    {
        var result = BrokenLinkFilter.Apply(
            [Link("B", LinkStatus.Unpublished), Link("C", LinkStatus.Missing), Link("A", LinkStatus.Unpublished)],
            Query(orderBy: BrokenLinkOrder.Status));

        Assert.Equal(["C", "A", "B"], result.Select(l => l.SourceName));
    }

    [Fact]
    public void Page_returns_the_requested_slice_and_the_total()
    {
        var links = BrokenLinkFilter.Apply(Enumerable.Range(1, 30).Select(i => Link($"P{i:00}", LinkStatus.Missing)), Query());

        var page = BrokenLinkFilter.Page(links, 25, 25);

        Assert.Equal(30, page.Total);
        Assert.Equal(["P26", "P27", "P28", "P29", "P30"], page.Items.Select(l => l.SourceName));
    }

    [Fact]
    public void Skip_beyond_total_returns_empty_page_with_total()
    {
        var page = BrokenLinkFilter.Page([Link("A", LinkStatus.Missing)], 100, 25);

        Assert.Equal(1, page.Total);
        Assert.Empty(page.Items);
    }
}
