using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using t12Project.Data;
using t12Project.Models;

namespace t12Project.Services;

public class AdminUserService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

    public async Task<PagedResult<AdminUserSummary>> GetUsersAsync(int page = 1, int pageSize = 25, string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var now = DateTimeOffset.UtcNow;

        var query = _userManager.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(search)) ||
                (u.FullName != null && u.FullName.ToLower().Contains(search)));
        }

        var total = await query.CountAsync();

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserSummary(
                u.Id,
                u.FullName,
                u.Email ?? string.Empty,
                u.AccountType.ToString(),
                u.LockoutEnd.HasValue && u.LockoutEnd > now))
            .ToListAsync();

        return new PagedResult<AdminUserSummary>(users, total, page, pageSize);
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
        await LogAsync($"Created user {request.Email} with role {request.Role}");
        return OperationResult.Success("User created");
    }

    public async Task<OperationResult> UpdateRoleAsync(string userId, AccountRole role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        var actingUserId = GetActingUserId();
        if (!string.IsNullOrEmpty(actingUserId) && string.Equals(userId, actingUserId, StringComparison.Ordinal))
        {
            await LogAsync($"Blocked attempt to change own role for user {user.Email}");
            return OperationResult.Failure("You cannot change your own role.");
        }

        if (await IsOnlyAdminAsync(user) && role != AccountRole.Admin)
        {
            await LogAsync($"Blocked attempt to demote last admin: {user.Email}");
            return OperationResult.Failure("You cannot demote the last remaining admin.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, roles);
        }

        await _userManager.AddToRoleAsync(user, role.ToString());
        user.AccountType = role;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return OperationResult.Failure(string.Join(" ", update.Errors.Select(e => e.Description)));
        }

        await LogAsync($"Updated role for {user.Email} to {role}");
        return OperationResult.Success("Role updated");
    }

    public async Task<OperationResult> BlockUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        var actingUserId = GetActingUserId();
        if (!string.IsNullOrEmpty(actingUserId) && string.Equals(userId, actingUserId, StringComparison.Ordinal))
        {
            await LogAsync($"Blocked attempt to block own account for user {user.Email}");
            return OperationResult.Failure("You cannot block your own account.");
        }

        // Prevent blocking admin users
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        if (isAdmin)
        {
            await LogAsync($"Blocked attempt to block admin user {user.Email}");
            return OperationResult.Failure("Admin users cannot be blocked.");
        }

        // Enable lockout and set to far future
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return OperationResult.Failure(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await LogAsync($"Blocked user {user.Email}");
        return OperationResult.Success("User blocked successfully");
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

        if (!result.Succeeded)
        {
            return OperationResult.Failure(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await LogAsync($"Unblocked user {user.Email}");
        return OperationResult.Success("User unblocked successfully");
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

        if (!result.Succeeded)
        {
            return OperationResult.Failure(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await LogAsync($"Updated user {user.Email}");
        return OperationResult.Success("User updated successfully");
    }

    public async Task<OperationResult> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        var actingUserId = GetActingUserId();
        if (!string.IsNullOrEmpty(actingUserId) && string.Equals(userId, actingUserId, StringComparison.Ordinal))
        {
            await LogAsync($"Blocked attempt to delete own account for user {user.Email}");
            return OperationResult.Failure("You cannot delete your own account.");
        }

        if (await IsOnlyAdminAsync(user))
        {
            await LogAsync($"Blocked attempt to delete last admin: {user.Email}");
            return OperationResult.Failure("You cannot delete the last remaining admin.");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return OperationResult.Failure(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await LogAsync($"Deleted user {user.Email}");
        return OperationResult.Success("User deleted");
    }

    private async Task<bool> IsOnlyAdminAsync(ApplicationUser user)
    {
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        if (!isAdmin)
        {
            return false;
        }

        var adminCount = await _userManager.GetUsersInRoleAsync("Admin");
        return adminCount.Count == 1;
    }

    private string? GetActingUserId()
    {
        return _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private async Task LogAsync(string action, string? metadata = null)
    {
        var userId = GetActingUserId() ?? string.Empty;
        _dbContext.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            Action = action,
            Metadata = metadata
        });
        await _dbContext.SaveChangesAsync();
    }
}

public record AdminUserSummary(string Id, string Name, string Email, string Role, bool IsBlocked);

public record AdminCreateUserRequest(string FullName, string Email, string Password, AccountRole Role);

public record OperationResult(bool Succeeded, string Message)
{
    public static OperationResult Success(string message = "Success") => new(true, message);
    public static OperationResult Failure(string message) => new(false, message);
}
