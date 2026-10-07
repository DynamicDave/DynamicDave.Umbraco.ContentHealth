using System.Text;
using DynamicDave.Umbraco.ContentHealth.Export;
using DynamicDave.Umbraco.ContentHealth.Models;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class CsvWriterTests
{
    private static readonly Guid SourceKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetKey = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static BrokenLinkModel Link(string sourceName = "Home", bool isDraft = false, string? targetName = null) => new()
    {
        SourceKey = SourceKey,
        SourceName = sourceName,
        SourcePath = sourceName,
        SourceDocumentTypeAlias = "page",
        Culture = null,
        PropertyAlias = "body",
        PropertyName = "Body",
        IsDraft = isDraft,
        TargetKey = TargetKey,
        TargetType = LinkTargetType.Document,
        TargetName = targetName,
        Status = LinkStatus.Missing,
        CheckedUtc = new DateTime(2026, 10, 7, 8, 30, 0, DateTimeKind.Unspecified),
    };

    private static string[] Lines(byte[] csv) => Encoding.UTF8.GetString(csv[3..]).Split("\r\n");

    [Fact]
    public void Starts_with_a_utf8_bom()
        => Assert.Equal(Encoding.UTF8.GetPreamble(), CsvWriter.Write([])[..3]);

    [Fact]
    public void Writes_header_and_one_line_per_link()
    {
        var lines = Lines(CsvWriter.Write([Link(), Link(isDraft: true, targetName: "Contact")]));

        Assert.Equal("Source,SourceKey,SourcePath,Culture,Property,Version,TargetKey,TargetName,TargetType,Status,CheckedUtc", lines[0]);
        Assert.Equal($"Home,{SourceKey},Home,,Body,Live,{TargetKey},,Document,Missing,2026-10-07T08:30:00Z", lines[1]);
        Assert.Equal($"Home,{SourceKey},Home,,Body,Draft,{TargetKey},Contact,Document,Missing,2026-10-07T08:30:00Z", lines[2]);
        Assert.Equal("", lines[3]); // trailing line break
    }

    [Theory]
    [InlineData("Plain", "Plain")]
    [InlineData("Café", "Café")]
    [InlineData("A, B", "\"A, B\"")]
    [InlineData("Say \"hi\"", "\"Say \"\"hi\"\"\"")]
    [InlineData("Line\nbreak", "\"Line\nbreak\"")]
    public void Escapes_separators_quotes_and_line_breaks(string value, string expected)
        => Assert.Equal(expected, CsvWriter.Escape(value));

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tTab", "'\tTab")]
    public void Neutralises_spreadsheet_formulas(string value, string expected)
        => Assert.Equal(expected, CsvWriter.Escape(value));

    [Fact]
    public void Non_ascii_names_survive_the_round_trip()
        => Assert.StartsWith("Ünïcødé", Lines(CsvWriter.Write([Link(sourceName: "Ünïcødé")]))[1]);
}
