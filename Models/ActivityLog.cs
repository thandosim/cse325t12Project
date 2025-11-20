using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public class ActivityLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }
    [MaxLength(160)]
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Metadata { get; set; }
}
