using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tilith.Core.Data;

namespace Tilith.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly TilithDbContext _context;

    public HealthController(TilithDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check DB connectivity
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);

            if ( !canConnect )
                return StatusCode(503, new { status = "unhealthy", message = "Database unavailable" });

            // Get basic stats
            var userCount = await _context.Users.CountAsync(cancellationToken);
            var totalXp = await _context.Users.SumAsync(u => u.Experience, cancellationToken);
            var totalGems = await _context.Users.SumAsync(u => u.Gems, cancellationToken);

            return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    database = "connected",
                    stats = new
                    {
                        totalUsers = userCount,
                        totalXpEarned = totalXp,
                        totalGems
                    }
                }
            );
        }
        catch ( Exception ex )
        {
            return StatusCode(503, new
                {
                    status = "unhealthy",
                    message = ex.Message
                }
            );
        }
    }
}