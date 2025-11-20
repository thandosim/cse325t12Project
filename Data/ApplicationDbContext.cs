using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using t12Project.Models;

namespace t12Project.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Load> Loads => Set<Load>();
    public DbSet<Truck> Trucks => Set<Truck>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<LocationUpdate> LocationUpdates => Set<LocationUpdate>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .Property(u => u.AccountType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Entity<Load>()
            .HasOne(l => l.Customer)
            .WithMany()
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Booking>()
            .HasOne(b => b.Load)
            .WithMany(l => l.Bookings)
            .HasForeignKey(b => b.LoadId);

        builder.Entity<Booking>()
            .HasOne(b => b.Driver)
            .WithMany()
            .HasForeignKey(b => b.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Truck>()
            .HasOne(t => t.Driver)
            .WithMany()
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AvailabilitySlot>()
            .HasOne(s => s.Driver)
            .WithMany()
            .HasForeignKey(s => s.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LocationUpdate>()
            .HasOne(l => l.Driver)
            .WithMany()
            .HasForeignKey(l => l.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ActivityLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
