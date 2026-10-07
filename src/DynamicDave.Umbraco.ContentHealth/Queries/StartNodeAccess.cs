namespace DynamicDave.Umbraco.ContentHealth.Queries;

internal static class StartNodeAccess
{
    public static bool CanSee(string path, IReadOnlyCollection<int>? startNodeIds)
    {
        if (startNodeIds is null || startNodeIds.Contains(-1)) return true;
        var segments = path.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return startNodeIds.Any(id => segments.Contains(id.ToString()));
    }

    /// <summary>
    /// Decides what to pass to <see cref="CanSee"/>: null (full access) only when the user has root access;
    /// otherwise the calculated start nodes, where a missing set hides everything.
    /// </summary>
    public static IReadOnlyCollection<int>? Resolve(bool hasRootAccess, IReadOnlyCollection<int>? startNodeIds)
        => hasRootAccess ? null : startNodeIds ?? [];
}
