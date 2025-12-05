using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public class DriverRoute
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string DriverId { get; set; } = default!;
    public ApplicationUser? Driver { get; set; }

    [Required, MaxLength(256)]
    public string StartLocation { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string EndLocation { get; set; } = string.Empty;

    // GPS Coordinates for map display
    public decimal StartLatitude { get; set; }
    public decimal StartLongitude { get; set; }
    public decimal EndLatitude { get; set; }
    public decimal EndLongitude { get; set; }

    public DateTimeOffset AvailableFrom { get; set; }
    public DateTimeOffset AvailableTo { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Equipment type they can carry on this route
    [MaxLength(60)]
    public string EquipmentType { get; set; } = "General";

    // Max weight they can carry
    public decimal MaxWeightLbs { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
