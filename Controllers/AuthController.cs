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

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RegisterAsync([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== AuthController.RegisterAsync START ===");
        _logger.LogInformation("Request: Email={Email}, FullName={FullName}, Role={Role}", request.Email, request.FullName, request.Role);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState invalid: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return ValidationProblem(ModelState);
        }

        _logger.LogInformation("Checking if user already exists...");
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            _logger.LogWarning("User already exists: {Email}", request.Email);
            return BadRequest(new { message = "An account with this email already exists. Please log in instead." });
        }

        _logger.LogInformation("Creating new user...");
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName,
            AccountType = request.Role
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(" ", createResult.Errors.Select(e => e.Description));
            _logger.LogWarning("User creation failed: {Errors}", errors);
            return BadRequest(new { message = errors });
        }

        _logger.LogInformation("User created, adding role: {Role}", request.Role);
        await _userManager.AddToRoleAsync(user, request.Role.ToString());

        _logger.LogInformation("Issuing tokens...");
        var authResult = await _tokenService.IssueTokensAsync(
            user,
            rememberMe: true,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        _logger.LogInformation("Signing in user...");
        await _signInManager.SignInAsync(user, isPersistent: true);

        var response = await BuildAuthResponseAsync(user, authResult, cancellationToken);
        _logger.LogInformation("=== AuthController.RegisterAsync SUCCESS ===");
        return Ok(response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== AuthController.LoginAsync START ===");
        _logger.LogInformation("Login attempt for: {Email}", request.Email);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState invalid");
            return ValidationProblem(ModelState);
        }

        _logger.LogInformation("Looking up user in database...");
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            _logger.LogWarning("User not found: {Email}", request.Email);
            return Unauthorized(new { message = "Invalid email or password." });
        }
        _logger.LogInformation("User found: {UserId}", user.Id);

        if (user.IsBlocked)
        {
            _logger.LogWarning("User is blocked: {Email}", request.Email);
            return Unauthorized(new { message = "This account is blocked. Contact support.", blocked = true, redirectUrl = "/account-blocked" });
        }

        _logger.LogInformation("Checking password...");
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Password check failed for: {Email}", request.Email);
            if (result.IsLockedOut)
            {
                return Unauthorized(new { message = "This account is locked. Try again later." });
            }

            return Unauthorized(new { message = "Invalid email or password." });
        }
        _logger.LogInformation("Password valid, issuing tokens...");

        var authResult = await _tokenService.IssueTokensAsync(
            user,
            request.RememberMe,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        _logger.LogInformation("Signing in user...");
        await _signInManager.SignInAsync(user, request.RememberMe);

        var response = await BuildAuthResponseAsync(user, authResult, cancellationToken);
        _logger.LogInformation("=== AuthController.LoginAsync SUCCESS ===");
        return Ok(response);
    }
    
    // TEST ENDPOINT - bypasses database for testing UI
    [HttpPost("test-login")]
    [AllowAnonymous]
    public ActionResult<AuthResponse> TestLogin([FromBody] LoginRequest request)
    {
        _logger.LogInformation("=== TEST LOGIN (no database) ===");
        _logger.LogInformation("Email: {Email}", request.Email);
        
        // Return mock response for testing
        var response = new AuthResponse(
            AccessToken: "test-access-token-" + Guid.NewGuid().ToString("N"),
            RefreshToken: "test-refresh-token-" + Guid.NewGuid().ToString("N"),
            AccessTokenExpiresAt: DateTime.UtcNow.AddHours(1),
            RefreshTokenExpiresAt: DateTime.UtcNow.AddDays(7),
            FullName: "Test User",
            Email: request.Email,
            Roles: new[] { "Customer" },
            DashboardUrl: "/dashboard/customer"
        );
        
        _logger.LogInformation("Returning mock response for: {Email}", request.Email);
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

    /// <summary>
    /// Simple GET logout endpoint that clears session and redirects to login page.
    /// Used by the Sign Out link in the navigation.
    /// </summary>
    [HttpGet("signout")]
    [AllowAnonymous]
    public async Task<IActionResult> SignOutAsync()
    {
        _logger.LogInformation("SignOut: Clearing session for user");
        
        // Sign out from ASP.NET Identity (clears cookies)
        await _signInManager.SignOutAsync();
        
        // Clear the authentication cookie explicitly
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        
        _logger.LogInformation("SignOut: Session cleared, redirecting to login");
        
        // Redirect to login page
        return Redirect("/login");
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
        var isNewUser = false;
        
        if (user is null)
        {
            isNewUser = true;
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email,
                AccountType = AccountRole.Customer // No Identity role assigned yet; updated when user picks a role
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var message = string.Join(' ', createResult.Errors.Select(e => e.Description));
                return ErrorScriptResult(message);
            }
            // Don't assign role yet - let user choose on SelectRole page
        }

        // Check if user is blocked - redirect to blocked page
        if (user.IsBlocked)
        {
            return BlockedScriptResult();
        }

        // Check if existing user has no role (incomplete registration)
        var existingRoles = await _userManager.GetRolesAsync(user);
        var needsRoleSelection = isNewUser || !existingRoles.Any();

        var authResult = await _tokenService.IssueTokensAsync(
            user,
            rememberMe: true,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        await _signInManager.SignInAsync(user, isPersistent: true);

        // If user needs to select role, redirect to role selection page
        if (needsRoleSelection)
        {
            return RoleSelectionScriptResult();
        }

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

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private ContentResult SuccessScriptResult(AuthResponse response, string? state)
    {
        var payload = JsonSerializer.Serialize(response, _jsonOptions);
        var stateJson = state is null ? "null" : JsonSerializer.Serialize(state);
        var targetOrigin = JsonSerializer.Serialize(GetPostMessageOrigin());
        var script = $"<script>window.opener && window.opener.postMessage({{ type: 'loadhitch:auth', state: {stateJson}, payload: {payload} }}, {targetOrigin}); window.close();</script>";
        return Content(script, "text/html");
    }

    private ContentResult ErrorScriptResult(string message)
    {
        var payload = JsonSerializer.Serialize(new { error = message }, _jsonOptions);
        var targetOrigin = JsonSerializer.Serialize(GetPostMessageOrigin());
        var script = $"<script>window.opener && window.opener.postMessage({{ type: 'loadhitch:auth-error', payload: {payload} }}, {targetOrigin}); window.close();</script>";
        return Content(script, "text/html");
    }

    private string GetPostMessageOrigin()
    {
        var origin = Request.Headers["Origin"].ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            origin = $"{Request.Scheme}://{Request.Host}";
        }

        return origin;
    }

    private ContentResult BlockedScriptResult()
    {
        // Close the popup and redirect the main window to the blocked page
        var script = "<script>window.opener && (window.opener.location.href = '/account-blocked'); window.close();</script>";
        return Content(script, "text/html");
    }

    private ContentResult RoleSelectionScriptResult()
    {
        // Close the popup and redirect the main window to the role selection page
        var script = "<script>window.opener && (window.opener.location.href = '/select-role'); window.close();</script>";
        return Content(script, "text/html");
    }
}
