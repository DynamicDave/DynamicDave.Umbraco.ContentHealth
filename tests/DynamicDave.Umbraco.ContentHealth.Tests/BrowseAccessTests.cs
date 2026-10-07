using DynamicDave.Umbraco.ContentHealth.Queries;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class BrowseAccessTests
{
    private const string Update = "Umb.Document.Update";

    private static IReadOnlyUserGroup Group(string[] permissions, params IGranularPermission[] granular)
        => new ReadOnlyUserGroup(1, Guid.NewGuid(), "Group", null, null, null, "group", [], [], permissions.ToHashSet(), granular.ToHashSet(), true);

    private static DocumentGranularPermission Document(Guid key, string permission) => new() { Key = key, Permission = permission };

    [Fact]
    public void Default_browse_without_granular_permissions_allows_every_document()
        => Assert.True(BrowseAccess.ForAllDocuments([Group([ActionBrowse.ActionLetter, Update])]));

    [Fact]
    public void No_default_browse_without_granular_permissions_denies_every_document()
        => Assert.False(BrowseAccess.ForAllDocuments([Group([Update]), Group([])]));

    [Fact]
    public void Granular_document_permissions_make_browse_depend_on_the_path()
        => Assert.Null(BrowseAccess.ForAllDocuments([Group([ActionBrowse.ActionLetter], Document(Guid.NewGuid(), Update))]));

    [Fact]
    public void Another_group_with_default_browse_and_no_granular_permissions_still_allows_every_document()
        => Assert.True(BrowseAccess.ForAllDocuments([
            Group([], Document(Guid.NewGuid(), Update)),
            Group([ActionBrowse.ActionLetter]),
        ]));

    [Fact]
    public void Granular_permissions_of_other_contexts_do_not_count()
        => Assert.True(BrowseAccess.ForAllDocuments([
            Group([ActionBrowse.ActionLetter], new UnknownTypeGranularPermission { Context = "DocumentPropertyValue", Permission = "x" }),
        ]));

    [Fact]
    public void No_groups_deny_every_document() => Assert.False(BrowseAccess.ForAllDocuments([]));

    [Fact]
    public void By_default_is_the_union_of_the_group_defaults()
    {
        Assert.True(BrowseAccess.ByDefault([Group([]), Group([ActionBrowse.ActionLetter], Document(Guid.NewGuid(), Update))]));
        Assert.False(BrowseAccess.ByDefault([Group([Update])]));
    }

    [Fact]
    public void Granular_document_keys_are_distinct_and_only_documents()
    {
        var shared = Guid.NewGuid();
        var other = Guid.NewGuid();
        var groups = new[]
        {
            Group([], Document(shared, Update), Document(shared, ActionBrowse.ActionLetter)),
            Group([], Document(shared, Update), Document(other, Update), new UnknownTypeGranularPermission { Context = "Other", Permission = "x" }),
        };

        Assert.Equal([shared, other], BrowseAccess.GranularDocumentKeys(groups).ToArray());
    }

    [Theory]
    [InlineData("-1,10,20,30", "-1,10,20")]
    [InlineData("-1,10,20", "-1,10,20")]
    [InlineData("-1,10,25,30", "-1,10")]
    [InlineData("-1,11,25,30", null)]
    public void Deciding_path_ends_at_the_deepest_node_with_granular_permissions(string path, string? expected)
        => Assert.Equal(expected, BrowseAccess.DecidingPath(path, new HashSet<int> { 10, 20 }));

    [Fact]
    public void Deciding_path_does_not_match_id_prefixes()
        => Assert.Null(BrowseAccess.DecidingPath("-1,100,200", new HashSet<int> { 10, 20 }));
}
