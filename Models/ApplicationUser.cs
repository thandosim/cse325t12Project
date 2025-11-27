using Microsoft.AspNetCore.Identity;

namespace t12Project.Models;

public enum AccountRole
{
    Admin,
    Driver,
    Customer
}

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public AccountRole AccountType { get; set; } = AccountRole.Customer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsBlocked => LockoutEnd.HasValue && LockoutEnd > DateTimeOffset.UtcNow;

    public List<RefreshToken> RefreshTokens { get; set; } = new();
}
