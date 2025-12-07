using t12Project.Data;
using t12Project.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using t12Project.Hubs;

namespace t12Project.Services;

/// <summary>
/// Service for sending real-time notifications via SignalR and persisting to database
/// </summary>
public class RealtimeNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<LoadTrackingHub> _hubContext;
    private readonly ILogger<RealtimeNotificationService> _logger;

    public RealtimeNotificationService(
        ApplicationDbContext context,
        IHubContext<LoadTrackingHub> hubContext,
        ILogger<RealtimeNotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Send notification to a specific user
    /// </summary>
    public async Task SendNotificationAsync(
        string userId,
        string title,
        string message,
        string type = "Info",
        string? actionUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            CreatedAt = DateTimeOffset.UtcNow,
            IsRead = false
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Send real-time notification via SignalR
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("ReceiveNotification", new
            {
                notification.Id,
                notification.Title,
                notification.Message,
                notification.Type,
                notification.ActionUrl,
                notification.CreatedAt
            });

        _logger.LogInformation("Notification sent to user {UserId}: {Title}", userId, title);
    }

    /// <summary>
    /// Send notification when driver accepts a load
    /// </summary>
    public async Task NotifyLoadAcceptedAsync(Load load, string driverName, int estimatedMinutes)
    {
        await SendNotificationAsync(
            load.CustomerId,
            "Load Accepted",
            $"{driverName} has accepted your load. Estimated arrival: {estimatedMinutes} minutes.",
            "Success",
            $"/load/{load.Id}");

        // Broadcast to load subscribers
        await _hubContext.Clients.Group($"load_{load.Id}")
            .SendAsync("LoadStatusChanged", new
            {
                LoadId = load.Id,
                Status = "Accepted",
                Message = $"Driver {driverName} accepted the load",
                EstimatedMinutes = estimatedMinutes,
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Send notification when driver picks up the load
    /// </summary>
    public async Task NotifyLoadPickedUpAsync(Load load, string driverName)
    {
        await SendNotificationAsync(
            load.CustomerId,
            "Load Picked Up",
            $"{driverName} has picked up your load.",
            "Info",
            $"/load/{load.Id}");

        await _hubContext.Clients.Group($"load_{load.Id}")
            .SendAsync("LoadStatusChanged", new
            {
                LoadId = load.Id,
                Status = "PickedUp",
                Message = $"Driver {driverName} picked up the load",
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Send notification when driver starts transit
    /// </summary>
    public async Task NotifyLoadInTransitAsync(Load load, string driverName, int estimatedMinutes)
    {
        await SendNotificationAsync(
            load.CustomerId,
            "Load In Transit",
            $"{driverName} is on the way with your load. ETA: {estimatedMinutes} minutes.",
            "Info",
            $"/load/{load.Id}");

        await _hubContext.Clients.Group($"load_{load.Id}")
            .SendAsync("LoadStatusChanged", new
            {
                LoadId = load.Id,
                Status = "InTransit",
                Message = $"Load is in transit",
                EstimatedMinutes = estimatedMinutes,
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Send notification when driver delivers the load
    /// </summary>
    public async Task NotifyLoadDeliveredAsync(Load load, string driverName)
    {
        await SendNotificationAsync(
            load.CustomerId,
            "Load Delivered",
            $"{driverName} has delivered your load. Please confirm completion.",
            "Success",
            $"/load/{load.Id}");

        await _hubContext.Clients.Group($"load_{load.Id}")
            .SendAsync("LoadStatusChanged", new
            {
                LoadId = load.Id,
                Status = "Delivered",
                Message = "Load has been delivered",
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Send notification when customer confirms completion
    /// </summary>
    public async Task NotifyLoadCompletedAsync(Load load, string customerName)
    {
        if (!string.IsNullOrEmpty(load.AssignedDriverId))
        {
            await SendNotificationAsync(
                load.AssignedDriverId,
                "Load Completed",
                $"{customerName} has confirmed delivery. Payment will be processed.",
                "Success",
                $"/load/{load.Id}");
        }

        await _hubContext.Clients.Group($"load_{load.Id}")
            .SendAsync("LoadStatusChanged", new
            {
                LoadId = load.Id,
                Status = "Completed",
                Message = "Load has been completed",
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Send notification when load is cancelled
    /// </summary>
    public async Task NotifyLoadCancelledAsync(Load load, string cancelledBy, string reason)
    {
        // Notify customer if driver cancelled
        if (!string.IsNullOrEmpty(load.AssignedDriverId) && cancelledBy == load.AssignedDriverId)
        {
            await SendNotificationAsync(
                load.CustomerId,
                "Load Cancelled",
                $"Driver cancelled the load. Reason: {reason}",
                "Warning",
                $"/load/{load.Id}");
        }
        // Notify driver if customer cancelled
        else if (!string.IsNullOrEmpty(load.AssignedDriverId))
        {
            await SendNotificationAsync(
                load.AssignedDriverId,
                "Load Cancelled",
                $"Customer cancelled the load. Reason: {reason}",
                "Warning",
                $"/load/{load.Id}");
        }

        await _hubContext.Clients.Group($"load_{load.Id}")
            .SendAsync("LoadStatusChanged", new
            {
                LoadId = load.Id,
                Status = "Cancelled",
                Message = $"Load cancelled: {reason}",
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Send notification when driver arrives at pickup location
    /// </summary>
    public async Task NotifyDriverArrivedAtPickupAsync(Load load, string driverName)
    {
        await SendNotificationAsync(
            load.CustomerId,
            "Driver Arrived",
            $"{driverName} has arrived at the pickup location.",
            "Info",
            $"/load/{load.Id}");

        await _hubContext.Clients.Group($"load_{load.Id}")
            .SendAsync("LoadStatusChanged", new
            {
                LoadId = load.Id,
                Status = "DriverArrived",
                Message = $"Driver {driverName} arrived at pickup location",
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Send notification when driver arrives at dropoff location
    /// </summary>
    public async Task NotifyDriverArrivedAtDropoffAsync(Load load, string driverName)
    {
        await SendNotificationAsync(
            load.CustomerId,
            "Driver Arrived at Destination",
            $"{driverName} has arrived at the delivery location.",
            "Info",
            $"/load/{load.Id}");

        await _hubContext.Clients.Group($"load_{load.Id}")
            .SendAsync("LoadStatusChanged", new
            {
                LoadId = load.Id,
                Status = "DriverArrivedAtDropoff",
                Message = $"Driver {driverName} arrived at delivery location",
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Send ETA update notification
    /// </summary>
    public async Task NotifyETAUpdateAsync(Guid loadId, string customerId, int newETA)
    {
        await SendNotificationAsync(
            customerId,
            "ETA Updated",
            $"Estimated arrival time updated: {newETA} minutes.",
            "Info",
            $"/load/{loadId}");

        await _hubContext.Clients.Group($"load_{loadId}")
            .SendAsync("ReceiveETAUpdate", new
            {
                LoadId = loadId,
                EstimatedMinutes = newETA,
                Timestamp = DateTimeOffset.UtcNow
            });
    }
}
