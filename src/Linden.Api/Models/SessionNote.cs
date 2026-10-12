namespace Linden.Api.Models;

public class SessionNote
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    // The note text is never stored as plain text. See Security/AesGcmNoteCipher.cs.
    public string EncryptedContent { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
