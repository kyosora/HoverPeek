using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HoverPeek.App;
using HoverPeek.Core.Preview;
using HoverPeek.Core.Settings;
using HoverPeek.UI;
using HoverPeek.UI.Themes;

// 用法：dotnet run --project tools/HoverPeek.UiSnapshot -- <輸出資料夾> <圖片檔> <程式碼檔> <資料夾>
// 不呼叫 Show()：直接對視窗內容做 Measure/Arrange 再 RenderTargetBitmap，所以不會出現在螢幕上。

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("用法：<輸出資料夾> <圖片檔> <程式碼檔> <資料夾>");
            return 1;
        }

        var outDir = Directory.CreateDirectory(args[0]).FullName;
        _ = new Application(); // pack://application URI 需要 Application 已初始化

        var results = new (string Name, PreviewResult Result)[]
        {
            ("image", new ImagePreviewProvider().GeneratePreviewAsync(new PreviewRequest { Path = args[1] }).GetAwaiter().GetResult()),
            ("code", new TextPreviewProvider().GeneratePreviewAsync(new PreviewRequest { Path = args[2] }).GetAwaiter().GetResult()),
            ("folder", new FolderPreviewProvider().GeneratePreviewAsync(new PreviewRequest { Path = args[3] }).GetAwaiter().GetResult()),
            ("error", PreviewResult.FromException(@"C:\範例\壞掉的圖片.jpg",
                new InvalidOperationException("解碼失敗（JPEG，4000×3000 → 800×600）：InvalidInput"))),
        };

        foreach (var dark in new[] { false, true })
        {
            var theme = dark ? "dark" : "light";
            foreach (var (name, result) in results)
                Save(RenderPreview(result, dark), Path.Combine(outDir, $"preview-{name}-{theme}.png"));

            foreach (var section in Enumerable.Range(0, 8))
                Save(RenderSettings(section, dark), Path.Combine(outDir, $"settings-{section}-{theme}.png"));
        }

        // 其他語言：設定視窗每一頁（深色），檢查較長的翻譯會不會被截掉或跑版
        foreach (var language in HoverPeek.Core.Localization.SupportedLanguages.All)
        {
            HoverPeek.Core.Localization.LocaleManager.SetLanguage(language.Code);
            foreach (var section in Enumerable.Range(0, 8))
                Save(RenderSettings(section, dark: true), Path.Combine(outDir, $"lang-{language.Code}-{section}.png"));
        }

        Console.WriteLine($"已輸出到 {outDir}");
        return 0;
    }

    private static BitmapSource RenderPreview(PreviewResult result, bool dark)
    {
        var window = new PreviewWindow(new NullHost());
        window.ApplySettings(AppSettings.Default);
        SetTheme(window, dark);

        Invoke(window, "ResetContent");
        var content = (Size)Invoke(window, "RenderContent", result)!;
        var root = (FrameworkElement)window.Content;

        // 照 PlaceAndShow 的規則算視窗大小（DIP）
        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var header = ((FrameworkElement)window.FindName("HeaderBar")).DesiredSize.Height;
        var w = Math.Clamp(content.Width, 320, AppSettings.Default.WindowWidth);
        var h = Math.Clamp(content.Height + header, 120 + header, AppSettings.Default.WindowHeight);
        return Render(root, window.Background, w, h);
    }

    private static BitmapSource RenderSettings(int section, bool dark)
    {
        var temp = Path.Combine(Path.GetTempPath(), "HoverPeek.UiSnapshot", "settings.json");
        var window = new SettingsWindow(new SettingsService(temp));
        SetTheme(window, dark);
        ((ListBox)window.FindName("NavigationList")).SelectedIndex = section;
        return Render((FrameworkElement)window.Content, window.Background, 760, 540);
    }

    private static BitmapSource Render(FrameworkElement root, Brush background, double w, double h)
    {
        root.Measure(new Size(w, h));
        root.Arrange(new Rect(0, 0, w, h));
        root.UpdateLayout();

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(background, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, w, h));
        }

        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(w), (int)Math.Ceiling(h), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    /// <summary>強制套用指定主題（不看系統設定），呼叫 ThemeManager 的私有方法。</summary>
    private static void SetTheme(Window window, bool dark)
    {
        typeof(ThemeManager)
            .GetMethod("ApplyResources", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { window.Resources, dark });
    }

    private static object? Invoke(object target, string method, params object[] args)
    {
        // DeclaredOnly：Window 基底類別也有非公開的同名方法（例如 RenderContent）
        var info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                   ?? throw new MissingMethodException(target.GetType().Name, method);
        return info.Invoke(target, args);
    }

    private static void Save(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private sealed class NullHost : IPreviewHost
    {
        public Task<PreviewResult> PreviewPathAsync(string path, double dpiScale) =>
            Task.FromResult(PreviewResult.Unsupported(path));

        public Task<PreviewResult> PreviewArchiveEntryAsync(string archivePath, string entryPath, double dpiScale) =>
            Task.FromResult(PreviewResult.Unsupported(entryPath));
    }
}
