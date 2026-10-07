using Asp.Versioning;
using DynamicDave.Umbraco.ContentHealth.Export;
using DynamicDave.Umbraco.ContentHealth.Models;
using DynamicDave.Umbraco.ContentHealth.Queries;
using DynamicDave.Umbraco.ContentHealth.Scanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace DynamicDave.Umbraco.ContentHealth.Controllers;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "LinkHealth")]
public class LinkHealthController(
    LinkQueryService queries,
    ScanStateStore scanStateStore,
    IBackOfficeSecurityAccessor security,
    IAuthorizationService authorizationService,
    IEntityService entityService,
    TimeProvider timeProvider) : DynamicDaveUmbracoContentHealthApiControllerBase
{
    private const int MaxTake = 100;
    private const int MaxExportRows = 50_000;

    [HttpGet("summary")]
    [ProducesResponseType<LinkHealthSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        if (CurrentUser() is not { } user) return Unauthorized();

        var canStartScan = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.SectionAccessSettings)).Succeeded;
        return Ok(queries.GetSummary(user, ScanStatusModel.From(scanStateStore.Read().State), canStartScan));
    }

    [HttpGet("broken-links")]
    [ProducesResponseType<BrokenLinksResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetBrokenLinks(
        [FromQuery] LinkStatus[]? status = null,
        [FromQuery] LinkVersionFilter version = LinkVersionFilter.All,
        [FromQuery] string? documentType = null,
        [FromQuery] BrokenLinkOrder orderBy = BrokenLinkOrder.Source,
        [FromQuery] bool descending = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        if (skip < 0 || take < 1 || take > MaxTake || !TryCreateQuery(status, version, documentType, orderBy, descending, out var query)) return BadRequest();
        if (CurrentUser() is not { } user) return Unauthorized();

        return Ok(BrokenLinkFilter.Page(BrokenLinkFilter.Apply(queries.GetVisibleProblems(user), query), skip, take));
    }

    [HttpGet("broken-links/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ExportBrokenLinks(
        [FromQuery] LinkStatus[]? status = null,
        [FromQuery] LinkVersionFilter version = LinkVersionFilter.All,
        [FromQuery] string? documentType = null,
        [FromQuery] BrokenLinkOrder orderBy = BrokenLinkOrder.Source,
        [FromQuery] bool descending = false)
    {
        if (!TryCreateQuery(status, version, documentType, orderBy, descending, out var query)) return BadRequest();
        if (CurrentUser() is not { } user) return Unauthorized();

        var links = BrokenLinkFilter.Apply(queries.GetVisibleProblems(user), query).Take(MaxExportRows);
        var fileName = $"broken-links-{timeProvider.GetUtcNow():yyyyMMdd-HHmm}.csv";
        return File(CsvWriter.Write(links), "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("document/{key:guid}")]
    [ProducesResponseType<DocumentLinkHealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocument(Guid key)
    {
        if (!await CanBrowseAsync(key)) return StatusCode(StatusCodes.Status403Forbidden);
        if (entityService.Get(key, UmbracoObjectTypes.Document) is null) return NotFound();
        if (CurrentUser() is not { } user) return Unauthorized();

        return Ok(queries.GetDocument(key, user));
    }

    [HttpPost("scan")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult RequestScan()
        => scanStateStore.Request(timeProvider.GetUtcNow().UtcDateTime) == ScanRequestResult.Requested ? Accepted() : Conflict();

    private IUser? CurrentUser() => security.BackOfficeSecurity?.CurrentUser;

    // Model binding accepts any number for an enum, so undefined values are rejected here.
    private static bool TryCreateQuery(
        LinkStatus[]? status, LinkVersionFilter version, string? documentType, BrokenLinkOrder orderBy, bool descending, out BrokenLinkQuery query)
    {
        query = new BrokenLinkQuery(status ?? [], version, documentType, orderBy, descending);
        return Enum.IsDefined(version) && Enum.IsDefined(orderBy) && query.Statuses.All(s => Enum.IsDefined(s));
    }

    // Same check as Umbraco's own document endpoints: start nodes and the Browse permission of the user's groups.
    private async Task<bool> CanBrowseAsync(Guid key)
    {
        var result = await authorizationService.AuthorizeResourceAsync(
            User,
            ContentPermissionResource.WithKeys(ActionBrowse.ActionLetter, key),
            AuthorizationPolicies.ContentPermissionByResource);
        return result.Succeeded;
    }
}
