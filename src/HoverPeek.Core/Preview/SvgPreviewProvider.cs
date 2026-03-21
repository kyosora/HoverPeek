using HoverPeek.Core.Localization;
using SkiaSharp;
using Svg.Skia;
using System.Text;

namespace HoverPeek.Core.Preview;

public sealed class SvgPreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> SupportedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".svg"
    };

    private int _maxPreviewDimension;

    public SvgPreviewProvider(int maxPreviewDimension = 800)
    {
        _maxPreviewDimension = maxPreviewDimension;
    }

    public void UpdateSettings(int maxDimension)
    {
        _maxPreviewDimension = maxDimension;
    }

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public Task<PreviewResult> GeneratePreviewAsync(
        string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(Strings.Format("FileNotFound", filePath));
                }

                var svgContent = File.ReadAllText(filePath);

                if (string.IsNullOrWhiteSpace(svgContent))
                {
                    throw new InvalidOperationException(Strings.Format("SvgFileEmpty", filePath));
                }

                return GenerateFromSvgContent(svgContent);
            }
            catch (Exception ex)
            {
                throw new Exception(Strings.Format("GeneratePreviewFailed", filePath), ex);
            }
        }, ct);
    }

    private PreviewResult GenerateFromSvgContent(string svgContent)
    {
        try
        {
            using var svg = new SKSvg();

            // 從字串載入 SVG
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));
            var picture = svg.Load(stream);

            if (picture == null)
            {
                throw new InvalidOperationException(Strings.CannotLoadSvg);
            }

            var bounds = picture.CullRect;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                throw new InvalidOperationException(
                    Strings.Format("SvgInvalidSize", bounds.Width, bounds.Height));
            }

            // 計算縮放比例
            var scale = CalculateScale((int)bounds.Width, (int)bounds.Height);
            var scaledWidth = (int)(bounds.Width * scale);
            var scaledHeight = (int)(bounds.Height * scale);

            // 建立點陣圖
            var info = new SKImageInfo(scaledWidth, scaledHeight);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            // 白色背景
            canvas.Clear(SKColors.White);

            // 縮放並繪製 SVG
            canvas.Scale((float)scale);
            canvas.DrawPicture(picture);
            canvas.Flush();

            // 轉換為 PNG
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);

            return new PreviewResult
            {
                Kind = PreviewKind.Image,
                ImageData = data.ToArray(),
                ImageWidth = scaledWidth,
                ImageHeight = scaledHeight,
                ImageFormat = "Svg"
            };
        }
        catch (Exception ex)
        {
            throw new Exception(Strings.GenerateSvgFailed, ex);
        }
    }

    private double CalculateScale(int width, int height)
    {
        if (width <= _maxPreviewDimension &&
            height <= _maxPreviewDimension)
            return 1.0;

        var scaleX = (double)_maxPreviewDimension / width;
        var scaleY = (double)_maxPreviewDimension / height;
        return Math.Min(scaleX, scaleY);
    }
}
