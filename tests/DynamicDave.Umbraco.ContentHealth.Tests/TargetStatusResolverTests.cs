using DynamicDave.Umbraco.ContentHealth.Models;
using DynamicDave.Umbraco.ContentHealth.Status;
using DynamicDave.Umbraco.ContentHealth.Tests.Fakes;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class TargetStatusResolverTests
{
    private readonly FakeTargetStateSource _source = new();
    private readonly ContentHealthOptions _options = new();
    private readonly Guid _key = Guid.NewGuid();

    private TargetStatusResolver Resolver() => new(_source, new TestOptionsMonitor<ContentHealthOptions>(_options));

    [Theory]
    [InlineData(LinkTargetType.Document)]
    [InlineData(LinkTargetType.Media)]
    public void Unknown_target_is_missing(LinkTargetType type)
        => Assert.Equal(LinkStatus.Missing, Resolver().Resolve(_key, type, null));

    [Theory]
    [InlineData(LinkTargetType.Document)]
    [InlineData(LinkTargetType.Media)]
    public void Target_in_recycle_bin_is_trashed(LinkTargetType type)
    {
        _source.Presence[_key] = TargetPresence.InRecycleBin;
        Assert.Equal(LinkStatus.Trashed, Resolver().Resolve(_key, type, null));
    }

    [Fact]
    public void Media_in_tree_is_ok_without_publish_check()
    {
        _source.Presence[_key] = TargetPresence.InTree;
        Assert.Equal(LinkStatus.Ok, Resolver().Resolve(_key, LinkTargetType.Media, "nl-NL"));
    }

    [Fact]
    public void Document_published_in_source_culture_is_ok()
    {
        _source.Presence[_key] = TargetPresence.InTree;
        _source.Published.Add((_key, "nl-NL"));
        Assert.Equal(LinkStatus.Ok, Resolver().Resolve(_key, LinkTargetType.Document, "nl-NL"));
    }

    [Fact]
    public void Document_not_published_in_source_culture_is_unpublished()
    {
        _source.Presence[_key] = TargetPresence.InTree;
        _source.Published.Add((_key, "en-US"));
        Assert.Equal(LinkStatus.Unpublished, Resolver().Resolve(_key, LinkTargetType.Document, "nl-NL"));
    }

    [Fact]
    public void Invariant_source_checks_the_default_language()
    {
        _source.Presence[_key] = TargetPresence.InTree;
        _source.Published.Add((_key, "en-US"));
        Assert.Equal(LinkStatus.Ok, Resolver().Resolve(_key, LinkTargetType.Document, null));
    }

    [Fact]
    public void Document_under_unpublished_ancestor_is_unpublished()
    {
        _source.Presence[_key] = TargetPresence.InTree;
        _source.Published.Add((_key, "en-US"));
        _source.UnpublishedAncestorPath.Add(_key);
        Assert.Equal(LinkStatus.Unpublished, Resolver().Resolve(_key, LinkTargetType.Document, "en-US"));
    }

    [Fact]
    public void Unpublished_targets_are_ok_when_reporting_is_disabled()
    {
        _options.ReportUnpublishedTargets = false;
        _source.Presence[_key] = TargetPresence.InTree;
        Assert.Equal(LinkStatus.Ok, Resolver().Resolve(_key, LinkTargetType.Document, "en-US"));
    }

    [Fact]
    public void Missing_targets_are_reported_even_when_unpublished_reporting_is_disabled()
    {
        _options.ReportUnpublishedTargets = false;
        Assert.Equal(LinkStatus.Missing, Resolver().Resolve(_key, LinkTargetType.Document, "en-US"));
    }
}
