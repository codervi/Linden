using System.Security.Cryptography;
using Linden.Api.Data;
using Linden.Api.Dtos;
using Linden.Api.Models;
using Linden.Api.Security;
using Linden.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Linden.Api.Controllers;

// Session notes are clinical records. Only the psychologist (Admin role) can touch them,
// the assistant at reception cannot even list them.
[ApiController, Route("api"), Authorize(Roles = "Admin")]
public class NotesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INoteCipher _cipher;
    private readonly IAuditService _audit;

    public NotesController(AppDbContext db, INoteCipher cipher, IAuditService audit)
    {
        _db = db;
        _cipher = cipher;
        _audit = audit;
    }

    private static string Context(int clientId) => $"client:{clientId}";

    [HttpGet("clients/{clientId:int}/notes")]
    public async Task<ActionResult<List<NoteDto>>> List(int clientId)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId)) return NotFound();

        var rows = await _db.SessionNotes.AsNoTracking()
            .Where(n => n.ClientId == clientId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync();

        List<NoteDto> result;
        try
        {
            result = rows.Select(n => new NoteDto(n.Id, n.ClientId, n.AppointmentId,
                _cipher.Decrypt(n.EncryptedContent, Context(clientId)), n.CreatedAtUtc, n.UpdatedAtUtc)).ToList();
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return Problem(title: "Notes could not be decrypted",
                detail: "Check that the encryption key is the one the notes were saved with.",
                statusCode: 500);
        }

        // The read is logged before the data leaves the server.
        // If the log can't be saved, the request fails and nothing is returned.
        _audit.Add(AuditActions.ViewNotes, "Client", clientId, $"count={rows.Count}");
        await _db.SaveChangesAsync();
        return result;
    }

    [HttpPost("clients/{clientId:int}/notes")]
    public async Task<ActionResult<NoteDto>> Create(int clientId, NoteInput input)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
        if (client == null) return NotFound();
        if (client.IsArchived) return Problem(detail: "This client is archived.", statusCode: 409);

        if (input.AppointmentId.HasValue &&
            !await _db.Appointments.AnyAsync(a => a.Id == input.AppointmentId && a.ClientId == clientId))
            return Problem(detail: "That appointment does not belong to this client.", statusCode: 400);

        var note = new SessionNote
        {
            ClientId = clientId,
            AppointmentId = input.AppointmentId,
            EncryptedContent = _cipher.Encrypt(input.Content.Trim(), Context(clientId))
        };

        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.SessionNotes.Add(note);
        await _db.SaveChangesAsync();
        _audit.Add(AuditActions.CreateNote, "Note", note.Id);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return CreatedAtAction(nameof(List), new { clientId },
            new NoteDto(note.Id, clientId, note.AppointmentId, input.Content.Trim(), note.CreatedAtUtc, null));
    }

    [HttpPut("notes/{id:int}")]
    public async Task<ActionResult<NoteDto>> Update(int id, NoteInput input)
    {
        var note = await _db.SessionNotes.FindAsync(id);
        if (note == null) return NotFound();

        if (input.AppointmentId.HasValue &&
            !await _db.Appointments.AnyAsync(a => a.Id == input.AppointmentId && a.ClientId == note.ClientId))
            return Problem(detail: "That appointment does not belong to this client.", statusCode: 400);

        note.AppointmentId = input.AppointmentId;
        note.EncryptedContent = _cipher.Encrypt(input.Content.Trim(), Context(note.ClientId));
        note.UpdatedAtUtc = DateTime.UtcNow;

        _audit.Add(AuditActions.UpdateNote, "Note", id);
        await _db.SaveChangesAsync();

        return new NoteDto(note.Id, note.ClientId, note.AppointmentId, input.Content.Trim(),
            note.CreatedAtUtc, note.UpdatedAtUtc);
    }

    [HttpDelete("notes/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _db.SessionNotes.FindAsync(id);
        if (note == null) return NotFound();

        _db.SessionNotes.Remove(note);
        _audit.Add(AuditActions.DeleteNote, "Note", id);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
