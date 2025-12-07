using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public class LocationUpdate
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string DriverId { get; set; } = default!;
    public ApplicationUser? Driver { get; set; }
    
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Optional: Link to specific load if tracking during delivery
    public Guid? LoadId { get; set; }
    public Load? Load { get; set; }
    
    public string? Notes { get; set; }
}
