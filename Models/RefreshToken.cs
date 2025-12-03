using System.ComponentModel.DataAnnotations;

namespace t12Project.Models;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(256)]
    public string TokenHash { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ReplacedByTokenHash { get; set; }

    [MaxLength(128)]
    public string? CreatedByIp { get; set; }

    [MaxLength(256)]
    public string? UserAgent { get; set; }

    [MaxLength(128)]
    public string? RevokedByIp { get; set; }

    public string? ReasonRevoked { get; set; }

    public bool IsPersistent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;
}
