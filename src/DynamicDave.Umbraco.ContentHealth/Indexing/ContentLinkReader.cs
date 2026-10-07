using DynamicDave.Umbraco.ContentHealth.Models;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace DynamicDave.Umbraco.ContentHealth.Indexing;

/// <summary>
/// Reads the document and media links of a document, per property and culture, for the published and the draft
/// version. Uses the same reference detection as Umbraco's "Referenced by" (IDataValueReference), so rich text,
/// pickers, blocks and third-party editors that implement it are covered.
/// </summary>
public sealed class ContentLinkReader(PropertyEditorCollection propertyEditors, DataValueReferenceFactoryCollection referenceFactories)
{
    public IReadOnlyList<PropertyValueLinks> Read(IContent content)
    {
        var result = new List<PropertyValueLinks>();
        string?[] cultures = content.AvailableCultures.ToArray();

        foreach (var property in content.Properties)
        {
            if (!propertyEditors.TryGet(property.PropertyType.PropertyEditorAlias, out var editor))
            {
                continue;
            }

            string?[] propertyCultures = property.PropertyType.VariesByCulture() ? cultures : [null];
            foreach (var culture in propertyCultures)
            {
                // Segments are not scanned: only the value without segment.
                var live = PublishedState.IsPublished(content, culture)
                    ? Targets(editor, property.GetValue(culture, null, published: true))
                    : [];
                var edited = Targets(editor, property.GetValue(culture, null, published: false));
                if (live.Count > 0 || edited.Count > 0)
                {
                    result.Add(new PropertyValueLinks(property.Alias, culture, live, edited));
                }
            }
        }

        return result;
    }

    private IReadOnlyCollection<LinkTarget> Targets(IDataEditor editor, object? value)
    {
        if (value is null)
        {
            return [];
        }

        var targets = new HashSet<LinkTarget>();
        foreach (var reference in referenceFactories.GetReferences(editor, new object[] { value }))
        {
            if (reference.Udi is not GuidUdi udi)
            {
                continue;
            }

            if (udi.EntityType == UmbracoConstants.UdiEntityType.Document)
            {
                targets.Add(new LinkTarget(udi.Guid, LinkTargetType.Document));
            }
            else if (udi.EntityType == UmbracoConstants.UdiEntityType.Media)
            {
                targets.Add(new LinkTarget(udi.Guid, LinkTargetType.Media));
            }
        }

        return targets;
    }
}
