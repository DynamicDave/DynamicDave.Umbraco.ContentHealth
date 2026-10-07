using System.Text.Json;
using DynamicDave.Umbraco.ContentHealth.Models;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

// The package's controllers do not use Umbraco's backoffice JSON options, so the API models serialize with
// default System.Text.Json options. The generated TypeScript client expects enums as strings.
public class ApiJsonTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void BrokenLinkModel_serializes_status_and_target_type_as_strings()
    {
        var json = JsonSerializer.Serialize(new BrokenLinkModel
        {
            SourceKey = Guid.NewGuid(),
            SourceName = "Home",
            SourcePath = "Home",
            SourceDocumentTypeAlias = "page",
            PropertyAlias = "body",
            PropertyName = "Body",
            IsDraft = false,
            TargetKey = Guid.NewGuid(),
            TargetType = LinkTargetType.Media,
            Status = LinkStatus.Missing,
            CheckedUtc = DateTime.UtcNow,
        }, Web);

        Assert.Contains("\"status\":\"Missing\"", json);
        Assert.Contains("\"targetType\":\"Media\"", json);
    }

    [Fact]
    public void StatusCountModel_serializes_status_as_string()
    {
        var json = JsonSerializer.Serialize(new StatusCountModel { Status = LinkStatus.Trashed, Live = 1, Draft = 0 }, Web);

        Assert.Contains("\"status\":\"Trashed\"", json);
    }

    [Fact]
    public void ScanStatusModel_serializes_phase_as_string()
    {
        var json = JsonSerializer.Serialize(ScanStatusModel.From(null), Web);

        Assert.Contains("\"phase\":\"Idle\"", json);
    }
}
