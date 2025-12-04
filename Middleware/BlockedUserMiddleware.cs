using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using t12Project.Models;

namespace t12Project.Middleware;

/// <summary>
/// Middleware that checks if the current user is blocked and signs them out if so.
/// </summary>
public class BlockedUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BlockedUserMiddleware> _logger;

    public BlockedUserMiddleware(RequestDelegate next, ILogger<BlockedUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        // Only check authenticated users
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user is not null && user.IsBlocked)
                {
                    _logger.LogWarning("Blocked user {UserId} attempted to access {Path}", userId, context.Request.Path);
                    
                    // Sign out the blocked user
                    await signInManager.SignOutAsync();

                    // Redirect to account blocked page, avoiding redirect loops
                    if (!context.Request.Path.Equals("/account-blocked", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.Redirect("/account-blocked");
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to add the middleware to the pipeline.
/// </summary>
public static class BlockedUserMiddlewareExtensions
{
    public static IApplicationBuilder UseBlockedUserCheck(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<BlockedUserMiddleware>();
    }
}
