using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using t12Project.Models;

namespace t12Project.Services;

public class AdminUserService(UserManager<ApplicationUser> userManager)
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public async Task<IReadOnlyList<AdminUserSummary>> GetUsersAsync()
    {
        var now = DateTimeOffset.UtcNow;
        return await _userManager.Users
            .OrderBy(u => u.Email)
            .Select(u => new AdminUserSummary(
                u.Id, 
                u.FullName, 
                u.Email ?? string.Empty, 
                u.AccountType.ToString(), 
                u.LockoutEnd.HasValue && u.LockoutEnd > now))
            .ToListAsync();
    }

    public async Task<OperationResult> CreateUserAsync(AdminCreateUserRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true,
            AccountType = request.Role
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var message = string.Join(" ", result.Errors.Select(e => e.Description));
            return OperationResult.Failure(message);
        }

        await _userManager.AddToRoleAsync(user, request.Role.ToString());
        return OperationResult.Success("User created");
    }

    public async Task<OperationResult> UpdateRoleAsync(string userId, AccountRole role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, roles);
        }

        await _userManager.AddToRoleAsync(user, role.ToString());
        user.AccountType = role;
        var update = await _userManager.UpdateAsync(user);
        return update.Succeeded
            ? OperationResult.Success("Role updated")
            : OperationResult.Failure(string.Join(" ", update.Errors.Select(e => e.Description)));
    }

    public async Task<OperationResult> BlockUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        // Prevent blocking admin users
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        if (isAdmin)
        {
            return OperationResult.Failure("Admin users cannot be blocked.");
        }

        // Enable lockout and set to far future
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        var result = await _userManager.UpdateAsync(user);
        
        return result.Succeeded 
            ? OperationResult.Success("User blocked successfully") 
            : OperationResult.Failure(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task<OperationResult> UnblockUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        // Clear lockout
        user.LockoutEnd = null;
        var result = await _userManager.UpdateAsync(user);
        
        return result.Succeeded 
            ? OperationResult.Success("User unblocked successfully") 
            : OperationResult.Failure(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId);
    }

    public async Task<OperationResult> UpdateUserAsync(string userId, string fullName, string email)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        user.FullName = fullName;
        
        // If email changed, update it
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = email;
            user.UserName = email;
            user.NormalizedEmail = email.ToUpperInvariant();
            user.NormalizedUserName = email.ToUpperInvariant();
        }

        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded 
            ? OperationResult.Success("User updated successfully") 
            : OperationResult.Failure(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task<OperationResult> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded
            ? OperationResult.Success("User deleted")
            : OperationResult.Failure(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}

public record AdminUserSummary(string Id, string Name, string Email, string Role, bool IsBlocked);

public record AdminCreateUserRequest(string FullName, string Email, string Password, AccountRole Role);

public record OperationResult(bool Succeeded, string Message)
{
    public static OperationResult Success(string message = "Success") => new(true, message);
    public static OperationResult Failure(string message) => new(false, message);
}
