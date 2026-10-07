using System.Collections.Concurrent;
using System.Reflection;

namespace DynamicDave.Umbraco.ContentHealth.Indexing;

/// <summary>
/// Reads the publish state of a document. IContent.Published and IContent.IsCulturePublished moved to the new base
/// interface IPublishableContentBase in Umbraco 18, so a call through IContent compiled against Umbraco 17 throws
/// MissingMethodException on 18. Binding to the runtime class works on both.
/// </summary>
internal static class PublishedState
{
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Published, MethodInfo IsCulturePublished)> Members = new();

    /// <summary>Null culture: published in any culture. Otherwise: published in that culture.</summary>
    public static bool IsPublished(object content, string? culture)
    {
        var (published, isCulturePublished) = Members.GetOrAdd(content.GetType(), Bind);
        return culture is null
            ? (bool)published.GetValue(content)!
            : (bool)isCulturePublished.Invoke(content, [culture])!;
    }

    private static (PropertyInfo, MethodInfo) Bind(Type type)
        => (type.GetProperty("Published", BindingFlags.Public | BindingFlags.Instance, null, typeof(bool), Type.EmptyTypes, null)
                ?? throw new MissingMemberException(type.FullName, "Published"),
            type.GetMethod("IsCulturePublished", BindingFlags.Public | BindingFlags.Instance, [typeof(string)])
                ?? throw new MissingMemberException(type.FullName, "IsCulturePublished"));
}
