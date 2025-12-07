using t12Project.Data;
using t12Project.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using t12Project.Hubs;

namespace t12Project.Services;

/// <summary>
/// Service for tracking driver locations and calculating ETAs
/// </summary>
public class LocationTrackingService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<LoadTrackingHub> _hubContext;
    private readonly ILogger<LocationTrackingService> _logger;

    public LocationTrackingService(
        IServiceProvider serviceProvider,
        IHubContext<LoadTrackingHub> hubContext,
        ILogger<LocationTrackingService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Update driver's current location and broadcast to subscribers
    /// </summary>
    public async Task<LocationUpdate> UpdateDriverLocationAsync(
        string driverId, 
        decimal latitude, 
        decimal longitude,
        Guid? loadId = null)
    {
        var locationUpdate = new LocationUpdate
        {
            DriverId = driverId,
            Latitude = latitude,
            Longitude = longitude,
            ReportedAt = DateTimeOffset.UtcNow,
            LoadId = loadId
        };

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.LocationUpdates.Add(locationUpdate);
        await context.SaveChangesAsync();

        // Broadcast to SignalR clients if associated with a load
        if (loadId.HasValue)
        {
            await _hubContext.Clients.Group($"load_{loadId}")
                .SendAsync("ReceiveLocationUpdate", new
                {
                    LoadId = loadId,
                    DriverId = driverId,
                    Latitude = latitude,
                    Longitude = longitude,
                    Timestamp = locationUpdate.ReportedAt
                });

            _logger.LogInformation("Location updated for driver {DriverId} on load {LoadId}", driverId, loadId);
        }

        return locationUpdate;
    }

    /// <summary>
    /// Get driver's latest location
    /// </summary>
    public async Task<LocationUpdate?> GetDriverLatestLocationAsync(string driverId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.LocationUpdates
            .Where(l => l.DriverId == driverId)
            .OrderByDescending(l => l.ReportedAt)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get location history for a driver
    /// </summary>
    public async Task<List<LocationUpdate>> GetDriverLocationHistoryAsync(
        string driverId, 
        DateTimeOffset since)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.LocationUpdates
            .Where(l => l.DriverId == driverId && l.ReportedAt >= since)
            .OrderBy(l => l.ReportedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get all location updates for a specific load
    /// </summary>
    public async Task<List<LocationUpdate>> GetLoadLocationHistoryAsync(Guid loadId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.LocationUpdates
            .Where(l => l.LoadId == loadId)
            .OrderBy(l => l.ReportedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Calculate distance between two points using Haversine formula (in kilometers)
    /// </summary>
    public double CalculateDistance(
        decimal lat1, decimal lon1, 
        decimal lat2, decimal lon2)
    {
        const double R = 6371; // Earth's radius in kilometers

        var dLat = DegreesToRadians((double)(lat2 - lat1));
        var dLon = DegreesToRadians((double)(lon2 - lon1));

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians((double)lat1)) * 
                Math.Cos(DegreesToRadians((double)lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    /// <summary>
    /// Calculate ETA based on current location and destination
    /// Assumes average speed of 60 km/h
    /// </summary>
    public async Task<int> CalculateETAAsync(
        string driverId,
        decimal destinationLat,
        decimal destinationLon,
        double averageSpeedKmh = 60.0)
    {
        var currentLocation = await GetDriverLatestLocationAsync(driverId);
        
        if (currentLocation == null)
        {
            _logger.LogWarning("No location data available for driver {DriverId}", driverId);
            return 0;
        }

        var distanceKm = CalculateDistance(
            currentLocation.Latitude, currentLocation.Longitude,
            destinationLat, destinationLon);

        // Calculate time in minutes
        var timeHours = distanceKm / averageSpeedKmh;
        var timeMinutes = (int)Math.Ceiling(timeHours * 60);

        _logger.LogInformation(
            "ETA calculated for driver {DriverId}: {Minutes} minutes ({Distance} km at {Speed} km/h)",
            driverId, timeMinutes, distanceKm, averageSpeedKmh);

        return timeMinutes;
    }

    /// <summary>
    /// Update ETA for a load based on driver's current location
    /// </summary>
    public async Task<int> UpdateLoadETAAsync(Guid loadId, string driverId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var load = await context.Loads.FindAsync(loadId);
        if (load == null)
        {
            throw new InvalidOperationException($"Load {loadId} not found");
        }

        if (load.AssignedDriverId != driverId)
        {
            throw new UnauthorizedAccessException("Driver is not assigned to this load");
        }

        // Calculate ETA to dropoff location
        var eta = await CalculateETAAsync(
            driverId,
            load.DropoffLatitude,
            load.DropoffLongitude);

        load.EstimatedTimeOfArrivalMinutes = eta;
        await context.SaveChangesAsync();

        // Broadcast ETA update
        await _hubContext.Clients.Group($"load_{loadId}")
            .SendAsync("ReceiveETAUpdate", new
            {
                LoadId = loadId,
                EstimatedMinutes = eta,
                Timestamp = DateTimeOffset.UtcNow
            });

        _logger.LogInformation("ETA updated for load {LoadId}: {Minutes} minutes", loadId, eta);

        return eta;
    }

    /// <summary>
    /// Clean up old location data (older than 30 days)
    /// </summary>
    public async Task CleanupOldLocationDataAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-30);
        var oldLocations = await context.LocationUpdates
            .Where(l => l.ReportedAt < cutoffDate)
            .ToListAsync();

        context.LocationUpdates.RemoveRange(oldLocations);
        var count = await context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} old location records", count);
    }

    private double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
