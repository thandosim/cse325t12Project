namespace t12Project.Options;

/// <summary>
/// Configuration options for Mapbox API integration.
/// Binds to the "Mapbox" section in appsettings.json or environment variables.
/// </summary>
public class MapboxOptions
{
    /// <summary>
    /// Configuration section name for Mapbox settings.
    /// Use "Mapbox:AccessToken" in appsettings.json and User Secrets.
    /// Use "Mapbox__AccessToken" in Azure App Settings (environment variables).
    /// </summary>
    public const string SectionName = "Mapbox";

    /// <summary>
    /// Mapbox public access token for map rendering and geocoding.
    /// Format: pk.ey...
    /// Should be stored securely using User Secrets (local) or Azure App Settings (production).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;
}
