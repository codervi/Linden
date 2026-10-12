using Linden.Api.Models;
using Linden.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Linden.Api.Data;

public static class SeedData
{
    public const string AdminRole = "Admin";          // the psychologist
    public const string AssistantRole = "Assistant";  // reception

    public static async Task RunAsync(IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AdminRole, AssistantRole })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        // Users are only created when credentials are configured.
        // In production these come from environment variables, never from the repository.
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        await EnsureUserAsync(users, config["SeedUsers:AdminEmail"], config["SeedUsers:AdminPassword"], AdminRole);
        await EnsureUserAsync(users, config["SeedUsers:AssistantEmail"], config["SeedUsers:AssistantPassword"], AssistantRole);

        if (isDevelopment && !await db.Clients.AnyAsync())
            await AddDemoDataAsync(db, sp.GetRequiredService<INoteCipher>());
    }

    private static async Task EnsureUserAsync(UserManager<IdentityUser> users, string? email, string? password, string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (await users.FindByEmailAsync(email) != null) return;

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        if ((await users.CreateAsync(user, password)).Succeeded)
            await users.AddToRoleAsync(user, role);
    }

    // First weekday on or after the given offset from today, at the given hour.
    private static DateTime Workday(int offsetDays, int hour)
    {
        var d = DateTime.Today.AddDays(offsetDays);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        return d.AddHours(hour);
    }

    // Invented people and invented notes, for local development only.
    private static async Task AddDemoDataAsync(AppDbContext db, INoteCipher cipher)
    {
        var clients = new[]
        {
            new Client { FirstName = "Nora", LastName = "Whitfield", Email = "nora.w@example.test", Phone = "+44 20 7946 0101", DateOfBirth = new DateOnly(1991, 4, 12) },
            new Client { FirstName = "Tomas", LastName = "Berg", Email = "t.berg@example.test", Phone = "+46 8 555 0102", DateOfBirth = new DateOnly(1985, 9, 3) },
            new Client { FirstName = "Aylin", LastName = "Demir", Email = "aylin.d@example.test", DateOfBirth = new DateOnly(1998, 1, 27) },
            new Client { FirstName = "Samuel", LastName = "Okoye", Phone = "+353 1 555 0104", DateOfBirth = new DateOnly(1979, 11, 8) },
            new Client { FirstName = "Lena", LastName = "Fischer", Email = "lena.f@example.test", DateOfBirth = new DateOnly(2001, 6, 19) }
        };
        db.Clients.AddRange(clients);
        await db.SaveChangesAsync();

        var past1 = new Appointment { ClientId = clients[0].Id, StartsAt = Workday(-9, 10), Status = AppointmentStatus.Completed };
        var past2 = new Appointment { ClientId = clients[1].Id, StartsAt = Workday(-6, 11), Status = AppointmentStatus.Completed };
        var past3 = new Appointment { ClientId = clients[0].Id, StartsAt = Workday(-3, 9), Status = AppointmentStatus.Completed };
        var missed = new Appointment { ClientId = clients[3].Id, StartsAt = Workday(-2, 13), Status = AppointmentStatus.NoShow };

        db.Appointments.AddRange(past1, past2, past3, missed,
            new Appointment { ClientId = clients[2].Id, StartsAt = Workday(0, 15) },
            new Appointment { ClientId = clients[0].Id, StartsAt = Workday(1, 10) },
            new Appointment { ClientId = clients[4].Id, StartsAt = Workday(2, 14) },
            new Appointment { ClientId = clients[1].Id, StartsAt = Workday(3, 16) });
        await db.SaveChangesAsync();

        string Enc(Client c, string text) => cipher.Encrypt(text, $"client:{c.Id}");

        db.SessionNotes.AddRange(
            new SessionNote { ClientId = clients[0].Id, AppointmentId = past1.Id, EncryptedContent = Enc(clients[0], "Intake session. Main concern is poor sleep and work stress. Agreed to keep a sleep diary for two weeks.") },
            new SessionNote { ClientId = clients[0].Id, AppointmentId = past3.Id, EncryptedContent = Enc(clients[0], "Diary shows irregular bedtimes. Practised a short breathing exercise. Client reports slightly better mornings.") },
            new SessionNote { ClientId = clients[1].Id, AppointmentId = past2.Id, EncryptedContent = Enc(clients[1], "First session after referral. Talked about recent move abroad and feeling isolated. Plan: weekly sessions for six weeks.") });
        await db.SaveChangesAsync();
    }
}
