using DynamicDave.Umbraco.ContentHealth.Indexing;
using DynamicDave.Umbraco.ContentHealth.Models;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class LinkRowBuilderTests
{
    private static readonly LinkTarget A = new(Guid.NewGuid(), LinkTargetType.Document);
    private static readonly LinkTarget B = new(Guid.NewGuid(), LinkTargetType.Document);
    private static readonly LinkTarget Image = new(Guid.NewGuid(), LinkTargetType.Media);

    private static PropertyValueLinks Value(string alias, string? culture, LinkTarget[] live, LinkTarget[] edited)
        => new(alias, culture, live, edited);

    [Fact]
    public void Unchanged_published_value_gives_only_live_rows()
    {
        var rows = LinkRowBuilder.Build([Value("body", null, [A, Image], [A, Image])], []);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.False(r.IsDraft));
        Assert.Contains(rows, r => r.TargetKey == Image.Key && r.TargetType == LinkTargetType.Media);
    }

    [Fact]
    public void Unpublished_value_gives_only_draft_rows()
    {
        var rows = LinkRowBuilder.Build([Value("body", null, [], [A])], []);

        var row = Assert.Single(rows);
        Assert.True(row.IsDraft);
        Assert.Equal(A.Key, row.TargetKey);
    }

    [Fact]
    public void Link_added_in_draft_is_a_draft_row_and_existing_link_stays_live()
    {
        var rows = LinkRowBuilder.Build([Value("body", null, [A], [A, B])], []);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.TargetKey == A.Key && !r.IsDraft);
        Assert.Contains(rows, r => r.TargetKey == B.Key && r.IsDraft);
    }

    [Fact]
    public void Link_removed_in_draft_stays_live_and_has_no_draft_row()
    {
        var rows = LinkRowBuilder.Build([Value("body", null, [A], [])], []);

        var row = Assert.Single(rows);
        Assert.False(row.IsDraft);
    }

    [Fact]
    public void Duplicate_links_in_one_value_are_one_row()
    {
        var rows = LinkRowBuilder.Build([Value("body", null, [A, A, A], [A, A])], []);

        Assert.Single(rows);
    }

    [Fact]
    public void Each_culture_has_its_own_rows()
    {
        var rows = LinkRowBuilder.Build([Value("body", "nl-NL", [A], [A]), Value("body", "en-US", [A], [A])], []);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Culture == "nl-NL");
        Assert.Contains(rows, r => r.Culture == "en-US");
    }

    [Fact]
    public void Excluded_properties_are_skipped_case_insensitively()
    {
        var rows = LinkRowBuilder.Build([Value("legacyLinks", null, [A], [A]), Value("body", null, [B], [B])], ["LEGACYLINKS"]);

        var row = Assert.Single(rows);
        Assert.Equal("body", row.PropertyAlias);
    }
}
