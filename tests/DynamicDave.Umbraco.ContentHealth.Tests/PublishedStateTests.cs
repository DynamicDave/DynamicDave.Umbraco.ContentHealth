using DynamicDave.Umbraco.ContentHealth.Indexing;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class PublishedStateTests
{
    // Same member shapes as Umbraco's Content class, which PublishedState binds to at runtime.
    private sealed class FakeContent
    {
        public bool Published { get; set; }
        public HashSet<string> PublishedCultures { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsCulturePublished(string culture) => PublishedCultures.Contains(culture);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Null_culture_reads_published(bool published)
        => Assert.Equal(published, PublishedState.IsPublished(new FakeContent { Published = published }, null));

    [Fact]
    public void Culture_reads_is_culture_published()
    {
        var content = new FakeContent { Published = true };
        content.PublishedCultures.Add("en-US");

        Assert.True(PublishedState.IsPublished(content, "en-US"));
        Assert.False(PublishedState.IsPublished(content, "nl-NL"));
    }

    [Fact]
    public void Type_without_the_members_throws_a_clear_error()
        => Assert.Throws<MissingMemberException>(() => PublishedState.IsPublished(new object(), null));
}
