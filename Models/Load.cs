using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace t12Project.Models;

public enum LoadStatus
{
    Draft,
    Available,
    Assigned,
    Delivered
}

public class Load
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public LoadStatus Status { get; set; } = LoadStatus.Draft;

    [MaxLength(256)]
    public string PickupLocation { get; set; } = string.Empty;

    [MaxLength(256)]
    public string DropoffLocation { get; set; } = string.Empty;

    // Coordinates for mapping
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public double? DropoffLatitude { get; set; }
    public double? DropoffLongitude { get; set; }

    public DateTimeOffset PickupDate { get; set; } = DateTimeOffset.UtcNow;
    public double WeightLbs { get; set; }

    [Required]
    public string CustomerId { get; set; } = default!;
    public ApplicationUser? Customer { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
