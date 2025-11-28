using System.ComponentModel.DataAnnotations;
using t12Project.Models;

namespace t12Project.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required]
    [StringLength(80)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords must match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public AccountRole Role { get; set; } = AccountRole.Customer;
}
