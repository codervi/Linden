using Linden.Api.Data;
using Linden.Api.Dtos;
using Linden.Api.Models;
using Linden.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Linden.Api.Controllers;

[ApiController, Route("api/appointments"), Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public AppointmentsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // Defaults to the next 30 days.
    [HttpGet]
    public async Task<ActionResult<List<AppointmentDto>>> List(
        [FromQuery(Name = "from")] DateTime? dateFrom, [FromQuery(Name = "to")] DateTime? dateTo)
    {
        var start = (dateFrom ?? DateTime.Today).Date;
        var end = (dateTo ?? start.AddDays(30)).Date.AddDays(1);

        if (end <= start) return Problem(detail: "'to' must not be before 'from'.", statusCode: 400);
        if ((end - start).TotalDays > 120) return Problem(detail: "Ask for 120 days or fewer at a time.", statusCode: 400);

        return await _db.Appointments.AsNoTracking()
            .Where(a => a.StartsAt >= start && a.StartsAt < end)
            .OrderBy(a => a.StartsAt)
            .Select(Mappers.AppointmentToDto)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create(AppointmentInput input)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.ClientId);
        if (client == null) return Problem(detail: "Client not found.", statusCode: 400);
        if (client.IsArchived) return Problem(detail: "This client is archived.", statusCode: 409);

        var slotError = ScheduleRules.CheckSlot(input.StartsAt, input.DurationMinutes, DateTime.Now);
        if (slotError != null) return Problem(detail: slotError, statusCode: 400);

        if (await ScheduleRules.HasConflictAsync(_db, input.StartsAt, input.DurationMinutes))
            return Problem(detail: "That time overlaps another appointment.", statusCode: 409);

        var appointment = new Appointment
        {
            ClientId = input.ClientId,
            StartsAt = input.StartsAt,
            DurationMinutes = input.DurationMinutes
        };

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();
            _audit.Add(AuditActions.CreateAppointment, "Appointment", appointment.Id);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // two requests for the same minute: the unique index stopped the second one
            return Problem(detail: "That time was just taken. Pick another one.", statusCode: 409);
        }

        var dto = await _db.Appointments.AsNoTracking()
            .Where(a => a.Id == appointment.Id).Select(Mappers.AppointmentToDto).FirstAsync();
        return CreatedAtAction(nameof(List), dto);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, AppointmentStatusInput input)
    {
        if (!Enum.IsDefined(input.Status)) return Problem(detail: "Unknown status.", statusCode: 400);

        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        if (appointment.Status != AppointmentStatus.Scheduled)
            return Problem(detail: "Only scheduled appointments can be changed.", statusCode: 409);
        if (input.Status == AppointmentStatus.Scheduled)
            return Problem(detail: "The appointment is already scheduled.", statusCode: 400);

        appointment.Status = input.Status;
        _audit.Add(AuditActions.UpdateAppointment, "Appointment", id, $"status={input.Status}");
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
