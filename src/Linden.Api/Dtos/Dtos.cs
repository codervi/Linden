using System.ComponentModel.DataAnnotations;
using Linden.Api.Models;

namespace Linden.Api.Dtos;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record MeDto(string Email, string Role);

public record PagedResult<T>(List<T> Items, int Page, int TotalPages, int TotalCount);

public record ClientInput(
    [Required, StringLength(80)] string FirstName,
    [Required, StringLength(80)] string LastName,
    [EmailAddress, StringLength(120)] string? Email,
    [Phone, StringLength(30)] string? Phone,
    DateOnly? DateOfBirth);

public record ClientDto(
    int Id, string FirstName, string LastName, string? Email, string? Phone,
    DateOnly? DateOfBirth, bool IsArchived, DateTime CreatedAtUtc);

public record AppointmentInput(
    [Range(1, int.MaxValue)] int ClientId,
    DateTime StartsAt,
    [Range(15, 180)] int DurationMinutes);

public record AppointmentStatusInput(AppointmentStatus Status);

public record AppointmentDto(
    int Id, int ClientId, string ClientName, DateTime StartsAt, DateTime EndsAt,
    int DurationMinutes, AppointmentStatus Status);

public record NoteInput(
    int? AppointmentId,
    [Required, StringLength(8000, MinimumLength = 1)] string Content);

public record NoteDto(
    int Id, int ClientId, int? AppointmentId, string Content,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public record AuditDto(
    long Id, DateTime TimestampUtc, string UserName, string Action,
    string EntityType, int? EntityId, string? IpAddress, string? Details);

public record DashboardDto(
    int ActiveClients, int TodayCount, int WeekCount, List<AppointmentDto> Today);
