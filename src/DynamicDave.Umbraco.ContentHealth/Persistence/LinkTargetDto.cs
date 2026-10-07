using NPoco;

namespace DynamicDave.Umbraco.ContentHealth.Persistence;

/// <summary>A distinct target and status of link rows.</summary>
[ExplicitColumns]
public class LinkTargetDto
{
    [Column("targetKey")]
    public Guid TargetKey { get; set; }

    /// <summary><see cref="Models.LinkTargetType"/>.</summary>
    [Column("targetType")]
    public int TargetType { get; set; }

    /// <summary><see cref="Models.LinkStatus"/>.</summary>
    [Column("status")]
    public int Status { get; set; }
}
