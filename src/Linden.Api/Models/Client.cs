namespace Linden.Api.Models;

public class Client
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    // Archived clients disappear from normal lists but keep their history.
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Appointment> Appointments { get; set; } = new();
    public List<SessionNote> Notes { get; set; } = new();
}
