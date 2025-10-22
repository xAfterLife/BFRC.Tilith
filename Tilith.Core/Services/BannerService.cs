using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tilith.Core.Data;
using Tilith.Core.Entities;

namespace Tilith.Core.Services;

public sealed class BannerService
{
    private readonly ILogger<BannerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UnitService _unitService;

    public BannerService(IServiceScopeFactory scopeFactory,
        UnitService unitService,
        ILogger<BannerService> logger)
    {
        _scopeFactory = scopeFactory;
        _unitService = unitService;
        _logger = logger;
    }

    public async ValueTask<List<Banner>> GetActiveBannersAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        var now = DateTime.UtcNow;

        return await db.Banners
                       .AsNoTracking()
                       .Include(b => b.BannerUnits)
                       .Where(b =>
                           b.IsActive &&
                           b.StartDateUtc <= now &&
                           (b.EndDateUtc == null || b.EndDateUtc > now)
                       )
                       .OrderByDescending(b => b.StartDateUtc)
                       .ToListAsync(ct);
    }

    public async ValueTask<Banner?> GetBannerByIdAsync(int id, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        return await db.Banners
                       .AsNoTracking()
                       .Include(b => b.BannerUnits)
                       .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async ValueTask<Banner> CreateBannerAsync(string name,
        string? description,
        DateTime startDate,
        DateTime? endDate,
        bool isActive,
        string? imageUrl,
        IReadOnlyList<string> unitIds,
        CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        // Validate units exist
        foreach ( var unitId in unitIds )
        {
            if ( _unitService.Units.All(u => u.UnitId != unitId) )
            {
                throw new InvalidOperationException($"Unit with ID '{unitId}' not found");
            }
        }

        var banner = new Banner
        {
            Name = name,
            Description = description,
            StartDateUtc = startDate,
            EndDateUtc = endDate,
            IsActive = isActive,
            ImageUrl = imageUrl,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Banners.Add(banner);
        await db.SaveChangesAsync(ct);

        // Add banner units
        foreach ( var unitId in unitIds )
        {
            db.BannerUnits.Add(new BannerUnit
                {
                    BannerId = banner.Id,
                    UnitId = unitId
                }
            );
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Created banner {BannerId} '{Name}' with {UnitCount} units",
            banner.Id, banner.Name, unitIds.Count
        );

        return banner;
    }

    public async ValueTask<bool> UpdateBannerAsync(int id,
        string? name,
        string? description,
        DateTime? startDate,
        DateTime? endDate,
        bool? isActive,
        string? imageUrl,
        IReadOnlyList<string>? unitIds,
        CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        var banner = await db.Banners
                             .Include(b => b.BannerUnits)
                             .FirstOrDefaultAsync(b => b.Id == id, ct);

        if ( banner is null )
            return false;

        if ( name is not null )
            banner.Name = name;
        if ( description is not null )
            banner.Description = description;
        if ( startDate.HasValue )
            banner.StartDateUtc = startDate.Value;
        if ( endDate.HasValue )
            banner.EndDateUtc = endDate;
        if ( isActive.HasValue )
            banner.IsActive = isActive.Value;
        if ( imageUrl is not null )
            banner.ImageUrl = imageUrl;

        banner.UpdatedAtUtc = DateTime.UtcNow;

        // Update units if provided
        if ( unitIds is not null )
        {
            // Validate all units exist
            foreach ( var unitId in unitIds )
            {
                if ( _unitService.Units.All(u => u.UnitId != unitId) )
                {
                    throw new InvalidOperationException($"Unit with ID '{unitId}' not found");
                }
            }

            // Clear existing and add new
            db.BannerUnits.RemoveRange(banner.BannerUnits);

            foreach ( var unitId in unitIds )
            {
                db.BannerUnits.Add(new BannerUnit
                    {
                        BannerId = banner.Id,
                        UnitId = unitId
                    }
                );
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated banner {BannerId} '{Name}'", banner.Id, banner.Name);

        return true;
    }

    public async ValueTask<bool> DeleteBannerAsync(int id, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TilithDbContext>();

        var banner = await db.Banners.FindAsync([id], ct);
        if ( banner is null )
            return false;

        db.Banners.Remove(banner);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted banner {BannerId} '{Name}'", id, banner.Name);
        return true;
    }
}