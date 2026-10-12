using Linden.Api.Data;
using Linden.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Linden.Api.Controllers;

// Read-only on purpose: there is no endpoint that edits or deletes an audit entry.
[ApiController, Route("api/audit"), Authorize(Roles = "Admin")]
public class AuditController : ControllerBase
{
    private const int PageSize = 25;
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditDto>>> List(
        [FromQuery(Name = "event")] string? eventName, int page = 1)
    {
        var query = _db.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventName))
            query = query.Where(a => a.Action == eventName);

        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, pages);

        var items = await query
            .OrderByDescending(a => a.TimestampUtc).ThenByDescending(a => a.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(a => new AuditDto(a.Id, a.TimestampUtc, a.UserName, a.Action,
                a.EntityType, a.EntityId, a.IpAddress, a.Details))
            .ToListAsync();

        return new PagedResult<AuditDto>(items, page, pages, total);
    }
}
