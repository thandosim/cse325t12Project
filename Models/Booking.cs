using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public enum BookingStatus
{
    Requested,
    Active,
    Completed,
    Cancelled
}

public class Booking
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LoadId { get; set; }
    public Load? Load { get; set; }

    [Required]
    public string DriverId { get; set; } = default!;
    public ApplicationUser? Driver { get; set; }

    [Required]
    public BookingStatus Status { get; set; } = BookingStatus.Requested;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
