using Linden.Api.Data;
using Linden.Api.Dtos;
using Linden.Api.Models;
using Linden.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Linden.Api.Controllers;

[ApiController, Route("api/clients"), Authorize]
public class ClientsController : ControllerBase
{
    private const int PageSize = 10;
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ClientsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientDto>>> List(string? search, bool archived = false, int page = 1)
    {
        var query = _db.Clients.AsNoTracking().Where(c => c.IsArchived == archived);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c => c.FirstName.Contains(s) || c.LastName.Contains(s)
                                     || (c.Email != null && c.Email.Contains(s)));
        }

        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, pages);

        var items = await query
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(Mappers.ClientToDto)
            .ToListAsync();

        return new PagedResult<ClientDto>(items, page, pages, total);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> Get(int id)
    {
        var dto = await _db.Clients.AsNoTracking()
            .Where(c => c.Id == id).Select(Mappers.ClientToDto).FirstOrDefaultAsync();
        if (dto == null) return NotFound();

        // opening a client file is recorded
        _audit.Add(AuditActions.ViewClient, "Client", id);
        await _db.SaveChangesAsync();
        return dto;
    }

    [HttpGet("{id:int}/appointments")]
    public async Task<ActionResult<List<AppointmentDto>>> Appointments(int id)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == id)) return NotFound();

        return await _db.Appointments.AsNoTracking()
            .Where(a => a.ClientId == id)
            .OrderByDescending(a => a.StartsAt)
            .Select(Mappers.AppointmentToDto)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create(ClientInput input)
    {
        var error = Validate(input);
        if (error != null) return Problem(detail: error, statusCode: 400);

        var client = new Client();
        Apply(client, input);

        // the client row and its audit row are saved together or not at all
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        _audit.Add(AuditActions.CreateClient, "Client", client.Id);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return CreatedAtAction(nameof(Get), new { id = client.Id }, Mappers.ToClient(client));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClientDto>> Update(int id, ClientInput input)
    {
        var error = Validate(input);
        if (error != null) return Problem(detail: error, statusCode: 400);

        var client = await _db.Clients.FindAsync(id);
        if (client == null) return NotFound();
        if (client.IsArchived) return Problem(detail: "Restore the client before editing.", statusCode: 409);

        Apply(client, input);
        _audit.Add(AuditActions.UpdateClient, "Client", id);
        await _db.SaveChangesAsync();
        return Mappers.ToClient(client);
    }

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client == null) return NotFound();

        var hasUpcoming = await _db.Appointments.AnyAsync(a =>
            a.ClientId == id && a.Status == AppointmentStatus.Scheduled && a.StartsAt >= DateTime.Now);
        if (hasUpcoming)
            return Problem(detail: "This client still has upcoming appointments. Cancel them first.", statusCode: 409);

        client.IsArchived = true;
        _audit.Add(AuditActions.ArchiveClient, "Client", id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client == null) return NotFound();

        client.IsArchived = false;
        _audit.Add(AuditActions.RestoreClient, "Client", id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Right to erasure: removes the client with every appointment and note.
    // Only the psychologist can do it, and only for archived clients,
    // so it can't happen by accident. The audit row keeps the id only.
    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Erase(int id)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return NotFound();
        if (!client.IsArchived)
            return Problem(detail: "Archive the client first. Only archived clients can be erased.", statusCode: 409);

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.SessionNotes.Where(n => n.ClientId == id).ExecuteDeleteAsync();
        await _db.Appointments.Where(a => a.ClientId == id).ExecuteDeleteAsync();
        await _db.Clients.Where(c => c.Id == id).ExecuteDeleteAsync();
        _audit.Add(AuditActions.EraseClient, "Client", id, "client, appointments and notes erased");
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return NoContent();
    }

    private static string? Validate(ClientInput input)
    {
        if (input.DateOfBirth.HasValue && input.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.Today))
            return "Date of birth cannot be in the future.";
        return null;
    }

    private static void Apply(Client client, ClientInput input)
    {
        client.FirstName = input.FirstName.Trim();
        client.LastName = input.LastName.Trim();
        client.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
        client.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
        client.DateOfBirth = input.DateOfBirth;
    }
}
