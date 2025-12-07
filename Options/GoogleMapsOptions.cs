namespace t12Project.Options
{
    /// <summary>
    /// Configuration options for Google Maps API
    /// Bound from appsettings.json [GoogleMaps] section
    /// </summary>
    public class GoogleMapsOptions
    {
        /// <summary>
        /// Configuration section name in appsettings.json
        /// </summary>
        public const string SectionName = "GoogleMaps";

        /// <summary>
        /// Google Maps API Key
        /// Loaded from User Secrets (development) or Azure Key Vault (production)
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}
