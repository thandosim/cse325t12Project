using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public class AvailabilitySlot
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    public string DriverId { get; set; } = default!;
    public ApplicationUser? Driver { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public bool IsOpen { get; set; } = true;
}
