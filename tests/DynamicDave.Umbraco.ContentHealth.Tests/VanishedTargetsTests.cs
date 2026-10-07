using DynamicDave.Umbraco.ContentHealth.Indexing;
using DynamicDave.Umbraco.ContentHealth.Models;
using DynamicDave.Umbraco.ContentHealth.Persistence;
using DynamicDave.Umbraco.ContentHealth.Status;
using DynamicDave.Umbraco.ContentHealth.Tests.Fakes;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class VanishedTargetsTests
{
    private readonly FakeTargetStateSource _source = new();

    private static LinkTargetDto Target(Guid key, LinkStatus status, LinkTargetType type = LinkTargetType.Document)
        => new() { TargetKey = key, TargetType = (int)type, Status = (int)status };

    private IReadOnlyList<Guid> Select(params LinkTargetDto[] targets) => LinkIndexer.SelectVanishedTargets(targets, _source);

    [Theory]
    [InlineData(LinkStatus.Ok)]
    [InlineData(LinkStatus.Trashed)]
    [InlineData(LinkStatus.Unpublished)]
    public void Target_that_no_longer_exists_is_refreshed(LinkStatus status)
    {
        var key = Guid.NewGuid();

        Assert.Equal([key], Select(Target(key, status)));
    }

    [Fact]
    public void Target_still_in_the_tree_is_skipped()
    {
        var key = Guid.NewGuid();
        _source.Presence[key] = TargetPresence.InTree;

        Assert.Empty(Select(Target(key, LinkStatus.Ok), Target(key, LinkStatus.Unpublished)));
    }

    [Fact]
    public void Target_in_the_recycle_bin_is_refreshed_unless_already_trashed()
    {
        var trashed = Guid.NewGuid();
        var stale = Guid.NewGuid();
        _source.Presence[trashed] = TargetPresence.InRecycleBin;
        _source.Presence[stale] = TargetPresence.InRecycleBin;

        Assert.Equal([stale], Select(Target(trashed, LinkStatus.Trashed), Target(stale, LinkStatus.Ok)));
    }

    [Fact]
    public void Each_key_is_returned_once()
    {
        var key = Guid.NewGuid();

        Assert.Equal([key], Select(Target(key, LinkStatus.Ok), Target(key, LinkStatus.Unpublished)));
    }

    [Fact]
    public void Presence_is_checked_with_the_target_type()
    {
        var key = Guid.NewGuid();
        var source = new TypedPresenceSource(key);

        Assert.Empty(LinkIndexer.SelectVanishedTargets([Target(key, LinkStatus.Ok, LinkTargetType.Media)], source));
        Assert.Equal([key], LinkIndexer.SelectVanishedTargets([Target(key, LinkStatus.Ok, LinkTargetType.Document)], source));
    }

    private sealed class TypedPresenceSource(Guid mediaKey) : ITargetStateSource
    {
        public TargetPresence GetPresence(Guid key, LinkTargetType type)
            => key == mediaKey && type == LinkTargetType.Media ? TargetPresence.InTree : TargetPresence.Missing;

        public bool IsPublished(Guid documentKey, string? culture) => true;

        public bool HasPublishedAncestorPath(Guid documentKey) => true;
    }
}
