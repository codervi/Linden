using Linden.Api.Data;
using Linden.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Linden.Api.Services;

public static class ScheduleRules
{
    public static readonly TimeOnly OpeningTime = new(8, 0);
    public static readonly TimeOnly ClosingTime = new(19, 0);

    // Returns an error message, or null when the slot is acceptable.
    public static string? CheckSlot(DateTime start, int minutes, DateTime now)
    {
        if (start < now) return "The appointment must be in the future.";
        if (start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return "The practice is closed at weekends.";

        var end = start.AddMinutes(minutes);
        if (end.Date != start.Date
            || TimeOnly.FromDateTime(start) < OpeningTime
            || TimeOnly.FromDateTime(end) > ClosingTime)
            return "Appointments must fit between 08:00 and 19:00.";

        return null;
    }

    // Any active appointment that overlaps [start, start + minutes)?
    public static Task<bool> HasConflictAsync(AppDbContext db, DateTime start, int minutes, int? ignoreId = null)
    {
        var end = start.AddMinutes(minutes);
        return db.Appointments.AnyAsync(a =>
            a.Id != ignoreId
            && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Completed)
            && a.StartsAt < end
            && a.StartsAt.AddMinutes(a.DurationMinutes) > start);
    }
}
