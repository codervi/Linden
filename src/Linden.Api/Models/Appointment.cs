namespace Linden.Api.Models;

public enum AppointmentStatus { Scheduled, Completed, Cancelled, NoShow }

public class Appointment
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }

    // Practice local time. The practice works in one time zone, so no offset is stored.
    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 50;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public DateTime EndsAt => StartsAt.AddMinutes(DurationMinutes);
}
