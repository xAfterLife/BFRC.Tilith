using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

public static class LevelUpBannerGenerator
{
    private const int Width = 1400;
    private const int Height = 320;

    private const int SkewPoint = (int)(Width * 0.5f);

    private static readonly HttpClient Http = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 4
        }
    );

    public static async ValueTask<byte[]> GenerateAsync(string displayName,
        string? userAvatarUrl,
        int rank,
        int level,
        long currentXp,
        long nextLevelXp,
        string? wallpaperUrl,
        Color elementColor,
        CancellationToken ct = default)
    {
        using var image = new Image<Rgba32>(Width, Height);

        // 1. Draw gradient background
        DrawSkewedGradient(image, elementColor);

        // 2. Load wallpaper if any
        if ( !string.IsNullOrWhiteSpace(wallpaperUrl) )
        {
            await TryDrawWallpaperAsync(image, wallpaperUrl, ct).ConfigureAwait(false);
        }

        // 3. Draw user info text
        if ( userAvatarUrl != null )
            await TryDrawAvatarAsync(image, userAvatarUrl, ct).ConfigureAwait(false);

        var fontMain = FontCache.Get(48, FontStyle.Bold);
        var fontSmall = FontCache.Get(32);

        image.Mutate(ctx =>
            {
                ctx.DrawText(
                    new RichTextOptions(fontMain)
                    {
                        Origin = new PointF(180, 48),
                        HorizontalAlignment = HorizontalAlignment.Left
                    },
                    displayName,
                    Color.White
                );

                ctx.DrawText(
                    new RichTextOptions(fontSmall)
                    {
                        Origin = new PointF(180, 118)
                    },
                    $"Rank: #{rank} • Level {level}",
                    Color.LightGray
                );
            }
        );

        // 4. XP progress bar
        DrawProgressBar(image, currentXp, nextLevelXp);

        // 5. Encode result (pooled buffer)
        using var ms = new MemoryStream(32_000);
        await image.SaveAsPngAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    private static async ValueTask TryDrawAvatarAsync(Image<Rgba32> image, string url, CancellationToken ct)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            image.Mutate(ctx =>
                {
                    var avatar = Image.Load<Rgba32>(bytes);
                    var circleAvatar = CropToCircle(avatar);
                    ctx.DrawImage(circleAvatar, new Point(40, 40), 1f);
                }
            );
        }
        catch
        {
            // Skip silently on failure
        }
    }

    /// <summary>
    ///     Crops the image to a perfect circle with antialiased edges.
    /// </summary>
    private static Image<Rgba32> CropToCircle(Image<Rgba32> src)
    {
        var size = Math.Min(src.Width, src.Height);
        var dest = new Image<Rgba32>(size, size);

        dest.Mutate(ctx =>
            {
                ctx.Fill(Color.Transparent);
                ctx.SetGraphicsOptions(new GraphicsOptions
                    {
                        Antialias = true,
                        AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver
                    }
                );

                // Apply circular clipping and draw the source image inside it
                var mask = new EllipsePolygon(size / 2f, size / 2f, size / 2f);
                ctx.Clip(mask, clipCtx =>
                    {
                        // Center the source if not perfectly square
                        var offsetX = (size - src.Width) / 2;
                        var offsetY = (size - src.Height) / 2;
                        clipCtx.DrawImage(src, new Point(offsetX, offsetY), 1f);
                    }
                );
            }
        );

        return dest;
    }

    private static async ValueTask TryDrawWallpaperAsync(Image<Rgba32> image, string url, CancellationToken ct)
    {
        try
        {
            const int scaledHeight = (int)(Height * 0.8);
            var bytes = await Http.GetByteArrayAsync(url, ct).ConfigureAwait(false);

            image.Mutate(ctx =>
                {
                    using var wallpaper = Image.Load<Rgba32>(bytes);
                    var scale = (float)Height / wallpaper.Height;
                    var scaledWidth = (int)(wallpaper.Width * scale * 0.8);

                    wallpaper.Mutate(x => x.Resize(scaledWidth, scaledHeight));
                    ctx.DrawImage(wallpaper, new Point(Width - scaledWidth - 48, Height - scaledHeight - 32), 1f);
                }
            );
        }
        catch
        {
            // Skip wallpaper silently on failure
        }
    }

    private static void DrawSkewedGradient(Image<Rgba32> image, Rgba32 elementColor)
    {
        var grey = new Rgba32(0x32, 0x32, 0x32, 0xFF);
        var element = new Rgba32(elementColor.R, elementColor.G, elementColor.B, 0xFF);

        Span<Rgba32> gradient = stackalloc Rgba32[Width];
        for ( var x = 0; x < Width; x++ )
        {
            if ( x < SkewPoint )
            {
                gradient[x] = grey;
            }
            else
            {
                var t = (x - SkewPoint) / (float)(Width - SkewPoint);
                gradient[x] = new Rgba32(
                    (byte)(grey.R + t * (element.R - grey.R)),
                    (byte)(grey.G + t * (element.G - grey.G)),
                    (byte)(grey.B + t * (element.B - grey.B)),
                    0xFF
                );
            }
        }

        for ( var y = 0; y < Height; y++ )
        {
            gradient.CopyTo(image.DangerousGetPixelRowMemory(y).Span);
        }
    }

    private static void DrawProgressBar(Image<Rgba32> image, long current, long max)
    {
        const int barHeight = 24;
        const int margin = 48;
        const int barWidth = Width / 2 - 2 * margin;
        const int barY = Height - margin - barHeight;

        image.Mutate(ctx =>
            {
                // background
                ctx.Fill(Color.WhiteSmoke,
                    new RectangleF(margin, barY, barWidth, barHeight)
                );

                // fill
                var pct = Math.Clamp((float)current / max, 0f, 1f);
                var fillWidth = (int)(barWidth * pct);
                if ( fillWidth > 0 )
                    ctx.Fill(Color.Aquamarine,
                        new RectangleF(margin, barY, fillWidth, barHeight)
                    );

                // XP label
                var font = FontCache.Get(16);
                var text = $"{current:N0} / {max:N0} XP";
                ctx.DrawText(
                    new RichTextOptions(font)
                    {
                        Origin = new PointF(margin + barWidth / 2f, barY - 25),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    text,
                    Color.White
                );
            }
        );
    }
}