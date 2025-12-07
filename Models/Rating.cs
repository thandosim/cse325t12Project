using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public class Rating
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid LoadId { get; set; }
    public Load? Load { get; set; }

    [Required]
    public string CustomerId { get; set; } = default!;
    public ApplicationUser? Customer { get; set; }

    [Required]
    public string DriverId { get; set; } = default!;
    public ApplicationUser? Driver { get; set; }

    [Required, Range(1, 5)]
    public int Stars { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
