using System.IO;
using System.Windows;
using System.Windows.Media;
using HoverPeek.Core.Diagnostics;
using HoverPeek.UI.Themes;
using Microsoft.Web.WebView2.Core;

namespace HoverPeek.UI;

public sealed partial class PreviewWindow
{
    // NavigateToString 上限 2MB；留點餘裕給 CSS
    private const int MaxMarkdownHtmlChars = 1_500_000;

    private static Task<CoreWebView2Environment>? _webViewEnvironment;
    private Task? _webViewReady;
    private bool _allowNextNavigation;

    private static Task<CoreWebView2Environment> GetWebViewEnvironmentAsync() =>
        _webViewEnvironment ??= CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            // 預設會建在 exe 旁邊（publish 資料夾裡那堆 EBWebView），改放使用者的 LocalAppData
            userDataFolder: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HoverPeek", "WebView2"));

    private Task EnsureWebViewAsync() => _webViewReady ??= InitWebViewAsync();

    private async Task InitWebViewAsync()
    {
        var env = await GetWebViewEnvironmentAsync();
        await MarkdownView.EnsureCoreWebView2Async(env);

        var settings = MarkdownView.CoreWebView2.Settings;
        // Markdig 會放行 Markdown 裡的原始 HTML，關掉腳本，避免預覽惡意 .md 時執行 JS
        settings.IsScriptEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;

        // 預覽視窗不該跳去別的頁面；只放行我們自己 NavigateToString 的那一次
        MarkdownView.CoreWebView2.NavigationStarting += (_, e) =>
        {
            if (_allowNextNavigation)
            {
                _allowNextNavigation = false;
                return;
            }
            e.Cancel = true;
        };
        MarkdownView.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
    }

    private async Task ShowMarkdownAsync(string bodyHtml)
    {
        var expected = _current;
        if (bodyHtml.Length > MaxMarkdownHtmlChars)
        {
            ShowMarkdownAsPlainText();
            return;
        }

        try
        {
            await EnsureWebViewAsync();
        }
        catch (Exception ex)
        {
            // 沒裝 WebView2 Runtime 等情況：退回純文字，Markdown 原文一樣能讀
            AppLog.Warn("WebView2 初始化失敗，Markdown 改用純文字顯示", ex);
            _webViewReady = null;
            ShowMarkdownAsPlainText();
            return;
        }

        if (!ReferenceEquals(_current, expected))
            return;

        if (FindResource("Hp.Document") is SolidColorBrush doc)
        {
            var c = doc.Color;
            MarkdownView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        _allowNextNavigation = true;
        MarkdownView.NavigateToString(BuildMarkdownPage(bodyHtml, ThemeManager.IsDark));
        PlainTextView.Visibility = Visibility.Collapsed;
        MarkdownView.Visibility = Visibility.Visible;
    }

    private void ShowMarkdownAsPlainText()
    {
        MarkdownView.Visibility = Visibility.Collapsed;
        PlainTextView.Text = _current?.TextContent ?? string.Empty;
        PlainTextView.Visibility = Visibility.Visible;
    }

    private static string BuildMarkdownPage(string bodyHtml, bool dark)
    {
        var (bg, fg, muted, codeBg, border, link) = dark
            ? ("#1C1C1C", "#E8E8E8", "#A0A0A0", "#2A2A2A", "#3A3A3A", "#60CDFF")
            : ("#FFFFFF", "#1B1B1B", "#5D5D5D", "#F3F3F3", "#E2E2E2", "#005FB8");

        return $$"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data: file:; style-src 'unsafe-inline'">
            <style>
              :root { color-scheme: {{(dark ? "dark" : "light")}}; }
              body { margin: 0; padding: 16px 20px; background: {{bg}}; color: {{fg}};
                     font: 14px/1.65 "Segoe UI Variable Text", "Segoe UI", "Microsoft JhengHei UI", sans-serif; }
              h1, h2 { border-bottom: 1px solid {{border}}; padding-bottom: .3em; }
              h1 { font-size: 1.6em; } h2 { font-size: 1.3em; } h3 { font-size: 1.1em; }
              a { color: {{link}}; }
              code { background: {{codeBg}}; padding: 1px 5px; border-radius: 4px;
                     font: 12.5px "Cascadia Mono", Consolas, monospace; }
              pre { background: {{codeBg}}; padding: 12px; border-radius: 6px; overflow-x: auto; }
              pre code { padding: 0; background: none; }
              blockquote { margin: 0; padding: 0 1em; color: {{muted}}; border-left: 3px solid {{border}}; }
              table { border-collapse: collapse; } th, td { border: 1px solid {{border}}; padding: 6px 10px; }
              th { background: {{codeBg}}; } img { max-width: 100%; } hr { border: 0; border-top: 1px solid {{border}}; }
            </style></head>
            <body>{{bodyHtml}}</body></html>
            """;
    }
}
