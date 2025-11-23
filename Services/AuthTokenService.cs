using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using t12Project.Data;
using t12Project.Models;
using t12Project.Options;

namespace t12Project.Services;

public interface IAuthTokenService
{
    Task<AuthTokenResult> IssueTokensAsync(ApplicationUser user, bool rememberMe, string? createdByIp, string? userAgent, CancellationToken cancellationToken = default);
    Task<RefreshToken?> FindActiveRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(RefreshToken token, string reason, string? revokedByIp, CancellationToken cancellationToken = default);
    Task<AuthTokenResult> RotateRefreshTokenAsync(RefreshToken token, ApplicationUser user, string? createdByIp, string? userAgent, CancellationToken cancellationToken = default);
}

public sealed record AuthTokenResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string[] Roles);

public sealed class AuthTokenService : IAuthTokenService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public AuthTokenService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthTokenResult> IssueTokensAsync(ApplicationUser user, bool rememberMe, string? createdByIp, string? userAgent, CancellationToken cancellationToken = default)
    {
        var roles = (await _userManager.GetRolesAsync(user)).ToArray();
        var accessTokenExpiration = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("fullName", user.FullName ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var jwt = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: accessTokenExpiration,
            signingCredentials: credentials);

        var accessToken = _tokenHandler.WriteToken(jwt);

        var refreshLifetime = rememberMe
            ? TimeSpan.FromDays(_jwtOptions.RefreshTokenDaysRememberMe)
            : TimeSpan.FromDays(_jwtOptions.RefreshTokenDays);

        var (refreshTokenValue, refreshTokenEntity) = await CreateRefreshTokenAsync(
            user,
            refreshLifetime,
            createdByIp,
            userAgent,
            rememberMe,
            cancellationToken);

        return new AuthTokenResult(
            accessToken,
            accessTokenExpiration,
            refreshTokenValue,
            refreshTokenEntity.ExpiresAt,
            roles);
    }

    public async Task<RefreshToken?> FindActiveRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(rawToken);
        return await _dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == tokenHash &&
                t.RevokedAt == null &&
                t.ExpiresAt > DateTime.UtcNow,
                cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken token, string reason, string? revokedByIp, CancellationToken cancellationToken = default)
    {
        token.RevokedAt = DateTime.UtcNow;
        token.ReasonRevoked = reason;
        token.RevokedByIp = revokedByIp;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthTokenResult> RotateRefreshTokenAsync(RefreshToken token, ApplicationUser user, string? createdByIp, string? userAgent, CancellationToken cancellationToken = default)
    {
        var result = await IssueTokensAsync(user, token.IsPersistent, createdByIp, userAgent, cancellationToken);

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = createdByIp;
        token.ReasonRevoked = "Token rotated";
        token.ReplacedByTokenHash = HashToken(result.RefreshToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task<(string RawToken, RefreshToken Entity)> CreateRefreshTokenAsync(
        ApplicationUser user,
        TimeSpan lifetime,
        string? createdByIp,
        string? userAgent,
        bool isPersistent,
        CancellationToken cancellationToken)
    {
        var rawValue = GenerateSecureToken();
        var tokenHash = HashToken(rawValue);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            CreatedByIp = createdByIp,
            UserAgent = userAgent,
            IsPersistent = isPersistent
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (rawValue, refreshToken);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
