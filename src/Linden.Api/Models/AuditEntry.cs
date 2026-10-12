namespace Linden.Api.Models;

// Who did what, and when. It never contains names or note text,
// only the type of record and its id.
public class AuditEntry
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public int? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
}

public static class AuditActions
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string ViewClient = "ViewClient";
    public const string CreateClient = "CreateClient";
    public const string UpdateClient = "UpdateClient";
    public const string ArchiveClient = "ArchiveClient";
    public const string RestoreClient = "RestoreClient";
    public const string EraseClient = "EraseClient";
    public const string ViewNotes = "ViewNotes";
    public const string CreateNote = "CreateNote";
    public const string UpdateNote = "UpdateNote";
    public const string DeleteNote = "DeleteNote";
    public const string CreateAppointment = "CreateAppointment";
    public const string UpdateAppointment = "UpdateAppointment";
}
