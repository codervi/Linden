using System.Linq.Expressions;
using Linden.Api.Models;

namespace Linden.Api.Dtos;

public static class Mappers
{
    // Expressions are used inside .Select(...) so EF only reads the columns it needs.
    public static readonly Expression<Func<Client, ClientDto>> ClientToDto = c =>
        new ClientDto(c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.DateOfBirth, c.IsArchived, c.CreatedAtUtc);

    public static readonly Expression<Func<Appointment, AppointmentDto>> AppointmentToDto = a =>
        new AppointmentDto(a.Id, a.ClientId, a.Client!.FirstName + " " + a.Client.LastName,
            a.StartsAt, a.StartsAt.AddMinutes(a.DurationMinutes), a.DurationMinutes, a.Status);

    // Same mapping for an entity that is already in memory.
    public static readonly Func<Client, ClientDto> ToClient = ClientToDto.Compile();
}
