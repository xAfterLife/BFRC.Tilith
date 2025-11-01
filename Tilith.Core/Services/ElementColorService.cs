using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Tilith.Core.Services;

/// <summary>
///     Resolves element-specific colors from Discord role assignments.
///     Configured via ElementRoles:Roles section in appsettings.json.
/// </summary>
public sealed class ElementColorService
{
    private static readonly Color DefaultImageSharpColor = Color.WhiteSmoke;
    private static readonly Discord.Color DefaultDiscordColor = new(168, 32, 168);

    private readonly ImmutableDictionary<ulong, Color> _elementColors;

    public ElementColorService(IConfiguration config, ILogger<ElementColorService> logger)
    {
        var tempRoles = config.GetSection("ElementRoles:Roles").Get<Dictionary<string, string>>() ?? throw new InvalidOperationException("Missing ElementRoles:Roles configuration");

        var builder = ImmutableDictionary.CreateBuilder<ulong, Color>();
        foreach ( var kvp in tempRoles.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value)) )
        {
            if ( ulong.TryParse(kvp.Key, out var roleId) )
            {
                try
                {
                    builder[roleId] = Color.Parse($"#{kvp.Value}");
                    logger.LogDebug("Mapped role {RoleId} to color #{Color}", roleId, kvp.Value);
                }
                catch ( ArgumentException ex )
                {
                    logger.LogWarning(ex, "Invalid hex color '{Color}' for role {RoleId}; skipping", kvp.Value, roleId);
                }
            }
            else
            {
                logger.LogWarning("Invalid role ID '{RoleId}'; skipping", kvp.Key);
            }
        }

        _elementColors = builder.ToImmutable();
        logger.LogInformation("Loaded {Count} element role colors", _elementColors.Count);
    }

    /// <summary>
    ///     Gets ImageSharp color for banner generation from user's highest-priority element role.
    /// </summary>
    public Color GetImageSharpColor(IEnumerable<ulong>? roleIds)
    {
        if ( roleIds == null )
            return DefaultImageSharpColor;

        foreach ( var roleId in roleIds )
        {
            if ( _elementColors.TryGetValue(roleId, out var color) )
                return color;
        }

        return DefaultImageSharpColor;
    }

    /// <summary>
    ///     Gets Discord.NET color for embeds from user's highest-priority element role.
    /// </summary>
    public Discord.Color GetDiscordColor(IEnumerable<ulong>? roleIds)
    {
        if ( roleIds == null )
            return DefaultDiscordColor;

        foreach ( var roleId in roleIds )
        {
            if ( !_elementColors.TryGetValue(roleId, out var color) )
                continue;

            // Convert ImageSharp Color → Discord.NET Color
            var rgba = color.ToPixel<Rgba32>();
            return new Discord.Color(rgba.R, rgba.G, rgba.B);
        }

        return DefaultDiscordColor;
    }
}