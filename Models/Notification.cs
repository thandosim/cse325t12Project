using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public class Notification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Type { get; set; } = "Info"; // Info, LoadPosted, BookingRequest, BookingAccepted, etc.

    public bool IsRead { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Optional: Link to related entity
    public Guid? RelatedEntityId { get; set; }

    [MaxLength(50)]
    public string? RelatedEntityType { get; set; } // "Load", "Booking", "Driver", etc.

    [MaxLength(200)]
    public string? ActionUrl { get; set; } // URL to navigate to when notification is clicked
}
