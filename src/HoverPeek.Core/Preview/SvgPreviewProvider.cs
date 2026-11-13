using HoverPeek.Core.Imaging;
using HoverPeek.Core.Localization;
using SkiaSharp;
using Svg.Skia;

namespace HoverPeek.Core.Preview;

public sealed class SvgPreviewProvider : IPreviewProvider
{
    public bool CanHandle(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".svg", StringComparison.OrdinalIgnoreCase);

    public Task<PreviewResult> GeneratePreviewAsync(PreviewRequest request, CancellationToken ct = default) =>
        Task.Run(() => Render(request), ct);

    private static PreviewResult Render(PreviewRequest request)
    {
        var path = request.Path;
        var fileSize = new FileInfo(path).Length;
        if (fileSize == 0)
            throw new InvalidOperationException(Strings.Format("SvgFileEmpty", path));

        using var svg = new SKSvg();
        using (var stream = File.OpenRead(path))
        {
            if (svg.Load(stream) == null)
                throw new InvalidOperationException(Strings.CannotLoadSvg);
        }

        var picture = svg.Picture!;
        var bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidOperationException(Strings.Format("SvgInvalidSize", bounds.Width, bounds.Height));

        // 向量圖可以任意放大：小圖示也放大到預覽框，才看得清楚
        var longSide = Math.Max(bounds.Width, bounds.Height);
        var scale = request.MaxImagePixels / longSide;
        var width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bounds.Height * scale));

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            // 白底：多數 SVG 圖示是黑色線條，透明底放在深色主題上會看不見
            canvas.Clear(SKColors.White);
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
            canvas.Flush();
        }

        var dpi = 96.0 * (request.DpiScale > 0 ? request.DpiScale : 1.0);
        var image = new DecodedImage
        {
            Width = width,
            Height = height,
            Stride = bitmap.RowBytes,
            Pixels = bitmap.GetPixelSpan().ToArray(),
            SourceWidth = (int)Math.Round(bounds.Width),
            SourceHeight = (int)Math.Round(bounds.Height),
            Format = "SVG",
            DpiX = dpi,
            DpiY = dpi,
        };

        return new PreviewResult
        {
            Kind = PreviewKind.Image,
            Image = image,
            ImageWidth = image.SourceWidth,
            ImageHeight = image.SourceHeight,
            ImageFormat = "SVG",
            SourcePath = path,
            FileSize = fileSize,
        };
    }
}
