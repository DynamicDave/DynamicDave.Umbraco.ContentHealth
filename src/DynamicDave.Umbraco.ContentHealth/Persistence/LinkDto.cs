using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace DynamicDave.Umbraco.ContentHealth.Persistence;

/// <summary>One internal link of a document. All links are stored, not only broken ones.</summary>
[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public class LinkDto
{
    public const string TableName = "dynamicDaveContentHealthLink";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("sourceKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_dynamicDaveContentHealthLink_sourceKey")]
    public Guid SourceKey { get; set; }

    /// <summary>Null for invariant properties.</summary>
    [Column("culture")]
    [Length(20)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Culture { get; set; }

    /// <summary>True: the link is only in the draft version, not in the published one.</summary>
    [Column("isDraft")]
    public bool IsDraft { get; set; }

    [Column("propertyAlias")]
    [Length(255)]
    public string PropertyAlias { get; set; } = string.Empty;

    [Column("targetKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_dynamicDaveContentHealthLink_targetKey")]
    public Guid TargetKey { get; set; }

    /// <summary><see cref="Models.LinkTargetType"/>.</summary>
    [Column("targetType")]
    public int TargetType { get; set; }

    /// <summary><see cref="Models.LinkStatus"/>.</summary>
    [Column("status")]
    [Index(IndexTypes.NonClustered, Name = "IX_dynamicDaveContentHealthLink_status")]
    public int Status { get; set; }

    [Column("checkedUtc")]
    public DateTime CheckedUtc { get; set; }
}
