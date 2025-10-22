using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tilith.Core.Data;
using Tilith.Core.Models;

namespace Tilith.Api.Controllers;

[ApiController]
[Route("/leaderboard")]
public sealed class LeaderboardPageController : ControllerBase
{
    private readonly TilithDbContext _context;

    public LeaderboardPageController(TilithDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ResponseCache(Duration = 10)] // lightweight cache
    public async Task<IActionResult> GetLeaderboardPageAsync(CancellationToken ct)
    {
        const int limit = 100;
        var users = await _context.Users
                                  .AsNoTracking()
                                  .OrderByDescending(u => u.Experience)
                                  .Take(limit)
                                  .Select(u => new { u.DisplayName, u.Experience })
                                  .ToListAsync(ct);

        var sb = new StringBuilder(8192);
        sb.Append("""
                  <!DOCTYPE html>
                  <html lang="en">
                  <head>
                  <meta charset="UTF-8">
                  <meta name="viewport" content="width=device-width,initial-scale=1">
                  <title>Tilith XP Leaderboard</title>
                  <style>
                  body{font-family:system-ui,sans-serif;background:#0b0e12;color:#eee;margin:0;padding:2rem;}
                  h1{text-align:center;margin-bottom:1rem;}
                  table{width:100%;border-collapse:collapse;background:#151a22;border-radius:8px;overflow:hidden;}
                  th,td{padding:.75rem;text-align:left;}
                  th{background:#1e2430;font-weight:600;}
                  tr:nth-child(even){background:#12161d;}
                  .rank{font-weight:700;color:#6cf;}
                  .level{color:#f8b400;}
                  .xp{color:#9ef;}
                  footer{text-align:center;margin-top:2rem;font-size:.9rem;color:#666;}
                  </style>
                  </head>
                  <body>
                  <h1>🏆 Tilith XP Leaderboard</h1>
                  <table>
                  <thead><tr><th>#</th><th>UserName</th><th>Level</th><th>XP</th></tr></thead>
                  <tbody>
                  """
        );

        var rank = 1;
        foreach ( var u in users )
        {
            var level = LevelCalculator.CalculateLevel(u.Experience);
            sb.Append("<tr>");
            sb.Append($"<td class='rank'>{rank}</td>");
            sb.Append($"<td><code>{u.DisplayName}</code></td>");
            sb.Append($"<td class='level'>{level}</td>");
            sb.Append($"<td class='xp'>{u.Experience:N0}</td>");
            sb.Append("</tr>");
            rank++;
        }

        sb.Append("""
                  </tbody></table>
                  <footer>Last updated at 
                  """
        );
        sb.Append(DateTime.UtcNow.ToString("u"));
        sb.Append(" UTC</footer></body></html>");

        return Content(sb.ToString(), "text/html; charset=utf-8");
    }
}