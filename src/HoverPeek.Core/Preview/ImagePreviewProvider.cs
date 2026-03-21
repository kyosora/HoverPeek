using HoverPeek.Core.Localization;
using SkiaSharp;

namespace HoverPeek.Core.Preview;

public sealed class ImagePreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> SupportedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".ico", ".tiff", ".tif", ".avif"
    };

    private int _maxPreviewDimension;

    public ImagePreviewProvider(int maxPreviewDimension = 800)
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

                var bytes = File.ReadAllBytes(filePath);

                if (bytes == null || bytes.Length == 0)
                {
                    throw new InvalidOperationException(Strings.Format("FileEmpty", filePath));
                }

                var result = GenerateFromBytes(bytes);

                if (result.Kind == PreviewKind.Unsupported)
                {
                    throw new NotSupportedException(Strings.Format("CannotDecodeImage", filePath, bytes.Length));
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception(Strings.Format("GeneratePreviewFailed", filePath), ex);
            }
        }, ct);
    }

    /// <summary>
    /// 從原始位元組產生預覽，也用於壓縮包內的圖片。
    /// 大圖會縮放到 _maxPreviewDimension 以內，節省記憶體。
    /// </summary>
    public PreviewResult GenerateFromBytes(byte[] rawBytes)
    {
        try
        {
            using var inputStream = new MemoryStream(rawBytes);
            using var codec = SKCodec.Create(inputStream);

            if (codec == null)
            {
                var header = rawBytes.Length >= 16
                    ? BitConverter.ToString(rawBytes.Take(16).ToArray())
                    : Strings.FileTooSmallHeader;
                throw new NotSupportedException(Strings.Format("SKCodecNull", rawBytes.Length, header));
            }

            var info = codec.Info;
            var format = codec.EncodedFormat;

            // GIF 特殊處理：保留原始位元組以支援動畫
            if (format == SKEncodedImageFormat.Gif)
            {
                return new PreviewResult
                {
                    Kind = PreviewKind.Image,
                    ImageData = rawBytes,  // 直接使用原始 GIF 資料
                    ImageWidth = info.Width,
                    ImageHeight = info.Height,
                    ImageFormat = "Gif"
                };
            }

            using var originalBitmap = SKBitmap.Decode(codec);

            if (originalBitmap == null)
            {
                throw new InvalidOperationException(Strings.Format("SKBitmapDecodeNull", info.Width, info.Height));
            }

            var scale = CalculateScale(originalBitmap.Width, originalBitmap.Height);
            var scaledWidth = (int)(originalBitmap.Width * scale);
            var scaledHeight = (int)(originalBitmap.Height * scale);

            SKBitmap finalBitmap;
            if (scale < 1.0)
            {
                var scaledInfo = new SKImageInfo(scaledWidth, scaledHeight);
                finalBitmap = originalBitmap.Resize(scaledInfo, SKFilterQuality.High);

                if (finalBitmap == null)
                {
                    throw new InvalidOperationException(Strings.Format("ResizeNull", originalBitmap.Width, originalBitmap.Height, scaledWidth, scaledHeight));
                }
            }
            else
            {
                finalBitmap = originalBitmap;
            }

            using var image = SKImage.FromBitmap(finalBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);

            if (finalBitmap != originalBitmap)
            {
                finalBitmap.Dispose();
            }

            return new PreviewResult
            {
                Kind = PreviewKind.Image,
                ImageData = data.ToArray(),
                ImageWidth = scaledWidth,
                ImageHeight = scaledHeight,
                ImageFormat = codec.EncodedFormat.ToString()
            };
        }
        catch (Exception ex)
        {
            throw new Exception(Strings.Format("GenerateFromBytesFailed", rawBytes?.Length ?? 0), ex);
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
