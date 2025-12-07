using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using t12Project.Data;
using t12Project.Models;

namespace t12Project.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RatingController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RatingController> _logger;

    public RatingController(ApplicationDbContext context, ILogger<RatingController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Customer rates a driver after delivery
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateRating([FromBody] CreateRatingRequest request)
    {
        var customerId = GetUserId();

        // Verify the load exists and is delivered
        var load = await _context.Loads.FindAsync(request.LoadId);
        if (load == null)
            return NotFound(new { message = "Load not found" });

        if (load.CustomerId != customerId)
            return Forbid();

        if (load.Status != "Delivered" && load.Status != "Completed")
            return BadRequest(new { message = "Can only rate after delivery" });

        if (string.IsNullOrEmpty(load.AssignedDriverId))
            return BadRequest(new { message = "No driver assigned to this load" });

        // Check if already rated
        var existingRating = await _context.Ratings
            .FirstOrDefaultAsync(r => r.LoadId == request.LoadId && r.CustomerId == customerId);

        if (existingRating != null)
            return BadRequest(new { message = "You have already rated this delivery" });

        var rating = new Rating
        {
            LoadId = request.LoadId,
            CustomerId = customerId,
            DriverId = load.AssignedDriverId,
            Stars = request.Stars,
            Comment = request.Comment
        };

        _context.Ratings.Add(rating);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer {CustomerId} rated driver {DriverId} {Stars} stars for load {LoadId}",
            customerId, load.AssignedDriverId, request.Stars, request.LoadId);

        return CreatedAtAction(nameof(GetRating), new { id = rating.Id }, rating);
    }

    /// <summary>
    /// Get a specific rating
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetRating(Guid id)
    {
        var rating = await _context.Ratings
            .Include(r => r.Customer)
            .Include(r => r.Driver)
            .Include(r => r.Load)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rating == null)
            return NotFound(new { message = "Rating not found" });

        return Ok(new
        {
            rating.Id,
            rating.LoadId,
            LoadTitle = rating.Load?.Title,
            rating.Stars,
            rating.Comment,
            CustomerName = rating.Customer?.FullName,
            DriverName = rating.Driver?.FullName,
            rating.CreatedAt
        });
    }

    /// <summary>
    /// Get all ratings for a driver
    /// </summary>
    [HttpGet("driver/{driverId}")]
    public async Task<IActionResult> GetDriverRatings(string driverId)
    {
        var ratings = await _context.Ratings
            .Include(r => r.Customer)
            .Include(r => r.Load)
            .Where(r => r.DriverId == driverId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.LoadId,
                LoadTitle = r.Load!.Title,
                r.Stars,
                r.Comment,
                CustomerName = r.Customer!.FullName,
                r.CreatedAt
            })
            .ToListAsync();

        var averageRating = ratings.Any() ? ratings.Average(r => r.Stars) : 0;

        return Ok(new
        {
            DriverId = driverId,
            AverageRating = averageRating,
            TotalRatings = ratings.Count,
            Ratings = ratings
        });
    }

    /// <summary>
    /// Get ratings given by a customer
    /// </summary>
    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetCustomerRatings(string customerId)
    {
        var ratings = await _context.Ratings
            .Include(r => r.Driver)
            .Include(r => r.Load)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.LoadId,
                LoadTitle = r.Load!.Title,
                r.Stars,
                r.Comment,
                DriverName = r.Driver!.FullName,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(ratings);
    }

    /// <summary>
    /// Get rating for a specific load
    /// </summary>
    [HttpGet("load/{loadId}")]
    public async Task<IActionResult> GetLoadRating(Guid loadId)
    {
        var rating = await _context.Ratings
            .Include(r => r.Customer)
            .Include(r => r.Driver)
            .FirstOrDefaultAsync(r => r.LoadId == loadId);

        if (rating == null)
            return NotFound(new { message = "No rating found for this load" });

        return Ok(new
        {
            rating.Id,
            rating.LoadId,
            rating.Stars,
            rating.Comment,
            CustomerName = rating.Customer?.FullName,
            DriverName = rating.Driver?.FullName,
            rating.CreatedAt
        });
    }

    /// <summary>
    /// Update a rating (within 24 hours of creation)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> UpdateRating(Guid id, [FromBody] UpdateRatingRequest request)
    {
        var customerId = GetUserId();
        var rating = await _context.Ratings.FindAsync(id);

        if (rating == null)
            return NotFound(new { message = "Rating not found" });

        if (rating.CustomerId != customerId)
            return Forbid();

        // Only allow updates within 24 hours
        if (DateTimeOffset.UtcNow - rating.CreatedAt > TimeSpan.FromHours(24))
            return BadRequest(new { message = "Can only update ratings within 24 hours of creation" });

        rating.Stars = request.Stars;
        rating.Comment = request.Comment;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer {CustomerId} updated rating {RatingId}", customerId, id);

        return Ok(rating);
    }

    /// <summary>
    /// Delete a rating (within 24 hours of creation)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> DeleteRating(Guid id)
    {
        var customerId = GetUserId();
        var rating = await _context.Ratings.FindAsync(id);

        if (rating == null)
            return NotFound(new { message = "Rating not found" });

        if (rating.CustomerId != customerId)
            return Forbid();

        // Only allow deletion within 24 hours
        if (DateTimeOffset.UtcNow - rating.CreatedAt > TimeSpan.FromHours(24))
            return BadRequest(new { message = "Can only delete ratings within 24 hours of creation" });

        _context.Ratings.Remove(rating);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer {CustomerId} deleted rating {RatingId}", customerId, id);

        return NoContent();
    }
}

// DTOs
public record CreateRatingRequest(Guid LoadId, int Stars, string? Comment);
public record UpdateRatingRequest(int Stars, string? Comment);
