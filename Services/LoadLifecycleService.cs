using Microsoft.EntityFrameworkCore;
using t12Project.Data;
using t12Project.Models;

namespace t12Project.Services;

/// <summary>
/// Manages load lifecycle: acceptance, pickup, transit, delivery, completion
/// </summary>
public class LoadLifecycleService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LoadLifecycleService> _logger;
    private readonly RealtimeNotificationService _notificationService;
    private readonly LocationTrackingService _locationService;

    public LoadLifecycleService(
        ApplicationDbContext context,
        ILogger<LoadLifecycleService> logger,
        RealtimeNotificationService notificationService,
        LocationTrackingService locationService)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
        _locationService = locationService;
    }

    /// <summary>
    /// Driver accepts a load
    /// </summary>
    public async Task<(bool success, string message)> AcceptLoadAsync(Guid loadId, string driverId, int estimatedMinutes = 30)
    {
        var load = await _context.Loads
            .Include(l => l.Customer)
            .FirstOrDefaultAsync(l => l.Id == loadId);

        if (load == null)
            return (false, "Load not found");

        if (load.Status != "Available")
            return (false, $"Load is not available (current status: {load.Status})");

        // Update load
        load.Status = "Accepted";
        load.AssignedDriverId = driverId;
        load.AcceptedAt = DateTimeOffset.UtcNow;
        load.EstimatedTimeOfArrivalMinutes = estimatedMinutes;

        // Create or update booking
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.LoadId == loadId && b.DriverId == driverId);

        if (booking == null)
        {
            booking = new Booking
            {
                LoadId = loadId,
                DriverId = driverId,
                Status = "Accepted",
                RespondedAt = DateTimeOffset.UtcNow
            };
            _context.Bookings.Add(booking);
        }
        else
        {
            booking.Status = "Accepted";
            booking.RespondedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Get driver name for notification
        var driver = await _context.Users.FindAsync(driverId);
        var driverName = driver?.UserName ?? "Driver";

        // Send real-time notification
        await _notificationService.NotifyLoadAcceptedAsync(load, driverName, estimatedMinutes);

        _logger.LogInformation("Driver {DriverId} accepted load {LoadId}", driverId, loadId);
        return (true, "Load accepted successfully");
    }

    /// <summary>
    /// Driver marks arrival at pickup location
    /// </summary>
    public async Task<(bool success, string message)> NotifyArrivalAtPickupAsync(Guid loadId, string driverId)
    {
        var load = await _context.Loads.FirstOrDefaultAsync(l => l.Id == loadId);

        if (load == null)
            return (false, "Load not found");

        if (load.AssignedDriverId != driverId)
            return (false, "You are not assigned to this load");

        if (load.Status != "Accepted")
            return (false, $"Load must be accepted to notify arrival (current status: {load.Status})");

        // Get driver name and send notification
        var driver = await _context.Users.FindAsync(driverId);
        var driverName = driver?.UserName ?? "Driver";
        await _notificationService.NotifyDriverArrivedAtPickupAsync(load, driverName);

        _logger.LogInformation("Driver {DriverId} arrived at pickup for load {LoadId}", driverId, loadId);
        return (true, "Customer notified of your arrival");
    }

    /// <summary>
    /// Driver marks arrival at dropoff location
    /// </summary>
    public async Task<(bool success, string message)> NotifyArrivalAtDropoffAsync(Guid loadId, string driverId)
    {
        var load = await _context.Loads.FirstOrDefaultAsync(l => l.Id == loadId);

        if (load == null)
            return (false, "Load not found");

        if (load.AssignedDriverId != driverId)
            return (false, "You are not assigned to this load");

        if (load.Status != "InTransit" && load.Status != "PickedUp")
            return (false, $"Invalid load status for arrival notification (current status: {load.Status})");

        // Get driver name and send notification
        var driver = await _context.Users.FindAsync(driverId);
        var driverName = driver?.UserName ?? "Driver";
        await _notificationService.NotifyDriverArrivedAtDropoffAsync(load, driverName);

        _logger.LogInformation("Driver {DriverId} arrived at dropoff for load {LoadId}", driverId, loadId);
        return (true, "Customer notified of your arrival");
    }

    /// <summary>
    /// Driver marks load as picked up
    /// </summary>
    public async Task<(bool success, string message)> PickupLoadAsync(Guid loadId, string driverId)
    {
        var load = await _context.Loads.FirstOrDefaultAsync(l => l.Id == loadId);

        if (load == null)
            return (false, "Load not found");

        if (load.AssignedDriverId != driverId)
            return (false, "You are not assigned to this load");

        if (load.Status != "Accepted")
            return (false, $"Load must be accepted before pickup (current status: {load.Status})");

        load.Status = "PickedUp";
        load.PickedUpAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        // Get driver name and send notification
        var driver = await _context.Users.FindAsync(driverId);
        var driverName = driver?.UserName ?? "Driver";
        await _notificationService.NotifyLoadPickedUpAsync(load, driverName);

        _logger.LogInformation("Driver {DriverId} picked up load {LoadId}", driverId, loadId);
        return (true, "Load marked as picked up");
    }

    /// <summary>
    /// Driver marks load as in transit
    /// </summary>
    public async Task<(bool success, string message)> StartTransitAsync(Guid loadId, string driverId)
    {
        var load = await _context.Loads.FirstOrDefaultAsync(l => l.Id == loadId);

        if (load == null)
            return (false, "Load not found");

        if (load.AssignedDriverId != driverId)
            return (false, "You are not assigned to this load");

        if (load.Status != "PickedUp")
            return (false, $"Load must be picked up before transit (current status: {load.Status})");

        load.Status = "InTransit";

        await _context.SaveChangesAsync();

        // Calculate and update ETA based on current location
        var eta = load.EstimatedTimeOfArrivalMinutes ?? 30;
        try
        {
            eta = await _locationService.UpdateLoadETAAsync(loadId, driverId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not calculate ETA for load {LoadId}, using default", loadId);
        }

        // Get driver name and send notification
        var driver = await _context.Users.FindAsync(driverId);
        var driverName = driver?.UserName ?? "Driver";
        await _notificationService.NotifyLoadInTransitAsync(load, driverName, eta);

        _logger.LogInformation("Driver {DriverId} started transit for load {LoadId}", driverId, loadId);
        return (true, "Load in transit");
    }

    /// <summary>
    /// Driver marks load as delivered
    /// </summary>
    public async Task<(bool success, string message)> DeliverLoadAsync(Guid loadId, string driverId)
    {
        var load = await _context.Loads.FirstOrDefaultAsync(l => l.Id == loadId);

        if (load == null)
            return (false, "Load not found");

        if (load.AssignedDriverId != driverId)
            return (false, "You are not assigned to this load");

        if (load.Status != "InTransit" && load.Status != "PickedUp")
            return (false, $"Load must be in transit before delivery (current status: {load.Status})");

        load.Status = "Delivered";
        load.DeliveredAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        // Get driver name and send notification
        var driver = await _context.Users.FindAsync(driverId);
        var driverName = driver?.UserName ?? "Driver";
        await _notificationService.NotifyLoadDeliveredAsync(load, driverName);

        _logger.LogInformation("Driver {DriverId} delivered load {LoadId}", driverId, loadId);
        return (true, "Load marked as delivered");
    }

    /// <summary>
    /// Customer confirms completion
    /// </summary>
    public async Task<(bool success, string message)> CompleteLoadAsync(Guid loadId, string customerId)
    {
        var load = await _context.Loads.FirstOrDefaultAsync(l => l.Id == loadId);

        if (load == null)
            return (false, "Load not found");

        if (load.CustomerId != customerId)
            return (false, "You are not the owner of this load");

        if (load.Status != "Delivered")
            return (false, $"Load must be delivered before completion (current status: {load.Status})");

        load.Status = "Completed";
        load.CompletedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        // Get customer name and send notification
        var customer = await _context.Users.FindAsync(customerId);
        var customerName = customer?.UserName ?? "Customer";
        await _notificationService.NotifyLoadCompletedAsync(load, customerName);

        _logger.LogInformation("Customer {CustomerId} completed load {LoadId}", customerId, loadId);
        return (true, "Load marked as completed");
    }

    /// <summary>
    /// Cancel a load
    /// </summary>
    public async Task<(bool success, string message)> CancelLoadAsync(Guid loadId, string userId)
    {
        var load = await _context.Loads.FirstOrDefaultAsync(l => l.Id == loadId);

        if (load == null)
            return (false, "Load not found");

        // Only customer or assigned driver can cancel
        if (load.CustomerId != userId && load.AssignedDriverId != userId)
            return (false, "You do not have permission to cancel this load");

        if (load.Status == "Completed")
            return (false, "Cannot cancel a completed load");

        load.Status = "Cancelled";

        await _context.SaveChangesAsync();

        // Send cancellation notification
        var reason = "User requested cancellation";
        await _notificationService.NotifyLoadCancelledAsync(load, userId, reason);

        _logger.LogInformation("User {UserId} cancelled load {LoadId}", userId, loadId);
        return (true, "Load cancelled");
    }

    /// <summary>
    /// Update ETA for a load in transit
    /// </summary>
    public async Task<(bool success, string message)> UpdateETAAsync(Guid loadId, string driverId, int estimatedMinutes)
    {
        var load = await _context.Loads.FirstOrDefaultAsync(l => l.Id == loadId);

        if (load == null)
            return (false, "Load not found");

        if (load.AssignedDriverId != driverId)
            return (false, "You are not assigned to this load");

        load.EstimatedTimeOfArrivalMinutes = estimatedMinutes;

        await _context.SaveChangesAsync();

        // Send ETA update notification
        await _notificationService.NotifyETAUpdateAsync(loadId, load.CustomerId, estimatedMinutes);

        return (true, $"ETA updated to {estimatedMinutes} minutes");
    }

    /// <summary>
    /// Get load history for a customer
    /// </summary>
    public async Task<List<Load>> GetCustomerLoadHistoryAsync(string customerId)
    {
        return await _context.Loads
            .Include(l => l.AssignedDriver)
            .Where(l => l.CustomerId == customerId && (l.Status == "Completed" || l.Status == "Cancelled"))
            .OrderByDescending(l => l.CompletedAt ?? l.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get load history for a driver
    /// </summary>
    public async Task<List<Load>> GetDriverLoadHistoryAsync(string driverId)
    {
        return await _context.Loads
            .Include(l => l.Customer)
            .Where(l => l.AssignedDriverId == driverId && (l.Status == "Completed" || l.Status == "Cancelled"))
            .OrderByDescending(l => l.CompletedAt ?? l.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get active loads for a driver (Accepted, PickedUp, InTransit, Delivered)
    /// </summary>
    public async Task<List<Load>> GetDriverActiveLoadsAsync(string driverId)
    {
        return await _context.Loads
            .Include(l => l.Customer)
            .Where(l => l.AssignedDriverId == driverId &&
                        (l.Status == "Accepted" || l.Status == "PickedUp" ||
                         l.Status == "InTransit" || l.Status == "Delivered"))
            .OrderBy(l => l.AcceptedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get active loads for a customer (Accepted, PickedUp, InTransit, Delivered)
    /// </summary>
    public async Task<List<Load>> GetCustomerActiveLoadsAsync(string customerId)
    {
        return await _context.Loads
            .Include(l => l.AssignedDriver)
            .Where(l => l.CustomerId == customerId &&
                        (l.Status == "Accepted" || l.Status == "PickedUp" ||
                         l.Status == "InTransit" || l.Status == "Delivered"))
            .OrderBy(l => l.AcceptedAt)
            .ToListAsync();
    }
}
