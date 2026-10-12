using System.Security.Claims;
using Linden.Api.Data;
using Linden.Api.Models;

namespace Linden.Api.Services;

public interface IAuditService
{
    // Adds the entry to the current unit of work. The caller saves,
    // so the audit row and the change it describes are committed together.
    void Add(string action, string entityType, int? entityId = null, string? details = null, string? userName = null);
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public void Add(string action, string entityType, int? entityId = null, string? details = null, string? userName = null)
    {
        var ctx = _http.HttpContext;
        _db.AuditEntries.Add(new AuditEntry
        {
            UserName = userName ?? ctx?.User.FindFirstValue(ClaimTypes.Name) ?? "anonymous",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
            Details = details
        });
    }
}
