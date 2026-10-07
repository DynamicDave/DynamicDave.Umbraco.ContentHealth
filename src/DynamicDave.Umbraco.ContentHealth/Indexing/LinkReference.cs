using DynamicDave.Umbraco.ContentHealth.Models;

namespace DynamicDave.Umbraco.ContentHealth.Indexing;

public readonly record struct LinkTarget(Guid Key, LinkTargetType Type);

/// <summary>
/// The link targets in one property value of a document. <see cref="Live"/> is empty when the document is not
/// published in <see cref="Culture"/>.
/// </summary>
public sealed record PropertyValueLinks(
    string PropertyAlias,
    string? Culture,
    IReadOnlyCollection<LinkTarget> Live,
    IReadOnlyCollection<LinkTarget> Edited);

/// <summary>One internal link of a document, as stored in the link table.</summary>
public sealed record LinkReference(string PropertyAlias, string? Culture, bool IsDraft, Guid TargetKey, LinkTargetType TargetType);
