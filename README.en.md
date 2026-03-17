# HoverPeek

> 🖱️ A lightweight Windows file preview tool — hover over files in Explorer to instantly preview their contents.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)

**[繁體中文](README.md)** | **English**

## ✨ Features

### 📸 Multi-Format Preview

- **Images**
  - Formats: JPG, PNG, GIF (animated), WebP, SVG, AVIF, BMP, TIFF, ICO
  - Auto-scaling with aspect ratio preserved
  - High-quality rendering via SkiaSharp

- **Archives**
  - Formats: ZIP, RAR, 7Z, TAR, GZ, BZ2, XZ, LZMA
  - Browse file listings inside archives
  - Hover over images within archives for instant preview (no extraction needed)

- **Videos**
  - Formats: MP4, AVI, MKV, MOV, WMV, and more
  - Powered by VLC engine with hardware decoding
  - Configurable auto-play and mute

- **Text Files**
  - **30+ file formats** supported
  - **Markdown rendering**: `.md` files displayed as formatted HTML (GitHub Flavored Markdown)
  - **Syntax highlighting**: Auto-colored code for C#, Python, JavaScript, TypeScript, Java, Go, Rust, C/C++, Ruby, PHP, HTML, CSS, SQL, PowerShell, Bash, and more
  - **Smart encoding detection**: Auto-detects UTF-8, Big5, and system default encoding
  - Configurable file size and line count limits

### ⚙️ Full Settings System

- **Hover Behavior** — delay, jitter tolerance, auto-close delay
- **Preview Window** — size, position (center screen / follow mouse), fade animations
- **Image** — max preview size, GIF animation toggle
- **Text** — max file size, max lines, font size/family, Markdown and syntax highlighting toggles
- **Video** — auto-play, mute
- **Archives** — auto-expand file list
- **Startup** — launch on Windows startup (via registry)

### 🎯 Highlights

- ✅ **Lightweight** — runs in the background, lives in the system tray
- ✅ **Instant Preview** — no need to open any application
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
dotnet build src/HoverPeek.App/HoverPeek.App.csproj -c Release

# Output:
# src/HoverPeek.App/bin/Release/net8.0-windows/HoverPeek.App.exe
```

## 🚀 Usage

1. **Launch** — Run `HoverPeek.App.exe`. It minimizes to the system tray.
2. **Preview** — Hover over a file in Explorer for ~500ms (configurable). A preview window appears automatically.
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
2. **Hover Detection** — Jitter tolerance (< 8px) prevents accidental triggers. Cancellable via `CancellationTokenSource`. Locks preview to avoid re-triggering on mouse movement.
3. **File Path Resolution** — Primary: UI Automation API (supports all Explorer view modes). Fallback: Shell COM interface. Silent failure on errors.
4. **Preview Window** — Never steals focus (`ShowActivated=False` + `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`). Smart positioning avoids screen overflow. Configurable fade animations.

## 🛠️ Configuration

Settings are stored at: `%AppData%\HoverPeek\settings.json`

```json
{
  "HoverDelayMs": 500,
  "JitterTolerancePx": 8,
  "AutoCloseDelayMs": 600,
  "WindowWidth": 800,
  "WindowHeight": 600,
  "CenterWindow": true,
  "FadeInDurationMs": 150,
  "FadeOutDurationMs": 100,
  "ImageMaxDimension": 800,
  "EnableGifAnimation": true,
  "TextMaxFileSizeMB": 1,
  "TextMaxLines": 1000,
  "TextFontSize": 11,
  "TextFontFamily": "Consolas",
  "EnableMarkdownRendering": true,
  "EnableSyntaxHighlighting": true,
  "VideoAutoPlay": true,
  "VideoMuted": true,
  "ArchiveAutoExpand": true,
  "StartWithWindows": false
}
```

## 🤝 Contributing

Issues and Pull Requests are welcome!

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Roadmap

- [ ] PDF preview support
- [ ] Multi-monitor improvements
- [ ] Custom CSS themes for Markdown
- [ ] More syntax highlighting languages
- [ ] Thumbnail caching
- [ ] Performance monitoring & optimization

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
