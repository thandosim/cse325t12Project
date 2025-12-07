using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using t12Project.Models;
using System.Security.Claims;

namespace t12Project.Hubs;

/// <summary>
/// SignalR hub for real-time load tracking and notifications
/// </summary>
[Authorize]
public class LoadTrackingHub : Hub
{
    private readonly ILogger<LoadTrackingHub> _logger;

    public LoadTrackingHub(ILogger<LoadTrackingHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            // Add user to their personal group for targeted notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} connected to LoadTrackingHub", userId);
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} disconnected from LoadTrackingHub", userId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to real-time updates for a specific load
    /// </summary>
    public async Task SubscribeToLoad(Guid loadId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"load_{loadId}");
        _logger.LogInformation("Connection {ConnectionId} subscribed to load {LoadId}", Context.ConnectionId, loadId);
    }

    /// <summary>
    /// Unsubscribe from a load's updates
    /// </summary>
    public async Task UnsubscribeFromLoad(Guid loadId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"load_{loadId}");
        _logger.LogInformation("Connection {ConnectionId} unsubscribed from load {LoadId}", Context.ConnectionId, loadId);
    }

    /// <summary>
    /// Update driver's current location (called from driver's device)
    /// </summary>
    public async Task UpdateDriverLocation(Guid loadId, double latitude, double longitude)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthorized location update attempt");
            return;
        }

        // Broadcast location update to all clients watching this load
        await Clients.Group($"load_{loadId}").SendAsync("ReceiveLocationUpdate", new
        {
            LoadId = loadId,
            DriverId = userId,
            Latitude = latitude,
            Longitude = longitude,
            Timestamp = DateTimeOffset.UtcNow
        });

        _logger.LogInformation("Driver {DriverId} updated location for load {LoadId}: ({Lat}, {Lng})", 
            userId, loadId, latitude, longitude);
    }
}
