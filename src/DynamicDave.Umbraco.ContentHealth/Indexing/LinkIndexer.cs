using DynamicDave.Umbraco.ContentHealth.Models;
using DynamicDave.Umbraco.ContentHealth.Persistence;
using DynamicDave.Umbraco.ContentHealth.Status;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;

namespace DynamicDave.Umbraco.ContentHealth.Indexing;

public sealed class LinkIndexer(
    ContentLinkReader reader,
    TargetStatusResolver resolver,
    ITargetStateSource stateSource,
    LinkIndexRepository repository,
    IOptionsMonitor<ContentHealthOptions> options,
    TimeProvider timeProvider)
{
    /// <summary>Re-reads the links of a document and replaces its rows; returns the number of rows.</summary>
    public int ReindexSource(IContent content)
    {
        var settings = options.CurrentValue;
        if (content.Trashed || settings.ExcludedDocumentTypes.Contains(content.ContentType.Alias, StringComparer.OrdinalIgnoreCase))
        {
            repository.RemoveSources([content.Key]);
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rows = LinkRowBuilder.Build(reader.Read(content), settings.ExcludedProperties)
            .Select(link => new LinkDto
            {
                SourceKey = content.Key,
                Culture = link.Culture,
                IsDraft = link.IsDraft,
                PropertyAlias = link.PropertyAlias,
                TargetKey = link.TargetKey,
                TargetType = (int)link.TargetType,
                Status = (int)resolver.Resolve(link.TargetKey, link.TargetType, link.Culture),
                CheckedUtc = now,
            })
            .ToList();

        repository.ReplaceSource(content.Key, rows);
        return rows.Count;
    }

    public void RemoveSources(IEnumerable<Guid> sourceKeys) => repository.RemoveSources(sourceKeys);

    /// <summary>Recalculates the status of every row linking to one of <paramref name="targetKeys"/>; returns the number of changed rows.</summary>
    public int RefreshTargets(IEnumerable<Guid> targetKeys)
    {
        var changed = 0;
        foreach (var batch in targetKeys.Distinct().Chunk(LinkIndexRepository.BatchSize))
        {
            changed += Refresh(repository.GetByTargets(batch));
        }

        return changed;
    }

    /// <summary>Recalculates the status of every row.</summary>
    public int RefreshAllTargets() => RefreshTargets(repository.GetDistinctTargetKeys());

    /// <summary>
    /// After deletions: recalculates only the rows of targets that no longer exist or are now in the recycle bin.
    /// Reads the distinct targets once and checks their presence in memory.
    /// </summary>
    public int RefreshVanishedTargets() => RefreshTargets(SelectVanishedTargets(repository.GetTargetsNotMissing(), stateSource));

    /// <summary>Targets whose rows are out of date: gone while not reported Missing, or in the recycle bin while not reported Trashed.</summary>
    internal static IReadOnlyList<Guid> SelectVanishedTargets(IEnumerable<LinkTargetDto> targets, ITargetStateSource stateSource)
    {
        var presence = new Dictionary<(Guid, int), TargetPresence>();
        var vanished = new HashSet<Guid>();
        foreach (var target in targets)
        {
            var key = (target.TargetKey, target.TargetType);
            if (!presence.TryGetValue(key, out var current))
            {
                current = stateSource.GetPresence(target.TargetKey, (LinkTargetType)target.TargetType);
                presence[key] = current;
            }

            if (current == TargetPresence.Missing
                || (current == TargetPresence.InRecycleBin && target.Status != (int)LinkStatus.Trashed))
            {
                vanished.Add(target.TargetKey);
            }
        }

        return vanished.ToList();
    }

    private int Refresh(IReadOnlyCollection<LinkDto> rows)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var statuses = new Dictionary<(Guid, int, string?), int>();
        var changed = new List<LinkDto>();

        foreach (var row in rows)
        {
            var key = (row.TargetKey, row.TargetType, row.Culture);
            if (!statuses.TryGetValue(key, out var status))
            {
                status = (int)resolver.Resolve(row.TargetKey, (LinkTargetType)row.TargetType, row.Culture);
                statuses[key] = status;
            }

            if (status == row.Status)
            {
                continue;
            }

            row.Status = status;
            row.CheckedUtc = now;
            changed.Add(row);
        }

        repository.UpdateStatuses(changed);
        return changed.Count;
    }
}
