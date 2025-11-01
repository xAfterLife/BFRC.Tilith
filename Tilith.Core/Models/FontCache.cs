using SixLabors.Fonts;

namespace Tilith.Core.Models;

public static class FontCache
{
    private static readonly FontCollection Collection = new();
    private static FontFamily _workSans;

    public static void Initialize()
    {
        var asm = typeof(FontCache).Assembly;
        const string resourceName = "Tilith.Core.Resource.Fonts.WorkSans-SemiBold.ttf";

        using var stream = asm.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException($"Embedded font resource not found: {resourceName}");

        _workSans = Collection.Add(stream);
    }

    public static Font Get(float size, FontStyle style = FontStyle.Regular)
    {
        return _workSans.CreateFont(size, style);
    }
}