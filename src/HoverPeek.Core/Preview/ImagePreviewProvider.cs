using HoverPeek.Core.Imaging;
using HoverPeek.Core.Localization;
using SkiaSharp;

namespace HoverPeek.Core.Preview;

public sealed class ImagePreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jfif", ".png", ".gif", ".bmp", ".webp",
        ".ico", ".tiff", ".tif", ".avif", ".heic", ".heif", ".wbmp",
    };

    private readonly ShellThumbnailProvider? _shellFallback;

    /// <param name="shellFallback">
    /// Skia 解不了的格式（AVIF、HEIC…）改用系統 WIC codec；傳 null 則不做後備。
    /// </param>
    public ImagePreviewProvider(ShellThumbnailProvider? shellFallback = null)
    {
        _shellFallback = shellFallback;
    }

    public bool CanHandle(string filePath) => SupportedExtensions.Contains(Path.GetExtension(filePath));

    public async Task<PreviewResult> GeneratePreviewAsync(PreviewRequest request, CancellationToken ct = default)
    {
        var path = request.Path;
        var fileSize = new FileInfo(path).Length;
        if (fileSize == 0)
            throw new InvalidOperationException(Strings.Format("FileEmpty", path));

        var result = await Task.Run(() => DecodeFile(request, fileSize), ct).ConfigureAwait(false);
        if (result != null)
            return result;

        // Skia 不認得這個格式 → 請 Windows 解（需要對應的 WIC codec，例如 HEIF／AV1 擴充功能）
        if (_shellFallback != null)
        {
            var image = await _shellFallback
                .GetAsync(path, request.MaxImagePixels, request.DpiScale, cacheOnly: false, ct)
                .ConfigureAwait(false);
            if (image != null)
                return ToResult(image, Path.GetExtension(path).TrimStart('.').ToUpperInvariant(), path, fileSize);
        }

        throw new NotSupportedException(Strings.Format("CannotDecodeImage", path, fileSize));
    }

    /// <summary>壓縮檔內的圖片：已經在記憶體裡，不做系統後備（Shell 只吃檔案路徑）。</summary>
    public PreviewResult GenerateFromBytes(byte[] bytes, PreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
            throw new InvalidOperationException(Strings.Format("FileEmpty", request.Path));

        if (request.AnimateGif && SkiaImageDecoder.IsGif(bytes))
            return ToGifResult(bytes, request, bytes.Length);

        var image = SkiaImageDecoder.TryDecode(bytes, request.MaxImagePixels, request.DpiScale)
                    ?? throw new NotSupportedException(Strings.Format("CannotDecodeImage", request.Path, bytes.Length));
        return ToResult(image, image.Format ?? "?", request.Path, bytes.Length);
    }

    private static PreviewResult? DecodeFile(PreviewRequest request, long fileSize)
    {
        using var stream = new FileStream(
            request.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024, FileOptions.RandomAccess);

        Span<byte> header = stackalloc byte[6];
        var read = stream.Read(header);
        stream.Position = 0;

        if (request.AnimateGif && SkiaImageDecoder.IsGif(header[..read]))
        {
            var bytes = new byte[fileSize];
            stream.ReadExactly(bytes);
            return ToGifResult(bytes, request, fileSize);
        }

        var image = SkiaImageDecoder.TryDecode(stream, request.MaxImagePixels, request.DpiScale);
        return image == null ? null : ToResult(image, image.Format ?? "?", request.Path, fileSize);
    }

    /// <summary>
    /// GIF：同時附上第一格的靜態圖（背景解好，預覽一出現就有畫面）與原始 bytes（給 UI 串流播放動畫）。
    /// </summary>
    private static PreviewResult ToGifResult(byte[] bytes, PreviewRequest request, long fileSize)
    {
        var firstFrame = SkiaImageDecoder.TryDecode(bytes, request.MaxImagePixels, request.DpiScale);
        return new PreviewResult
        {
            Kind = PreviewKind.Image,
            Image = firstFrame,
            AnimatedGifData = bytes,
            ImageWidth = firstFrame?.SourceWidth ?? 0,
            ImageHeight = firstFrame?.SourceHeight ?? 0,
            ImageFormat = "GIF",
            SourcePath = request.Path,
            FileSize = fileSize,
        };
    }

    private static PreviewResult ToResult(DecodedImage image, string format, string path, long fileSize) => new()
    {
        Kind = PreviewKind.Image,
        Image = image,
        ImageWidth = image.SourceWidth > 0 ? image.SourceWidth : image.Width,
        ImageHeight = image.SourceHeight > 0 ? image.SourceHeight : image.Height,
        ImageFormat = format,
        SourcePath = path,
        FileSize = fileSize,
    };
}
