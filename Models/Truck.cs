using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public class Truck
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    public string DriverId { get; set; } = default!;
    public ApplicationUser? Driver { get; set; }
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(40)]
    public string EquipmentType { get; set; } = string.Empty;
    public decimal CapacityLbs { get; set; }
    public bool IsActive { get; set; } = true;
}
