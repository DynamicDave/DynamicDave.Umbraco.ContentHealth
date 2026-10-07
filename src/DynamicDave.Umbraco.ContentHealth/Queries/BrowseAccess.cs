using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;

namespace DynamicDave.Umbraco.ContentHealth.Queries;

/// <summary>
/// Decides Browse the way Umbraco's IUserService.GetPermissionsForPath does, without a query per document: per group,
/// the granular permissions of the deepest node on the path that has them for that group, otherwise the group's
/// default permissions; the user has the union over all groups.
/// </summary>
internal static class BrowseAccess
{
    /// <summary>
    /// The Browse decision when it is the same for every document; null when it depends on granular permissions.
    /// </summary>
    public static bool? ForAllDocuments(IReadOnlyCollection<IReadOnlyUserGroup> groups)
    {
        // A group without granular document permissions always contributes its defaults.
        if (groups.Any(g => !HasGranularDocumentPermissions(g) && GrantsBrowse(g.Permissions)))
        {
            return true;
        }

        return groups.Any(HasGranularDocumentPermissions) ? null : false;
    }

    /// <summary>Browse on a document whose path has no node with granular permissions.</summary>
    public static bool ByDefault(IReadOnlyCollection<IReadOnlyUserGroup> groups) => groups.Any(g => GrantsBrowse(g.Permissions));

    /// <summary>The documents that have granular permissions in one of the groups.</summary>
    public static IEnumerable<Guid> GranularDocumentKeys(IReadOnlyCollection<IReadOnlyUserGroup> groups)
        => groups
            .SelectMany(g => g.GranularPermissions ?? Enumerable.Empty<IGranularPermission>())
            .Where(IsDocumentPermission)
            .Select(p => p.Key!.Value)
            .Distinct();

    /// <summary>
    /// The part of <paramref name="path"/> that decides its permissions: up to and including the deepest node with
    /// granular permissions. Null when no node on the path has them, so the group defaults apply.
    /// </summary>
    public static string? DecidingPath(string path, IReadOnlySet<int> nodesWithGranularPermissions)
    {
        var segments = path.Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            if (int.TryParse(segments[i], out var id) && nodesWithGranularPermissions.Contains(id))
            {
                return string.Join(',', segments.Take(i + 1));
            }
        }

        return null;
    }

    private static bool HasGranularDocumentPermissions(IReadOnlyUserGroup group)
        => group.GranularPermissions?.Any(p => p.Context == DocumentGranularPermission.ContextType) == true;

    private static bool IsDocumentPermission(IGranularPermission permission)
        => permission.Context == DocumentGranularPermission.ContextType && permission.Key is not null;

    private static bool GrantsBrowse(ISet<string>? permissions) => permissions?.Contains(ActionBrowse.ActionLetter) == true;
}
