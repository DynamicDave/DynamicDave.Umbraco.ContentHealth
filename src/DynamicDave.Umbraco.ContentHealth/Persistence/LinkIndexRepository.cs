using DynamicDave.Umbraco.ContentHealth.Models;
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace DynamicDave.Umbraco.ContentHealth.Persistence;

public sealed class LinkIndexRepository(IScopeProvider scopeProvider)
{
    // Keeps IN (...) clauses below SQL Server's limit of 2100 parameters.
    internal const int BatchSize = 1000;

    /// <summary>Replaces all rows of a source document in one transaction.</summary>
    public void ReplaceSource(Guid sourceKey, IReadOnlyCollection<LinkDto> rows)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Execute(scope.SqlContext.Sql().Delete<LinkDto>().Where<LinkDto>(x => x.SourceKey == sourceKey));
        foreach (var row in rows)
        {
            scope.Database.Insert(row);
        }

        scope.Complete();
    }

    public void RemoveSources(IEnumerable<Guid> sourceKeys)
    {
        using var scope = scopeProvider.CreateScope();
        foreach (var batch in sourceKeys.Distinct().Chunk(BatchSize))
        {
            scope.Database.Execute(scope.SqlContext.Sql().Delete<LinkDto>().WhereIn<LinkDto>(x => x.SourceKey, batch));
        }

        scope.Complete();
    }

    /// <summary>Removes the rows of every source not in <paramref name="keep"/>; returns the number of removed sources.</summary>
    public int RemoveSourcesExcept(IReadOnlySet<Guid> keep)
    {
        var stale = GetDistinctSourceKeys().Where(key => !keep.Contains(key)).ToList();
        if (stale.Count > 0)
        {
            RemoveSources(stale);
        }

        return stale.Count;
    }

    public List<LinkDto> GetBySource(Guid sourceKey) => Fetch(sql => sql.Where<LinkDto>(x => x.SourceKey == sourceKey));

    public List<LinkDto> GetByTarget(Guid targetKey) => Fetch(sql => sql.Where<LinkDto>(x => x.TargetKey == targetKey));

    public List<LinkDto> GetByTargets(IEnumerable<Guid> targetKeys)
        => targetKeys.Distinct().Chunk(BatchSize)
            .SelectMany(batch => Fetch(sql => sql.WhereIn<LinkDto>(x => x.TargetKey, batch)))
            .ToList();

    /// <summary>All rows whose status is not <see cref="LinkStatus.Ok"/>.</summary>
    public List<LinkDto> GetProblems() => Fetch(sql => sql.Where<LinkDto>(x => x.Status != (int)LinkStatus.Ok));

    public List<Guid> GetDistinctTargetKeys() => FetchKeys($"SELECT DISTINCT targetKey FROM {LinkDto.TableName}");

    public List<Guid> GetDistinctSourceKeys() => FetchKeys($"SELECT DISTINCT sourceKey FROM {LinkDto.TableName}");

    /// <summary>Writes the status and checked time of the given rows.</summary>
    public void UpdateStatuses(IReadOnlyCollection<LinkDto> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        using var scope = scopeProvider.CreateScope();
        foreach (var row in rows)
        {
            scope.Database.Update(row, ["status", "checkedUtc"]);
        }

        scope.Complete();
    }

    private List<LinkDto> Fetch(Func<Sql<ISqlContext>, Sql<ISqlContext>> where)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<LinkDto>(where(scope.SqlContext.Sql().Select<LinkDto>().From<LinkDto>()));
    }

    private List<Guid> FetchKeys(string sql)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<Guid>(sql);
    }
}
