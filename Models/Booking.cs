using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public class Booking
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LoadId { get; set; }
    public Load? Load { get; set; }
    [Required]
    public string DriverId { get; set; } = default!;
    public ApplicationUser? Driver { get; set; }
    [MaxLength(40)]
    public string Status { get; set; } = "Requested";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
