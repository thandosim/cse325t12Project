using System.ComponentModel.DataAnnotations;

namespace t12Project.Contracts.Auth;

public sealed class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
