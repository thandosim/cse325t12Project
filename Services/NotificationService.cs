using Microsoft.EntityFrameworkCore;
using t12Project.Data;
using t12Project.Models;

namespace t12Project.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Send a notification to a specific user
    /// </summary>
    public async Task SendNotificationAsync(string userId, string title, string message, string type = "Info", Guid? relatedEntityId = null, string? relatedEntityType = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Notify all drivers about a new load
    /// </summary>
    public async Task NotifyDriversOfNewLoadAsync(Load load)
    {
        var drivers = await _context.Users
            .Where(u => u.AccountType == AccountRole.Driver)
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var driverId in drivers)
        {
            await SendNotificationAsync(
                driverId,
                "New Load Available",
                $"A new load '{load.Title}' is available from {load.PickupLocation} to {load.DropoffLocation}",
                "LoadPosted",
                load.Id,
                "Load"
            );
        }
    }

    /// <summary>
    /// Notify customer when a driver requests their load
    /// </summary>
    public async Task NotifyCustomerOfBookingRequestAsync(Booking booking, string driverName)
    {
        var load = await _context.Loads.FindAsync(booking.LoadId);
        if (load == null) return;

        await SendNotificationAsync(
            load.CustomerId,
            "Booking Request Received",
            $"Driver {driverName} has requested to transport your load '{load.Title}'",
            "BookingRequest",
            booking.Id,
            "Booking"
        );
    }

    /// <summary>
    /// Get unread notifications for a user
    /// </summary>
    public async Task<List<Notification>> GetUnreadNotificationsAsync(string userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get all notifications for a user
    /// </summary>
    public async Task<List<Notification>> GetNotificationsAsync(string userId, int take = 20)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>
    /// Mark a notification as read
    /// </summary>
    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Mark all notifications as read for a user
    /// </summary>
    public async Task MarkAllAsReadAsync(string userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get unread notification count
    /// </summary>
    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    /// <summary>
    /// Notify driver when their booking is confirmed by the customer
    /// </summary>
    public async Task NotifyDriverOfBookingConfirmationAsync(Booking booking, string customerName)
    {
        var load = await _context.Loads.FindAsync(booking.LoadId);
        if (load == null) return;

        await SendNotificationAsync(
            booking.DriverId,
            "Booking Confirmed!",
            $"Your booking for load '{load.Title}' from {load.PickupLocation} to {load.DropoffLocation} has been confirmed by {customerName}.",
            "BookingConfirmed",
            booking.Id,
            "Booking"
        );
    }

    /// <summary>
    /// Notify customer when a driver accepts/books their load
    /// </summary>
    public async Task NotifyCustomerOfLoadBookedAsync(Load load, string driverName)
    {
        await SendNotificationAsync(
            load.CustomerId,
            "Load Booked!",
            $"Driver {driverName} has booked your load '{load.Title}' from {load.PickupLocation} to {load.DropoffLocation}.",
            "LoadBooked",
            load.Id,
            "Load"
        );
    }
}
