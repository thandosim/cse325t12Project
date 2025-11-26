using Microsoft.EntityFrameworkCore;
using t12Project.Data;
using t12Project.Models;

namespace t12Project.Services;

public class MapDataService(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    public async Task<LocationUpdate?> GetLatestLocationAsync(string userId)
    {
        return await _context.LocationUpdates
            .Where(l => l.ApplicationUserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<MapMarker>> GetDriverMarkersAsync()
    {
        var drivers = await _context.Users
            .Where(u => u.AccountType == AccountRole.Driver)
            .Join(_context.LocationUpdates,
                  u => u.Id,
                  l => l.ApplicationUserId,
                  (u, l) => new MapMarker((double)l.Latitude, (double)l.Longitude, $"🚚 {u.FullName}", "/images/truck.png"))
            .ToListAsync();

        return drivers;
    }

    public async Task<IEnumerable<MapMarker>> GetCustomerMarkersAsync()
    {
        var customers = await _context.Users
            .Where(u => u.AccountType == AccountRole.Customer)
            .Join(_context.LocationUpdates,
                  u => u.Id,
                  l => l.ApplicationUserId,
                  (u, l) => new MapMarker((double)l.Latitude, (double)l.Longitude, $"👤 {u.FullName}", "/images/customer.png"))
            .ToListAsync();

        return customers;
    }

    public async Task<IEnumerable<MapMarker>> GetLoadMarkersAsync()
    {
        var loads = await _context.Loads
            .Select(l => new MapMarker((double)l.PickupLatitude, (double)l.PickupLongitude, $"📦 {l.Title}", "/images/box.png"))
            .ToListAsync();

        return loads;
    }

    // -------------------------------
    // Added methods for dashboards
    // -------------------------------

    public async Task<IEnumerable<Booking>> GetActiveBookingsAsync(string driverId)
    {
        return await _context.Bookings
            .Where(b => b.DriverId == driverId && b.Status == BookingStatus.Active)
            .ToListAsync();
    }

    public async Task<IEnumerable<Truck>> GetMyTrucksAsync(string driverId)
    {
        return await _context.Trucks
            .Where(t => t.DriverId == driverId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Load>> GetAvailableLoadsAsync()
    {
        return await _context.Loads
            .Where(l => l.Status == LoadStatus.Available)
            .ToListAsync();
    }

    public async Task<IEnumerable<Load>> GetLoadsByCustomerAsync(string customerId)
    {
        return await _context.Loads
            .Where(l => l.CustomerId == customerId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Truck>> GetNearbyTrucksAsync(double latitude, double longitude, double radiusKm = 10)
    {
        var trucks = await _context.Trucks.ToListAsync();

        // Haversine formula for distance in km
        return trucks.Where(t =>
        {
            if (t.Latitude == null || t.Longitude == null) return false;

            const double R = 6371; // Earth radius in km
            double dLat = (t.Latitude.Value - latitude) * Math.PI / 180;
            double dLon = (t.Longitude.Value - longitude) * Math.PI / 180;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(latitude * Math.PI / 180) * Math.Cos(t.Latitude.Value * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distance = R * c;

            return distance <= radiusKm;
        });
    }

}