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

    public IReadOnlyList<BrokenLinkModel> GetVisibleProblems(IUser user) => ToModels(FilterVisible(repository.GetProblems(), user));

    public LinkHealthSummaryResponse GetSummary(IUser user, ScanStatusModel scan, bool canStartScan)
    {
        // Counts and document types only: no breadcrumbs, target names or property names.
        var visible = FilterVisible(repository.GetProblems(), user);
        return new LinkHealthSummaryResponse
        {
            Counts = ProblemStatuses
                .Select(status => new StatusCountModel
                {
                    Status = status,
                    Live = visible.Rows.Count(r => r.Status == (int)status && !r.IsDraft),
                    Draft = visible.Rows.Count(r => r.Status == (int)status && r.IsDraft),
                })
                .ToList(),
            DocumentTypes = visible.Sources.Values
                .Select(ContentTypeAlias)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(alias => new DocumentTypeOptionModel { Alias = alias, Name = contentTypeService.Get(alias)?.Name ?? alias })
                .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            Scan = scan,
            CanStartScan = canStartScan,
        };
    }

    /// <summary>
    /// Broken links in the document, and one page of the visible links to it. The caller checks access to the document.
    /// </summary>
    public DocumentLinkHealthResponse GetDocument(Guid documentKey, IUser user, int skip, int take)
    {
        var outgoing = ToModels(FilterVisible(repository.GetBySource(documentKey).Where(r => r.Status != (int)LinkStatus.Ok).ToList(), user));
        var incoming = FilterVisible(repository.GetByTarget(documentKey).Where(r => r.SourceKey != documentKey).ToList(), user);

        // A widely linked page can have thousands of incoming links: build full models (breadcrumbs, property names)
        // for the requested page only.
        var (incomingTotal, pageRows) = IncomingLinkPage.Select(incoming.Rows, key => incoming.Sources[key].Name ?? string.Empty, skip, take);
        var page = new Visibility(
            pageRows,
            pageRows.Select(r => r.SourceKey).Distinct().ToDictionary(key => key, key => incoming.Sources[key]),
            incoming.HiddenSourceCount);

        return new DocumentLinkHealthResponse
        {
            Outgoing = outgoing
                .OrderBy(l => l.PropertyName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.Culture, StringComparer.Ordinal)
                .ToList(),
            Incoming = ToModels(page)
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
            IncomingTotal = incomingTotal,
            IncomingHiddenCount = incoming.HiddenSourceCount,
        };
    }

    private Visibility FilterVisible(IReadOnlyCollection<LinkDto> rows, IUser user)
    {
        if (rows.Count == 0)
        {
            return new Visibility([], [], 0);
        }

        var sources = GetEntities(UmbracoObjectTypes.Document, rows.Select(r => r.SourceKey));

        // HasContentRootAccess is not publicly available in 17.x; root access is signalled by start node -1.
        var calculated = user.CalculateContentStartNodeIds(entityService, appCaches);
        var startNodes = StartNodeAccess.Resolve(calculated?.Contains(-1) == true, calculated);
        var canBrowse = BrowsePermission(user);

        var existing = sources.Values.Where(s => !s.Trashed).ToList();
        var visibleSources = existing
            .Where(s => StartNodeAccess.CanSee(s.Path, startNodes) && canBrowse(s.Path))
            .ToDictionary(s => s.Key);
        var visibleRows = rows.Where(r => visibleSources.ContainsKey(r.SourceKey)).ToList();

        return new Visibility(visibleRows, visibleSources, existing.Count - visibleSources.Count);
    }

    /// <summary>Browse by document path, with as few permission queries as possible.</summary>
    private Func<string, bool> BrowsePermission(IUser user)
    {
        var groups = user.Groups.ToList();
        if (BrowseAccess.ForAllDocuments(groups) is { } allowed)
        {
            return _ => allowed;
        }

        // Granular permissions: only the nodes on the path that have them change the result, so ask Umbraco once per
        // deciding path instead of once per document.
        var byDefault = BrowseAccess.ByDefault(groups);
        var granularNodeIds = GetEntities(UmbracoObjectTypes.Document, BrowseAccess.GranularDocumentKeys(groups))
            .Values.Select(e => e.Id).ToHashSet();
        var byDecidingPath = new Dictionary<string, bool>();
        return path =>
        {
            if (BrowseAccess.DecidingPath(path, granularNodeIds) is not { } decidingPath)
            {
                return byDefault;
            }

            if (!byDecidingPath.TryGetValue(decidingPath, out var browse))
            {
                browse = userService.GetPermissionsForPath(user, decidingPath).GetAllPermissions().Contains(ActionBrowse.ActionLetter);
                byDecidingPath[decidingPath] = browse;
            }

            return browse;
        };
    }

    private List<BrokenLinkModel> ToModels(Visibility visible)
    {
        if (visible.Rows.Count == 0)
        {
            return [];
        }

        var documents = GetEntities(UmbracoObjectTypes.Document, visible.Rows.Where(r => r.TargetType == (int)LinkTargetType.Document).Select(r => r.TargetKey));
        var media = GetEntities(UmbracoObjectTypes.Media, visible.Rows.Where(r => r.TargetType == (int)LinkTargetType.Media).Select(r => r.TargetKey));
        var ancestorNames = AncestorNames(visible.Sources.Values);
        var propertyNames = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        return visible.Rows.Select(row =>
        {
            var source = visible.Sources[row.SourceKey];
            var contentTypeAlias = ContentTypeAlias(source);
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
    }

    private static string ContentTypeAlias(IEntitySlim source) => (source as IContentEntitySlim)?.ContentTypeAlias ?? string.Empty;

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

    private sealed record Visibility(List<LinkDto> Rows, Dictionary<Guid, IEntitySlim> Sources, int HiddenSourceCount);
}
