using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HoverPeek.Core.Imaging;
using HoverPeek.Core.Preview;
using SkiaSharp;

// 比較舊版與新版圖片預覽的耗時。每項跑 5 次取中位數（第一次先暖機）。
// 舊版流程原樣內嵌在 LegacyDecode，作為對照基準。

const int MaxLogical = 800;
const int Runs = 5;
var dpiScales = new[] { 1.0, 1.5 };

var files = args.Where(File.Exists).ToArray();
if (files.Length == 0)
{
    Console.Error.WriteLine("用法：dotnet run -c Release --project tools/HoverPeek.Bench -- <圖片路徑...>");
    return 1;
}

using var shell = new ShellThumbnailProvider();
var provider = new ImagePreviewProvider(shell);

Console.WriteLine($"{"檔案",-30} {"KB",6} {"縮放",4} | {"舊版",8} {"新版",8} {"加速",6} | {"縮圖快取",8}");
foreach (var f in files)
{
    foreach (var dpi in dpiScales)
    {
        var request = new PreviewRequest
        {
            Path = f,
            MaxImagePixels = ImageMath.ToPhysicalPixels(MaxLogical, dpi),
            DpiScale = dpi,
        };

        var legacy = Median(() => LegacyDecode(f));
        var current = Median(() => ToWpf(provider.GeneratePreviewAsync(request).GetAwaiter().GetResult()));
        var thumb = Median(() => shell.GetAsync(f, request.MaxImagePixels, dpi, cacheOnly: true).GetAwaiter().GetResult());

        var name = Path.GetFileName(f);
        if (name.Length > 28)
            name = name[..28];
        Console.WriteLine(
            $"{name,-30} {new FileInfo(f).Length / 1024,6} {dpi,4:0.0} | {legacy,6:0}ms {current,6:0}ms {legacy / current,5:0.0}x | {thumb,6:0}ms");
    }
}
return 0;

double Median(Action act)
{
    act(); // 暖機：JIT、檔案快取
    var samples = new List<double>();
    for (var i = 0; i < Runs; i++)
    {
        var sw = Stopwatch.StartNew();
        act();
        samples.Add(sw.Elapsed.TotalMilliseconds);
    }
    samples.Sort();
    return samples[samples.Count / 2];
}

// 新版：解碼結果直接建 BitmapSource（與 PreviewWindow.ToBitmapSource 相同）
static void ToWpf(PreviewResult r)
{
    var img = r.Image ?? throw new InvalidOperationException($"沒有解碼結果：{r.Kind} {r.ErrorMessage}");
    var bmp = BitmapSource.Create(img.Width, img.Height, img.DpiX, img.DpiY, PixelFormats.Pbgra32, null, img.Pixels, img.Stride);
    bmp.Freeze();
}

// 舊版（2026-11 重構前）：整檔讀入 → 全尺寸解碼 → High 品質縮放 → 編碼 PNG → WPF 再解 PNG
static void LegacyDecode(string path)
{
    var bytes = File.ReadAllBytes(path);
    using var codec = SKCodec.Create(new MemoryStream(bytes));
    using var original = SKBitmap.Decode(codec);
    var scale = Math.Min(1.0, Math.Min((double)MaxLogical / original.Width, (double)MaxLogical / original.Height));
    var w = (int)(original.Width * scale);
    var h = (int)(original.Height * scale);
    using var resized = scale < 1.0 ? original.Resize(new SKImageInfo(w, h), SKFilterQuality.High) : original.Copy();
    using var image = SKImage.FromBitmap(resized);
    using var png = image.Encode(SKEncodedImageFormat.Png, 90);

    var bmp = new BitmapImage();
    bmp.BeginInit();
    bmp.CacheOption = BitmapCacheOption.OnLoad;
    bmp.StreamSource = new MemoryStream(png.ToArray());
    bmp.EndInit();
    bmp.Freeze();
}
