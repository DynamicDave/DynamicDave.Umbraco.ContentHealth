using DynamicDave.Umbraco.ContentHealth.Queries;
using Xunit;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class StartNodeAccessTests
{
    [Fact] public void Null_start_nodes_means_full_access() => Assert.True(StartNodeAccess.CanSee("-1,1,2", null));
    [Fact] public void Root_start_node_means_full_access() => Assert.True(StartNodeAccess.CanSee("-1,1,2", [-1]));
    [Fact] public void Descendant_of_start_node_is_visible() => Assert.True(StartNodeAccess.CanSee("-1,1,5,9", [5]));
    [Fact] public void Start_node_itself_is_visible() => Assert.True(StartNodeAccess.CanSee("-1,1,5", [5]));
    [Fact] public void Other_branch_is_hidden() => Assert.False(StartNodeAccess.CanSee("-1,1,7,9", [5]));
    [Fact] public void Id_prefix_is_not_a_match() => Assert.False(StartNodeAccess.CanSee("-1,1,15", [5])); // 15 != 5
    [Fact] public void Empty_start_node_list_hides_everything() => Assert.False(StartNodeAccess.CanSee("-1,1,2", []));
}

public class StartNodeAccessResolveTests
{
    [Fact] public void Root_access_means_full_access() => Assert.True(StartNodeAccess.CanSee("-1,1,2", StartNodeAccess.Resolve(true, [5])));
    [Fact] public void No_root_and_null_start_nodes_hides_everything() => Assert.False(StartNodeAccess.CanSee("-1,1,2", StartNodeAccess.Resolve(false, null)));
    [Fact] public void No_root_and_start_node_shows_only_that_branch()
    {
        var nodes = StartNodeAccess.Resolve(false, [5]);
        Assert.True(StartNodeAccess.CanSee("-1,1,5,9", nodes));
        Assert.False(StartNodeAccess.CanSee("-1,1,7,9", nodes));
    }
}
