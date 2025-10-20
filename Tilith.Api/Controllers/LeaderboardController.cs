using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tilith.Core.Data;
using Tilith.Core.Models;

namespace Tilith.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LeaderboardController : ControllerBase
{
    private readonly TilithDbContext _context;

    public LeaderboardController(TilithDbContext context)
    {
        _context = context;
    }

    [HttpGet("xp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetXpLeaderboardAsync([FromQuery] int limit = 10,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if ( limit is < 1 or > 100 )
            return BadRequest(new { error = "Limit must be between 1 and 100" });

        if ( offset < 0 )
            return BadRequest(new { error = "Offset must be non-negative" });

        var totalUsers = await _context.Users.CountAsync(cancellationToken);

        var users = await _context.Users
                                  .OrderByDescending(u => u.Experience)
                                  .Skip(offset)
                                  .Take(limit)
                                  .Select(u => new
                                      {
                                          u.DiscordId,
                                          u.Experience,
                                          Level = LevelCalculator.CalculateLevel(u.Experience),
                                          u.Gems
                                      }
                                  )
                                  .ToListAsync(cancellationToken);

        // Add rank calculation
        var leaderboard = users.Select((u, index) => new
            {
                rank = offset + index + 1,
                u.DiscordId,
                u.Level,
                u.Experience,
                u.Gems
            }
        );

        return Ok(new
            {
                total = totalUsers,
                limit,
                offset,
                users = leaderboard
            }
        );
    }

    [HttpGet("gems")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGemsLeaderboardAsync([FromQuery] int limit = 10,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if ( limit is < 1 or > 100 )
            return BadRequest(new { error = "Limit must be between 1 and 100" });

        if ( offset < 0 )
            return BadRequest(new { error = "Offset must be non-negative" });

        var totalUsers = await _context.Users.Where(u => u.Gems > 0).CountAsync(cancellationToken);

        var users = await _context.Users
                                  .Where(u => u.Gems > 0)
                                  .OrderByDescending(u => u.Gems)
                                  .Skip(offset)
                                  .Take(limit)
                                  .Select(u => new
                                      {
                                          u.DiscordId,
                                          u.Gems,
                                          Level = LevelCalculator.CalculateLevel(u.Experience)
                                      }
                                  )
                                  .ToListAsync(cancellationToken);

        var leaderboard = users.Select((u, index) => new
            {
                rank = offset + index + 1,
                u.DiscordId,
                u.Gems,
                u.Level
            }
        );

        return Ok(new
            {
                total = totalUsers,
                limit,
                offset,
                users = leaderboard
            }
        );
    }

    [HttpGet("user/{discordId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserStatsAsync(ulong discordId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync([discordId], cancellationToken);

        if ( user is null )
            return NotFound(new { error = "User not found" });

        var (level, currentLevelXp, nextLevelXp) = LevelCalculator.GetLevelProgress(user.Experience);
        var progressInLevel = user.Experience - currentLevelXp;
        var xpNeeded = nextLevelXp - currentLevelXp;

        // Calculate ranks
        var xpRank = await _context.Users
                                   .CountAsync(u => u.Experience > user.Experience, cancellationToken) +
                     1;

        var gemRank = user.Gems > 0
            ? await _context.Users.CountAsync(u => u.Gems > user.Gems, cancellationToken) + 1
            : (int?)null;

        return Ok(new
            {
                user.DiscordId,
                user.Experience,
                level,
                levelProgress = new
                {
                    current = progressInLevel,
                    needed = xpNeeded,
                    percentage = (double)progressInLevel / xpNeeded * 100
                },
                user.Gems,
                ranks = new
                {
                    xp = xpRank,
                    gems = gemRank
                },
                user.CreatedAtUtc
            }
        );
    }
}