using SkiaSharp;
using Svg.Skia;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: LogoAssetGenerator <microLogo.svg> <outputDir>");
    return 1;
}

var microSvgPath = Path.GetFullPath(args[0]);
var outDir = Path.GetFullPath(args[1]);
Directory.CreateDirectory(outDir);

if (!File.Exists(microSvgPath))
{
    Console.Error.WriteLine($"Missing micro logo source: {microSvgPath}");
    return 1;
}

var assets = new (string Path, int Width, int Height)[]
{
    ("StoreLogo.png", 50, 50),
    ("Square44x44Logo.png", 44, 44),
    ("Square44x44Logo.targetsize-24_altform-unplated.png", 24, 24),
    ("Square44x44Logo.scale-200.png", 88, 88),
    ("LockScreenLogo.scale-200.png", 48, 48),
    ("Square150x150Logo.scale-200.png", 300, 300),
    ("Wide310x150Logo.scale-200.png", 620, 300),
    ("SplashScreen.scale-200.png", 620, 300),
};

await RenderAssets(outDir, microSvgPath, assets);

CopyIfExists(outDir, "Square150x150Logo.scale-200.png", "Square150x150Logo.png");
CopyIfExists(outDir, "Wide310x150Logo.scale-200.png", "Wide310x150Logo.png");
CopyIfExists(outDir, "SplashScreen.scale-200.png", "SplashScreen.png");

return 0;

static async Task RenderAssets(string outDir, string svgPath, (string Path, int Width, int Height)[] assets)
{
    using var svg = new SKSvg();
    if (svg.Load(svgPath) is null || svg.Picture is null)
    {
        throw new InvalidOperationException($"Failed to load SVG: {svgPath}");
    }

    var picture = svg.Picture;
    var bounds = picture.CullRect;

    foreach (var (path, width, height) in assets)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var scale = Math.Min(width / bounds.Width, height / bounds.Height);
        var dx = ((width - (bounds.Width * scale)) / 2f) - (bounds.Left * scale);
        var dy = ((height - (bounds.Height * scale)) / 2f) - (bounds.Top * scale);

        canvas.Translate(dx, dy);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var outPath = Path.Combine(outDir, path);
        await using var stream = File.Create(outPath);
        data.SaveTo(stream);
        Console.WriteLine($"Wrote {outPath}");
    }
}

static void CopyIfExists(string sourceDir, string sourceName, string destName)
{
    var sourcePath = Path.Combine(sourceDir, sourceName);
    var destPath = Path.Combine(sourceDir, destName);
    if (File.Exists(sourcePath))
    {
        File.Copy(sourcePath, destPath, overwrite: true);
        Console.WriteLine($"Wrote {destPath}");
    }
}
