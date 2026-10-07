using DynamicDave.Umbraco.ContentHealth.Models;
using DynamicDave.Umbraco.ContentHealth.Persistence;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace DynamicDave.Umbraco.ContentHealth.Queries;

/// <summary>
/// Turns link rows into API models and applies the same visibility rule as the content tree: the source must be
/// within the user's start nodes and browsable by the user's groups. Sources in the recycle bin are never shown.
/// </summary>
public sealed class LinkQueryService(
    LinkIndexRepository repository,
    IEntityService entityService,
    IContentTypeService contentTypeService,
    IUserService userService,
    AppCaches appCaches)
{
    // Keeps IN (...) clauses below SQL Server's parameter limit.
    private const int BatchSize = 1000;
    private static readonly LinkStatus[] ProblemStatuses = [LinkStatus.Missing, LinkStatus.Trashed, LinkStatus.Unpublished];

    public IReadOnlyList<BrokenLinkModel> GetVisibleProblems(IUser user) => Resolve(repository.GetProblems(), user).Visible;

    public LinkHealthSummaryResponse GetSummary(IUser user, ScanStatusModel scan, bool canStartScan)
    {
        var problems = GetVisibleProblems(user);
        return new LinkHealthSummaryResponse
        {
            Counts = ProblemStatuses
                .Select(status => new StatusCountModel
                {
                    Status = status,
                    Live = problems.Count(p => p.Status == status && !p.IsDraft),
                    Draft = problems.Count(p => p.Status == status && p.IsDraft),
                })
                .ToList(),
            DocumentTypes = problems
                .Select(p => p.SourceDocumentTypeAlias)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(alias => new DocumentTypeOptionModel { Alias = alias, Name = contentTypeService.Get(alias)?.Name ?? alias })
                .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            Scan = scan,
            CanStartScan = canStartScan,
        };
    }

    /// <summary>Broken links in the document, and every visible link to it. The caller checks access to the document.</summary>
    public DocumentLinkHealthResponse GetDocument(Guid documentKey, IUser user)
    {
        var outgoing = Resolve(repository.GetBySource(documentKey).Where(r => r.Status != (int)LinkStatus.Ok).ToList(), user).Visible;
        var incoming = Resolve(repository.GetByTarget(documentKey).Where(r => r.SourceKey != documentKey).ToList(), user);

        return new DocumentLinkHealthResponse
        {
            Outgoing = outgoing
                .OrderBy(l => l.PropertyName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.Culture, StringComparer.Ordinal)
                .ToList(),
            Incoming = incoming.Visible
                .OrderBy(l => l.SourceName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.PropertyName, StringComparer.CurrentCultureIgnoreCase)
                .Select(l => new IncomingLinkModel
                {
                    SourceKey = l.SourceKey,
                    SourceName = l.SourceName,
                    Culture = l.Culture,
                    PropertyName = l.PropertyName,
                    IsDraft = l.IsDraft,
                    Status = l.Status,
                })
                .ToList(),
            IncomingHiddenCount = incoming.HiddenSourceCount,
        };
    }

    private (List<BrokenLinkModel> Visible, int HiddenSourceCount) Resolve(IReadOnlyCollection<LinkDto> rows, IUser user)
    {
        if (rows.Count == 0)
        {
            return ([], 0);
        }

        var sources = GetEntities(UmbracoObjectTypes.Document, rows.Select(r => r.SourceKey));

        // HasContentRootAccess is not publicly available in 17.x; root access is signalled by start node -1.
        var calculated = user.CalculateContentStartNodeIds(entityService, appCaches);
        var startNodes = StartNodeAccess.Resolve(calculated?.Contains(-1) == true, calculated);
        var browseByPath = new Dictionary<string, bool>();
        bool CanBrowse(IEntitySlim source)
        {
            if (!StartNodeAccess.CanSee(source.Path, startNodes))
            {
                return false;
            }

            if (!browseByPath.TryGetValue(source.Path, out var allowed))
            {
                allowed = userService.GetPermissionsForPath(user, source.Path).GetAllPermissions().Contains(ActionBrowse.ActionLetter);
                browseByPath[source.Path] = allowed;
            }

            return allowed;
        }

        var existing = sources.Values.Where(s => !s.Trashed).ToList();
        var visibleSources = existing.Where(CanBrowse).ToDictionary(s => s.Key);
        var hidden = existing.Count - visibleSources.Count;

        var visibleRows = rows.Where(r => visibleSources.ContainsKey(r.SourceKey)).ToList();
        var documents = GetEntities(UmbracoObjectTypes.Document, visibleRows.Where(r => r.TargetType == (int)LinkTargetType.Document).Select(r => r.TargetKey));
        var media = GetEntities(UmbracoObjectTypes.Media, visibleRows.Where(r => r.TargetType == (int)LinkTargetType.Media).Select(r => r.TargetKey));
        var ancestorNames = AncestorNames(visibleSources.Values);
        var propertyNames = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        var models = visibleRows.Select(row =>
        {
            var source = visibleSources[row.SourceKey];
            var contentTypeAlias = (source as IContentEntitySlim)?.ContentTypeAlias ?? string.Empty;
            var targets = row.TargetType == (int)LinkTargetType.Media ? media : documents;
            return new BrokenLinkModel
            {
                SourceKey = source.Key,
                SourceName = source.Name ?? string.Empty,
                SourcePath = Breadcrumb(source, ancestorNames),
                SourceDocumentTypeAlias = contentTypeAlias,
                Culture = row.Culture,
                PropertyAlias = row.PropertyAlias,
                PropertyName = PropertyName(contentTypeAlias, row.PropertyAlias, propertyNames),
                IsDraft = row.IsDraft,
                TargetKey = row.TargetKey,
                TargetType = (LinkTargetType)row.TargetType,
                TargetName = targets.TryGetValue(row.TargetKey, out var target) ? target.Name : null,
                Status = (LinkStatus)row.Status,
                CheckedUtc = DateTime.SpecifyKind(row.CheckedUtc, DateTimeKind.Utc),
            };
        }).ToList();

        return (models, hidden);
    }

    private Dictionary<Guid, IEntitySlim> GetEntities(UmbracoObjectTypes type, IEnumerable<Guid> keys)
        => keys.Distinct().Chunk(BatchSize)
            .SelectMany(batch => entityService.GetAll(type, batch))
            .GroupBy(e => e.Key)
            .ToDictionary(g => g.Key, g => g.First());

    private Dictionary<int, string> AncestorNames(IEnumerable<IEntitySlim> sources)
    {
        var ids = sources.SelectMany(s => AncestorIds(s.Path)).Distinct().ToArray();
        return ids.Chunk(BatchSize)
            .SelectMany(batch => entityService.GetAll(UmbracoObjectTypes.Document, batch))
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
    }

    private static string Breadcrumb(IEntitySlim source, IReadOnlyDictionary<int, string> ancestorNames)
        => string.Join(" / ", AncestorIds(source.Path).Select(id => ancestorNames.GetValueOrDefault(id, "?")).Append(source.Name ?? string.Empty));

    // "-1,1051,1088,1100": the ancestors of 1100 are 1051 and 1088.
    private static IEnumerable<int> AncestorIds(string path)
        => path.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .SkipLast(1)
            .Select(segment => int.TryParse(segment, out var id) ? id : 0)
            .Where(id => id > 0);

    private string PropertyName(string contentTypeAlias, string propertyAlias, Dictionary<string, Dictionary<string, string>> cache)
    {
        if (!cache.TryGetValue(contentTypeAlias, out var names))
        {
            names = contentTypeService.Get(contentTypeAlias)?.CompositionPropertyTypes
                .GroupBy(p => p.Alias, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            cache[contentTypeAlias] = names;
        }

        return names.TryGetValue(propertyAlias, out var name) ? name : propertyAlias;
    }
}
