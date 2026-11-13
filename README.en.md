# HoverPeek

> 🖱️ A lightweight Windows file preview tool — hover over files in Explorer to instantly preview their contents.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)

**[繁體中文](README.md)** | **English** | **[Русский](README.ru.md)**

## ✨ Features

### 📸 Multi-Format Preview

- **Images**
  - Formats: JPG, PNG, GIF (animated), WebP, SVG, AVIF, BMP, TIFF, ICO
  - Auto-scaling with aspect ratio preserved
  - High-quality rendering via SkiaSharp

- **Folders** (New)
  - Browse folder contents (name, size, modified date)
  - Double-click files to preview directly
  - Back navigation support

- **Archives**
  - Formats: ZIP, RAR, 7Z, TAR, GZ, BZ2, XZ, LZMA
  - Browse file listings inside archives
  - Double-click files to preview directly (no extraction needed)
  - Built-in image cache for faster re-browsing

- **Videos**
  - Formats: MP4, AVI, MKV, MOV, WMV, and more
  - Powered by VLC engine with hardware decoding
  - Configurable auto-play and mute

- **Text Files**
  - **30+ file formats** supported
  - **Markdown rendering**: `.md` files displayed as formatted HTML (GitHub Flavored Markdown)
  - **Syntax highlighting**: Auto-colored code for C#, Python, JavaScript, TypeScript, Java, Go, Rust, C/C++, Ruby, PHP, HTML, CSS, SQL, PowerShell, Bash, and more
  - **Smart encoding detection**: BOM → strict UTF-8 → Big5 → system ANSI, so Big5 files no longer show as garbled text
  - Files over the size or line limit load only their beginning and are marked as truncated

### ⚙️ Full Settings System

- **Hover Behavior** — delay (default 400ms), jitter tolerance, auto-close delay (default 400ms)
- **Preview Window** — maximum size (image previews shrink to the picture), position (beside the pointer by default, or screen center), fade-in
- **Image** — max preview size (sharp on high-DPI screens), GIF animation, quick Windows-thumbnail placeholder
- **Text** — max file size, max lines, font size/family, Markdown and syntax highlighting toggles
- **Video** — auto-play, mute
- **Archives** — auto-expand file list
- **About** — version info, author, GitHub link
- **Startup** — launch on Windows startup (via registry)

### 🎯 Highlights

- ✅ **Lightweight** — runs in the background, lives in the system tray (previews can be paused from the tray)
- ✅ **Fast** — decoding starts as soon as the pointer settles, so the preview is usually ready when the hover delay ends; large JPEGs decode 3–13× faster than v1.2
- ✅ **Multilingual** — 繁體中文, 简体中文, English, 日本語, 한국어, Русский; picked from the Windows display language on first launch
- ✅ **Follows the system theme** — preview and settings windows switch between light and dark automatically
- ✅ **Errors don't interrupt** — problems are shown inside the preview window and logged to `%AppData%\HoverPeek\logs\`
- ✅ **Graceful Degradation** — falls back to plain text if Markdown/syntax highlighting fails
- ✅ **Non-Intrusive** — preview window never steals focus
- ✅ **Smart Positioning** — auto-adjusts to stay within screen bounds
- ✅ **Persistent Settings** — saved as JSON, preserved across restarts

## 📦 Installation

### Requirements

- **OS**: Windows 10/11 (64-bit)
- **.NET Runtime**: .NET 8.0 Desktop Runtime
- **WebView2** (optional): For Markdown rendering — usually pre-installed on Windows 10/11

### Download

Head to the [Releases](https://github.com/kyosora/HoverPeek/releases) page for the latest version.

### Build from Source

```bash
git clone https://github.com/kyosora/HoverPeek.git
cd HoverPeek

# Build in Release mode
dotnet build HoverPeek.slnx -c Release

# Run unit tests
dotnet test HoverPeek.slnx

# Output:
# src/HoverPeek.App/bin/Release/net8.0-windows/HoverPeek.exe
```

Developer tools (not part of the solution, run manually):

```bash
# Image preview benchmark: v1.2 pipeline vs current, at 100% and 150% scaling
dotnet run -c Release --project tools/HoverPeek.Bench -- <image paths...>

# Offscreen snapshots of the preview and settings windows (light + dark), nothing is shown on screen
dotnet run --project tools/HoverPeek.UiSnapshot -- <output dir> <image> <code file> <folder>

# Add UI strings (writes both zh-TW / English resx and Strings.cs)
python tools/add_strings.py strings.json

# Build another language's resx from a translation JSON (validates keys, {0} placeholders and line breaks first)
python tools/build_locale_resx.py ja translations/ja.json
```

### CI & Releases

- Every push / PR: GitHub Actions builds and runs the unit tests (`.github/workflows/ci.yml`)
- New release: bump `<Version>` in `HoverPeek.App.csproj`, write `.github/release-notes/v<version>.md`, then `git tag v<version> && git push origin v<version>`; `release.yml` tests, packages the win-x64 zip and creates the GitHub Release

## 🚀 Usage

1. **Launch** — Run `HoverPeek.exe`. It minimizes to the system tray.
2. **Preview** — Hover over a file in Explorer for ~400ms (configurable). A preview window appears automatically.
3. **Settings** — Right-click the tray icon → "Settings". Adjust and save.
4. **Quit** — Right-click the tray icon → "Exit HoverPeek".

## ⚙️ Tech Stack

| Component | Technology |
|-----------|-----------|
| Framework | WPF (.NET 8.0) |
| Image Processing | SkiaSharp |
| Archive Handling | SharpCompress |
| Video Playback | LibVLCSharp |
| Markdown Rendering | Markdig + Microsoft Edge WebView2 |
| Syntax Highlighting | AvalonEdit |
| Mouse Hook | Windows Low-Level Mouse Hook (`WH_MOUSE_LL`) |
| File Path Resolution | UI Automation API |

### Project Structure

```
HoverPeek/
├── src/
│   ├── HoverPeek.App/          # Main application (WPF)
│   │   ├── App.xaml.cs         # Entry point, orchestrates all components
│   │   ├── SettingsWindow.xaml # Settings UI
│   │   └── SettingsWindow.xaml.cs
│   ├── HoverPeek.Core/         # Core logic (UI-independent)
│   │   ├── MouseHook/          # Global mouse hook
│   │   ├── FileResolver/       # File path resolution
│   │   ├── Preview/            # Preview providers
│   │   │   ├── ImagePreviewProvider.cs
│   │   │   ├── ArchivePreviewProvider.cs
│   │   │   ├── FolderPreviewProvider.cs # Folder preview
│   │   │   ├── VideoPreviewProvider.cs
│   │   │   └── TextPreviewProvider.cs
│   │   └── Settings/           # Settings service
│   │       ├── AppSettings.cs
│   │       └── SettingsService.cs
│   └── HoverPeek.UI/           # UI components
│       └── PreviewWindow.xaml  # Preview window
└── README.md
```

### How It Works

1. **Global Mouse Hook** — Registers a low-level mouse hook via `SetWindowsHookEx`. Does not block mouse events. Overhead < 1ms.
2. **Hover Detection & Preview Pipeline** — Jitter tolerance (< 8px) prevents accidental triggers. A preview session starts 60ms after the pointer settles and resolves/decodes the file in the background. Each file is decoded once and kept in an LRU cache (24 items / 160MB, invalidated when the file changes). While decoding, the Windows thumbnail cache is shown first. Moving from one file to another swaps the content without the window flickering.
3. **File Path Resolution** — Primary: UI Automation API (supports all Explorer view modes). Fallback: Shell COM interface. Silent failure on errors.
4. **Image Decoding** — Skia decodes straight to the target size (JPEG uses DCT k/8 scaling) and hands BGRA pixels to WPF without a PNG round-trip. EXIF orientation is applied. Formats Skia can't read (AVIF, HEIC…) fall back to the installed Windows WIC codecs.
5. **Preview Window** — Never steals focus (`ShowActivated=False` + `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`). PerMonitorV2 DPI awareness keeps placement correct and images pixel-sharp across monitors. Placed beside the pointer (flipping sides, never covering it) or centered. Native Win11 rounded corners and shadow via DWM (no transparent window, so WebView2 renders). Markdown previews run with scripts and navigation disabled.

## 🛠️ Configuration

Settings are stored at: `%AppData%\HoverPeek\settings.json`

```json
{
  "SettingsVersion": 2,
  "HoverDelayMs": 400,
  "JitterTolerancePx": 8,
  "AutoCloseDelayMs": 400,
  "WindowWidth": 800,
  "WindowHeight": 600,
  "CenterWindow": false,
  "FadeInDurationMs": 60,
  "ImageMaxDimension": 800,
  "EnableGifAnimation": true,
  "ShowQuickThumbnail": true,
  "TextMaxFileSizeMB": 1,
  "TextMaxLines": 1000,
  "TextFontSize": 12,
  "TextFontFamily": "Cascadia Mono, Consolas",
  "EnableMarkdownRendering": true,
  "EnableSyntaxHighlighting": true,
  "VideoAutoPlay": true,
  "VideoMuted": true,
  "ArchiveAutoExpand": true,
  "StartWithWindows": false
}
```

Settings files from v1.2 and earlier are upgraded on first launch: untouched old defaults (500ms hover, centered window, 150ms fade…) switch to the new defaults, customised values are kept, and out-of-range values are clamped.

## 🤝 Contributing

Issues and Pull Requests are welcome!

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Roadmap

- [ ] PDF preview support
- [x] Multi-monitor improvements (v1.3: PerMonitorV2)
- [x] Markdown follows light / dark theme (v1.3)
- [ ] More syntax highlighting languages
- [x] Thumbnail caching (v1.3: LRU cache + Windows thumbnail cache)
- [x] Performance optimization (v1.3: speculative preloading, scaled decoding)

## 📄 License

Licensed under the **MIT License** — see [LICENSE](LICENSE) for details.

## 🙏 Acknowledgements

- [QTTabBar](https://github.com/indiff/qttabbar) — Inspiration
- [QuickLook](https://github.com/QL-Win/QuickLook) — Reference implementation
- [SkiaSharp](https://github.com/mono/SkiaSharp) — High-performance image processing
- [SharpCompress](https://github.com/adamhathcock/sharpcompress) — Archive parsing
- [LibVLCSharp](https://github.com/videolan/libvlcsharp) — Video playback engine
- [Markdig](https://github.com/xoofx/markdig) — Markdown parser
- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) — Code editor & syntax highlighting
- [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) — HTML rendering engine

## 📮 Contact

Questions or suggestions? Open an [Issue](https://github.com/kyosora/HoverPeek/issues).

---

Made with ❤️ by [kyosora](https://github.com/kyosora)
