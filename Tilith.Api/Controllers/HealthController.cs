using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tilith.Core.Data;
using Tilith.Core.Services;

namespace Tilith.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly TilithDbContext _context;
    private readonly LevelCacheService _levelCache;

    public HealthController(TilithDbContext context, LevelCacheService levelCache)
    {
        _context = context;
        _levelCache = levelCache;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            if ( !canConnect )
                return StatusCode(503, new { status = "unhealthy", message = "Database unavailable" });

            var userCount = await _context.Users.CountAsync(cancellationToken);
            var totalXp = await _context.Users.SumAsync(u => u.Experience, cancellationToken);
            var totalGems = await _context.Users.SumAsync(u => u.Gems, cancellationToken);
            var (cacheTotal, cacheActive) = _levelCache.GetStatistics();

            return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    database = "connected",
                    stats = new
                    {
                        totalUsers = userCount,
                        totalXpEarned = totalXp,
                        totalGems,
                        cache = new
                        {
                            totalEntries = cacheTotal,
                            activeEntries = cacheActive,
                            expiredEntries = cacheTotal - cacheActive,
                            slidingWindowMinutes = 5
                        }
                    }
                }
            );
        }
        catch ( Exception ex )
        {
            return StatusCode(503, new { status = "unhealthy", message = ex.Message });
        }
    }
}