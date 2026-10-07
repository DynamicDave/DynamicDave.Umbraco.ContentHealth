using DynamicDave.Umbraco.ContentHealth.Notifications;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Services.Changes;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class TargetChangeHandlerTests
{
    private static ContentCacheRefresher.JsonPayload Payload(
        TreeChangeTypes types,
        bool withKey = true,
        bool blueprint = false,
        string[]? published = null,
        string[]? unpublished = null)
        => new()
        {
            Key = withKey ? Guid.NewGuid() : null,
            ChangeTypes = types,
            Blueprint = blueprint,
            PublishedCultures = published,
            UnpublishedCultures = unpublished,
        };

    [Fact]
    public void Branch_refresh_without_cultures_needs_a_reload()
    {
        var payload = Payload(TreeChangeTypes.RefreshBranch);

        Assert.Equal([payload.Key!.Value], TargetChangeHandler.KeysNeedingPublishStatusReload([payload]));
    }

    [Fact]
    public void Empty_culture_arrays_count_as_no_cultures()
    {
        var payload = Payload(TreeChangeTypes.RefreshBranch, published: [], unpublished: []);

        Assert.Equal([payload.Key!.Value], TargetChangeHandler.KeysNeedingPublishStatusReload([payload]));
    }

    [Fact]
    public void Branch_refresh_with_published_cultures_is_left_to_umbraco()
        => Assert.Empty(TargetChangeHandler.KeysNeedingPublishStatusReload([Payload(TreeChangeTypes.RefreshBranch, published: ["*"])]));

    [Fact]
    public void Branch_refresh_with_unpublished_cultures_is_left_to_umbraco()
        => Assert.Empty(TargetChangeHandler.KeysNeedingPublishStatusReload([Payload(TreeChangeTypes.RefreshBranch, unpublished: ["*"])]));

    [Fact]
    public void Node_refresh_never_needs_a_reload()
        => Assert.Empty(TargetChangeHandler.KeysNeedingPublishStatusReload([Payload(TreeChangeTypes.RefreshNode)]));

    [Theory]
    [InlineData(TreeChangeTypes.Remove)]
    [InlineData(TreeChangeTypes.RefreshAll)]
    [InlineData(TreeChangeTypes.RefreshBranch | TreeChangeTypes.Remove)]
    [InlineData(TreeChangeTypes.RefreshBranch | TreeChangeTypes.RefreshAll)]
    public void Removals_and_full_refreshes_do_not_need_a_reload(TreeChangeTypes types)
        => Assert.Empty(TargetChangeHandler.KeysNeedingPublishStatusReload([Payload(types)]));

    [Fact]
    public void Blueprints_do_not_need_a_reload()
        => Assert.Empty(TargetChangeHandler.KeysNeedingPublishStatusReload([Payload(TreeChangeTypes.RefreshBranch, blueprint: true)]));

    [Fact]
    public void Payloads_without_a_key_do_not_need_a_reload()
    {
        Assert.Empty(TargetChangeHandler.KeysNeedingPublishStatusReload([Payload(TreeChangeTypes.RefreshBranch, withKey: false)]));
    }

    [Fact]
    public void Only_the_matching_payloads_are_returned()
    {
        var restored = Payload(TreeChangeTypes.RefreshBranch);
        var published = Payload(TreeChangeTypes.RefreshBranch, published: ["*"]);
        var saved = Payload(TreeChangeTypes.RefreshNode);

        Assert.Equal([restored.Key!.Value], TargetChangeHandler.KeysNeedingPublishStatusReload([published, restored, saved]));
    }
}
