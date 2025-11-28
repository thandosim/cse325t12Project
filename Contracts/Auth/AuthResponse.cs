namespace t12Project.Contracts.Auth;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    string FullName,
    string Email,
    string[] Roles,
    string DashboardUrl
);
