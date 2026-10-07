namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class ContentHealthOptionsTests
{
    [Theory]
    [InlineData("03:00", 3, 0)]
    [InlineData("3:00", 3, 0)]
    [InlineData("23:45", 23, 45)]
    [InlineData(" 04:30 ", 4, 30)]
    public void Parses_valid_times(string value, int hour, int minute)
    {
        var options = new ContentHealthOptions { NightlyScanTime = value };

        Assert.Equal(new TimeOnly(hour, minute), options.GetNightlyScanTime());
        Assert.False(options.HasInvalidNightlyScanTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_time_disables_the_nightly_scan(string? value)
    {
        var options = new ContentHealthOptions { NightlyScanTime = value };

        Assert.Null(options.GetNightlyScanTime());
        Assert.False(options.HasInvalidNightlyScanTime);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("03:00 AM")]
    [InlineData("nightly")]
    [InlineData("03:00:00")]
    public void Invalid_time_disables_the_nightly_scan_and_is_reported(string value)
    {
        var options = new ContentHealthOptions { NightlyScanTime = value };

        Assert.Null(options.GetNightlyScanTime());
        Assert.True(options.HasInvalidNightlyScanTime);
    }

    [Fact]
    public void Defaults_scan_at_three_and_report_unpublished_targets()
    {
        var options = new ContentHealthOptions();

        Assert.Equal(new TimeOnly(3, 0), options.GetNightlyScanTime());
        Assert.True(options.ReportUnpublishedTargets);
        Assert.Empty(options.ExcludedDocumentTypes);
        Assert.Empty(options.ExcludedProperties);
    }
}
