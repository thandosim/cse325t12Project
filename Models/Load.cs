using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace t12Project.Models;

public class Load
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(60)]
    public string Status { get; set; } = "Draft";
    [MaxLength(256)]
    public string PickupLocation { get; set; } = string.Empty;
    [MaxLength(256)]
    public string DropoffLocation { get; set; } = string.Empty;
    public DateTimeOffset PickupDate { get; set; } = DateTimeOffset.UtcNow;
    public double WeightLbs { get; set; }
    [Required]
    public string CustomerId { get; set; } = default!;
    public ApplicationUser? Customer { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
