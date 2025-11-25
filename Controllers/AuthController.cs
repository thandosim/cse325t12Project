using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using t12Project.Contracts.Auth;
using t12Project.Models;
using t12Project.Services;

namespace t12Project.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthTokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuthTokenService tokenService,
        ILogger<AuthController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (user.IsBlocked)
        {
            return Unauthorized(new { message = "This account is blocked. Contact support." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                return Unauthorized(new { message = "This account is locked. Try again later." });
            }

            return Unauthorized(new { message = "Invalid email or password." });
        }

        var authResult = await _tokenService.IssueTokensAsync(
            user,
            request.RememberMe,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        await _signInManager.SignInAsync(user, request.RememberMe);

        var response = await BuildAuthResponseAsync(user, authResult, cancellationToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RefreshAsync([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var token = await _tokenService.FindActiveRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (token is null)
        {
            return Unauthorized(new { message = "Refresh token is invalid or expired." });
        }

        var user = token.User ?? await _userManager.FindByIdAsync(token.UserId);
        if (user is null)
        {
            return Unauthorized(new { message = "Account no longer exists." });
        }

        var authResult = await _tokenService.RotateRefreshTokenAsync(
            token,
            user,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        await _signInManager.SignInAsync(user, token.IsPersistent);

        var response = await BuildAuthResponseAsync(user, authResult, cancellationToken);
        return Ok(response);
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> LogoutAsync([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var token = await _tokenService.FindActiveRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (token is null)
        {
            return Ok();
        }

        await _tokenService.RevokeRefreshTokenAsync(token, "User logout", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        await _signInManager.SignOutAsync();
        return Ok();
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> GetProfileAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            user.FullName,
            user.Email,
            Roles = roles,
            Dashboard = ResolveDashboardUrl(roles)
        });
    }

    [HttpGet("google/signin")]
    [AllowAnonymous]
    public IActionResult SignInWithGoogle([FromQuery] string? state)
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), values: new { state });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl);
        if (!string.IsNullOrWhiteSpace(state))
        {
            properties.Items["state"] = state;
        }

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback(CancellationToken cancellationToken)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return ErrorScriptResult("Unable to complete Google sign-in.");
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return ErrorScriptResult("Google account is missing an email address.");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email,
                AccountType = AccountRole.Customer
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var message = string.Join(' ', createResult.Errors.Select(e => e.Description));
                return ErrorScriptResult(message);
            }

            await _userManager.AddToRoleAsync(user, AccountRole.Customer.ToString());
        }

        var authResult = await _tokenService.IssueTokensAsync(
            user,
            rememberMe: true,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        await _signInManager.SignInAsync(user, isPersistent: true);

        var response = await BuildAuthResponseAsync(user, authResult, cancellationToken);
        return SuccessScriptResult(response, Request.Query["state"].ToString());
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user, AuthTokenResult tokenResult, CancellationToken cancellationToken)
    {
        var roles = tokenResult.Roles;
        if (roles.Length == 0)
        {
            roles = (await _userManager.GetRolesAsync(user)).ToArray();
        }

        return new AuthResponse(
            tokenResult.AccessToken,
            tokenResult.RefreshToken,
            tokenResult.AccessTokenExpiresAt,
            tokenResult.RefreshTokenExpiresAt,
            user.FullName,
            user.Email ?? string.Empty,
            roles,
            ResolveDashboardUrl(roles));
    }

    private static string ResolveDashboardUrl(IEnumerable<string> roles)
    {
        var roleSet = roles is string[] array ? array : roles.ToArray();

        if (roleSet.Contains("Admin"))
        {
            return "/admin";
        }

        if (roleSet.Contains("Driver"))
        {
            return "/dashboard/driver";
        }

        if (roleSet.Contains("Customer"))
        {
            return "/dashboard/customer";
        }

        return "/dashboard";
    }

    private ContentResult SuccessScriptResult(AuthResponse response, string? state)
    {
        var payload = JsonSerializer.Serialize(response);
        var stateJson = state is null ? "null" : JsonSerializer.Serialize(state);
        var script = $"<script>window.opener && window.opener.postMessage({{ type: 'loadhitch:auth', state: {stateJson}, payload: {payload} }}, '*'); window.close();</script>";
        return Content(script, "text/html");
    }

    private ContentResult ErrorScriptResult(string message)
    {
        var payload = JsonSerializer.Serialize(new { error = message });
        var script = $"<script>window.opener && window.opener.postMessage({{ type: 'loadhitch:auth-error', payload: {payload} }}, '*'); window.close();</script>";
        return Content(script, "text/html");
    }
}
