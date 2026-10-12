using Linden.Api.Data;
using Linden.Api.Dtos;
using Linden.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Linden.Api.Controllers;

[ApiController, Route("api/dashboard"), Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var weekEnd = today.AddDays(7);

        var activeClients = await _db.Clients.CountAsync(c => !c.IsArchived);

        var scheduled = _db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Scheduled);

        var weekCount = await scheduled.CountAsync(a => a.StartsAt >= today && a.StartsAt < weekEnd);

        var todayList = await scheduled
            .Where(a => a.StartsAt >= today && a.StartsAt < tomorrow)
            .OrderBy(a => a.StartsAt)
            .Select(Mappers.AppointmentToDto)
            .ToListAsync();

        return new DashboardDto(activeClients, todayList.Count, weekCount, todayList);
    }
}
