using Linden.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Linden.Api.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<SessionNote> SessionNotes => Set<SessionNote>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Client>(e =>
        {
            e.Property(c => c.FirstName).HasMaxLength(80);
            e.Property(c => c.LastName).HasMaxLength(80);
            e.Property(c => c.Email).HasMaxLength(120);
            e.Property(c => c.Phone).HasMaxLength(30);
            e.HasIndex(c => new { c.LastName, c.FirstName });
        });

        b.Entity<Appointment>(e =>
        {
            e.Ignore(a => a.EndsAt);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(a => a.Client).WithMany(c => c.Appointments)
                .HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Cascade);

            // Only one person is treated at a time, so two active appointments
            // can never start at the same minute. The service also checks overlaps;
            // this index is the last line of defence against a race.
            e.HasIndex(a => a.StartsAt).IsUnique().HasFilter("[Status] <> 'Cancelled' AND [Status] <> 'NoShow'");
        });

        b.Entity<SessionNote>(e =>
        {
            e.HasOne(n => n.Client).WithMany(c => c.Notes)
                .HasForeignKey(n => n.ClientId).OnDelete(DeleteBehavior.Cascade);

            // SQL Server does not allow two cascade paths to the same table,
            // so deleting an appointment just detaches its notes.
            e.HasOne(n => n.Appointment).WithMany()
                .HasForeignKey(n => n.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.Property(a => a.UserName).HasMaxLength(256);
            e.Property(a => a.Action).HasMaxLength(40);
            e.Property(a => a.EntityType).HasMaxLength(40);
            e.Property(a => a.IpAddress).HasMaxLength(45);
            e.Property(a => a.Details).HasMaxLength(200);
            e.HasIndex(a => a.TimestampUtc);
        });
    }
}
