using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using t12Project.Data;
using t12Project.Models;
using t12Project.Services;

namespace t12Project.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoadController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly LoadLifecycleService _lifecycleService;
    private readonly LocationTrackingService _locationService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<LoadController> _logger;

    public LoadController(
        ApplicationDbContext context,
        LoadLifecycleService lifecycleService,
        LocationTrackingService locationService,
        NotificationService notificationService,
        ILogger<LoadController> logger)
    {
        _context = context;
        _lifecycleService = lifecycleService;
        _locationService = locationService;
        _notificationService = notificationService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get available loads for drivers
    /// </summary>
    [HttpGet("available")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetAvailableLoads()
    {
        var loads = await _context.Loads
            .Include(l => l.Customer)
            .Where(l => l.Status == "Available")
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new
            {
                l.Id,
                l.Title,
                l.Status,
                l.PickupLocation,
                l.DropoffLocation,
                l.PickupLatitude,
                l.PickupLongitude,
                l.DropoffLatitude,
                l.DropoffLongitude,
                l.PickupDate,
                l.WeightLbs,
                l.Description,
                l.CargoType,
                CustomerName = l.Customer!.FullName,
                l.CreatedAt
            })
            .ToListAsync();

        return Ok(loads);
    }

    /// <summary>
    /// Get driver's active loads
    /// </summary>
    [HttpGet("driver/active")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetDriverActiveLoads()
    {
        var driverId = GetUserId();
        var loads = await _lifecycleService.GetDriverActiveLoadsAsync(driverId);

        return Ok(loads.Select(l => new
        {
            l.Id,
            l.Title,
            l.Status,
            l.PickupLocation,
            l.DropoffLocation,
            l.PickupLatitude,
            l.PickupLongitude,
            l.DropoffLatitude,
            l.DropoffLongitude,
            l.PickupDate,
            l.WeightLbs,
            l.Description,
            l.CargoType,
            CustomerName = l.Customer?.FullName,
            l.AcceptedAt,
            l.PickedUpAt,
            l.EstimatedTimeOfArrivalMinutes
        }));
    }

    /// <summary>
    /// Get customer's active loads
    /// </summary>
    [HttpGet("customer/active")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetCustomerActiveLoads()
    {
        var customerId = GetUserId();
        var loads = await _lifecycleService.GetCustomerActiveLoadsAsync(customerId);

        return Ok(loads.Select(l => new
        {
            l.Id,
            l.Title,
            l.Status,
            l.PickupLocation,
            l.DropoffLocation,
            l.PickupLatitude,
            l.PickupLongitude,
            l.DropoffLatitude,
            l.DropoffLongitude,
            l.PickupDate,
            l.WeightLbs,
            l.Description,
            l.CargoType,
            DriverName = l.AssignedDriver?.FullName,
            l.AcceptedAt,
            l.PickedUpAt,
            l.DeliveredAt,
            l.EstimatedTimeOfArrivalMinutes
        }));
    }

    /// <summary>
    /// Get load details by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetLoad(Guid id)
    {
        var userId = GetUserId();
        var load = await _context.Loads
            .Include(l => l.Customer)
            .Include(l => l.AssignedDriver)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (load == null)
            return NotFound(new { message = "Load not found" });

        // Only customer, assigned driver, or admin can view
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        if (load.CustomerId != userId && load.AssignedDriverId != userId && userRole != "Admin")
            return Forbid();

        return Ok(load);
    }

    /// <summary>
    /// Customer creates a new load
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateLoad([FromBody] CreateLoadRequest request)
    {
        var customerId = GetUserId();

        var load = new Load
        {
            Title = request.Title,
            PickupLocation = request.PickupLocation,
            DropoffLocation = request.DropoffLocation,
            PickupLatitude = request.PickupLatitude,
            PickupLongitude = request.PickupLongitude,
            DropoffLatitude = request.DropoffLatitude,
            DropoffLongitude = request.DropoffLongitude,
            PickupDate = request.PickupDate,
            WeightLbs = request.WeightLbs,
            Description = request.Description,
            CargoType = request.CargoType,
            CustomerId = customerId,
            Status = "Available"
        };

        _context.Loads.Add(load);
        await _context.SaveChangesAsync();

        // Notify all drivers
        await _notificationService.NotifyDriversOfNewLoadAsync(load);

        _logger.LogInformation("Customer {CustomerId} created load {LoadId}", customerId, load.Id);

        return CreatedAtAction(nameof(GetLoad), new { id = load.Id }, load);
    }

    /// <summary>
    /// Driver accepts a load
    /// </summary>
    [HttpPost("{id}/accept")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> AcceptLoad(Guid id, [FromBody] AcceptLoadRequest request)
    {
        var driverId = GetUserId();
        var result = await _lifecycleService.AcceptLoadAsync(id, driverId, request.EstimatedMinutes);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    /// <summary>
    /// Driver notifies arrival at pickup
    /// </summary>
    [HttpPost("{id}/arrive-pickup")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> NotifyArrivalAtPickup(Guid id)
    {
        var driverId = GetUserId();
        var result = await _lifecycleService.NotifyArrivalAtPickupAsync(id, driverId);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    /// <summary>
    /// Driver marks load as picked up
    /// </summary>
    [HttpPost("{id}/pickup")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> PickupLoad(Guid id)
    {
        var driverId = GetUserId();
        var result = await _lifecycleService.PickupLoadAsync(id, driverId);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    /// <summary>
    /// Driver starts transit
    /// </summary>
    [HttpPost("{id}/start-transit")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> StartTransit(Guid id)
    {
        var driverId = GetUserId();
        var result = await _lifecycleService.StartTransitAsync(id, driverId);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    /// <summary>
    /// Driver notifies arrival at dropoff
    /// </summary>
    [HttpPost("{id}/arrive-dropoff")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> NotifyArrivalAtDropoff(Guid id)
    {
        var driverId = GetUserId();
        var result = await _lifecycleService.NotifyArrivalAtDropoffAsync(id, driverId);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    /// <summary>
    /// Driver marks load as delivered
    /// </summary>
    [HttpPost("{id}/deliver")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> DeliverLoad(Guid id)
    {
        var driverId = GetUserId();
        var result = await _lifecycleService.DeliverLoadAsync(id, driverId);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    /// <summary>
    /// Customer confirms completion
    /// </summary>
    [HttpPost("{id}/complete")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CompleteLoad(Guid id)
    {
        var customerId = GetUserId();
        var result = await _lifecycleService.CompleteLoadAsync(id, customerId);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    /// <summary>
    /// Cancel a load
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelLoad(Guid id)
    {
        var userId = GetUserId();
        var result = await _lifecycleService.CancelLoadAsync(id, userId);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    /// <summary>
    /// Update driver location for a load
    /// </summary>
    [HttpPost("{id}/location")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] LocationUpdateRequest request)
    {
        var driverId = GetUserId();

        var load = await _context.Loads.FindAsync(id);
        if (load == null)
            return NotFound(new { message = "Load not found" });

        if (load.AssignedDriverId != driverId)
            return Forbid();

        await _locationService.UpdateDriverLocationAsync(
            driverId,
            request.Latitude,
            request.Longitude,
            id);

        return Ok(new { message = "Location updated" });
    }

    /// <summary>
    /// Get location history for a load
    /// </summary>
    [HttpGet("{id}/location-history")]
    public async Task<IActionResult> GetLocationHistory(Guid id)
    {
        var userId = GetUserId();
        var load = await _context.Loads.FindAsync(id);

        if (load == null)
            return NotFound(new { message = "Load not found" });

        // Only customer or assigned driver can view
        if (load.CustomerId != userId && load.AssignedDriverId != userId)
            return Forbid();

        var locations = await _locationService.GetLoadLocationHistoryAsync(id);

        return Ok(locations.Select(l => new
        {
            l.Latitude,
            l.Longitude,
            l.ReportedAt
        }));
    }

    /// <summary>
    /// Update delivery sequence for driver's loads
    /// </summary>
    [HttpPost("driver/update-sequence")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> UpdateDeliverySequence([FromBody] UpdateSequenceRequest request)
    {
        var driverId = GetUserId();

        // Verify all loads belong to this driver
        var loads = await _context.Loads
            .Where(l => request.LoadSequences.Select(ls => ls.LoadId).Contains(l.Id))
            .ToListAsync();

        if (loads.Any(l => l.AssignedDriverId != driverId))
            return Forbid();

        // Update sequences
        foreach (var loadSeq in request.LoadSequences)
        {
            var load = loads.FirstOrDefault(l => l.Id == loadSeq.LoadId);
            if (load != null)
            {
                load.DeliverySequence = loadSeq.Sequence;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Driver {DriverId} updated delivery sequence for {Count} loads",
            driverId, request.LoadSequences.Count);

        return Ok(new { message = "Delivery sequence updated" });
    }

    /// <summary>
    /// Get driver's loads ordered by delivery sequence
    /// </summary>
    [HttpGet("driver/sequence")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetDriverLoadsBySequence()
    {
        var driverId = GetUserId();
        var loads = await _context.Loads
            .Include(l => l.Customer)
            .Where(l => l.AssignedDriverId == driverId &&
                       (l.Status == "Accepted" || l.Status == "PickedUp" || l.Status == "InTransit"))
            .OrderBy(l => l.DeliverySequence ?? int.MaxValue)
            .ThenBy(l => l.AcceptedAt)
            .Select(l => new
            {
                l.Id,
                l.Title,
                l.Status,
                l.PickupLocation,
                l.DropoffLocation,
                l.PickupLatitude,
                l.PickupLongitude,
                l.DropoffLatitude,
                l.DropoffLongitude,
                CustomerName = l.Customer!.FullName,
                l.DeliverySequence,
                l.EstimatedTimeOfArrivalMinutes
            })
            .ToListAsync();

        return Ok(loads);
    }
}

// DTOs
public record CreateLoadRequest(
    string Title,
    string PickupLocation,
    string DropoffLocation,
    decimal PickupLatitude,
    decimal PickupLongitude,
    decimal DropoffLatitude,
    decimal DropoffLongitude,
    DateTimeOffset PickupDate,
    double WeightLbs,
    string Description,
    string CargoType);

public record AcceptLoadRequest(int EstimatedMinutes = 30);
public record LocationUpdateRequest(decimal Latitude, decimal Longitude);
public record UpdateSequenceRequest(List<LoadSequence> LoadSequences);
public record LoadSequence(Guid LoadId, int Sequence);
