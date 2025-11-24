# Security Configuration

## Google Maps API Key Setup

This project uses Google Maps Embed API for map functionality. You need to provide your own API key.

### Steps:

1. **Get a Google Maps API Key:**
   - Go to [Google Cloud Console](https://console.cloud.google.com/apis/credentials)
   - Create a new project or select existing
   - Enable "Maps Embed API"
   - Create credentials (API Key)
   - Restrict the key to your domain (recommended)

2. **Configure the API Key:**
   - Copy `appsettings.Development.example.json` to `appsettings.Development.json`
   - Replace `REPLACE_WITH_YOUR_ACTUAL_GOOGLE_MAPS_API_KEY` with your actual key
   - **Never commit `appsettings.Development.json` if it contains real keys**

3. **For Production:**
   - Use environment variables or Azure Key Vault
   - Never hardcode API keys in source code

## Environment Variables

Alternatively, set the API key as an environment variable:

```bash
export GoogleMaps__ApiKey="your-api-key-here"
```

Or in Windows PowerShell:
```powershell
$env:GoogleMaps__ApiKey="your-api-key-here"
```
