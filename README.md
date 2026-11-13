# HoverPeek

> 🖱️ 一個輕量級的 Windows 檔案預覽工具，讓你在檔案總管中懸停滑鼠即可快速預覽檔案內容。

**繁體中文** | **[English](README.en.md)**

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)

## ✨ 功能特色

### 📸 支援多種檔案格式

- **圖片預覽**
  - 支援格式：JPG、PNG、GIF（含動畫）、WebP、SVG、AVIF、BMP、TIFF、ICO
  - 自動縮放，保持圖片比例
  - 高品質渲染（SkiaSharp）

- **資料夾預覽**（🆕 新功能）
  - 瀏覽資料夾內的檔案列表（檔名、大小、修改日期）
  - 雙擊檔案可直接預覽內容
  - 支援返回上層導覽

- **壓縮檔預覽**
  - 支援格式：ZIP、RAR、7Z、TAR、GZ、BZ2、XZ、LZMA
  - 瀏覽壓縮檔內的檔案列表
  - 雙擊檔案可直接預覽內容（無需解壓）
  - 內建圖片快取，提升重複瀏覽效能

- **影片預覽**
  - 支援格式：MP4、AVI、MKV、MOV、WMV 等
  - 使用 VLC 引擎，支援硬體解碼
  - 自動播放/靜音可設定

- **文字檔預覽**（🆕 新功能）
  - 支援 **30+ 種檔案格式**
  - **Markdown 渲染**：.md 檔案顯示為格式化的 HTML（支援 GitHub Flavored Markdown）
  - **語法高亮**：程式碼檔案自動套用顏色標記
    - 支援語言：C#、Python、JavaScript、TypeScript、Java、Go、Rust、C/C++、Ruby、PHP、HTML、CSS、SQL、PowerShell、Bash 等
  - **智慧編碼偵測**：BOM → 嚴格 UTF-8 → Big5 → 系統 ANSI，Big5 檔案不再顯示成亂碼
  - 超過大小或行數上限時只載入開頭，並標示「已截斷」

### ⚙️ 完整的設定系統（🆕 新功能）

- **懸停行為**
  - 懸停延遲（預設 400ms）
  - 抖動容忍度（預設 8px）
  - 自動關閉延遲（預設 400ms）

- **預覽視窗**
  - 視窗最大寬度／高度（圖片預覽會依圖片比例縮小）
  - 視窗位置（預設顯示在游標旁，可改為固定螢幕中央）
  - 淡入動畫時間（預設 60ms，0 = 立即顯示）

- **圖片設定**
  - 最大預覽尺寸（預設 800px，高 DPI 螢幕自動解出對應像素）
  - GIF 動畫開關
  - 先顯示 Windows 縮圖快取（清晰版解碼完成後替換）

- **文字設定**
  - 最大檔案大小（預設 1MB）
  - 最大顯示行數（預設 1000 行）
  - 字型大小/家族
  - Markdown 渲染開關
  - 語法高亮開關

- **影片設定**
  - 自動播放
  - 靜音播放

- **壓縮檔設定**
  - 自動展開檔案列表

- **關於**（🆕 新功能）
  - 版本資訊、作者、GitHub 連結

- **啟動設定**
  - 開機自動啟動（寫入 Windows 註冊表）

### 🎯 核心優勢

- ✅ **輕量化**：背景運作，不佔用工作列，僅在系統托盤顯示圖示（可從托盤暫停預覽）
- ✅ **快速預覽**：滑鼠一停穩就在背景開始解碼，懸停時間到時多半已經算好；大型 JPEG 解碼比 v1.2 快 3–13 倍
- ✅ **多國語言**：繁體中文、简体中文、English、日本語、한국어，第一次啟動依 Windows 顯示語言自動選擇
- ✅ **跟隨系統主題**：預覽視窗與設定視窗自動切換淺色／深色
- ✅ **錯誤不打斷**：無法預覽時在預覽視窗內顯示原因，詳細內容寫入 `%AppData%\HoverPeek\logs\`
- ✅ **優雅降級**：Markdown/語法高亮失敗時自動切換到純文字顯示
- ✅ **不干擾工作**：預覽視窗不搶焦點，不影響目前操作
- ✅ **自動定位**：預覽視窗智慧定位，避免超出螢幕邊界
- ✅ **設定持久化**：設定儲存於 JSON，重啟後保留

## 📦 安裝

### 系統需求

- **作業系統**：Windows 10/11（64-bit）
- **.NET Runtime**：.NET 8.0 Desktop Runtime
- **WebView2**（可選）：用於 Markdown 渲染，Windows 10/11 通常已內建

### 下載

前往 [Releases](https://github.com/kyosora/HoverPeek/releases) 頁面下載最新版本。

### 從原始碼建置

```bash
# 複製專案
git clone https://github.com/kyosora/HoverPeek.git
cd HoverPeek

# 建置專案（Release 模式）
dotnet build HoverPeek.slnx -c Release

# 執行單元測試
dotnet test HoverPeek.slnx

# 執行檔位於
# src/HoverPeek.App/bin/Release/net8.0-windows/HoverPeek.exe
```

開發輔助工具（不在 solution 裡，手動執行）：

```bash
# 圖片預覽效能基準測試：舊版流程 vs 新版，100% 與 150% 縮放
dotnet run -c Release --project tools/HoverPeek.Bench -- <圖片路徑...>

# 離屏截圖：把預覽視窗與設定視窗畫成 PNG（淺色＋深色），不會顯示在螢幕上
dotnet run --project tools/HoverPeek.UiSnapshot -- <輸出資料夾> <圖片檔> <程式碼檔> <資料夾>

# 新增介面字串（同時寫入繁中／英文 resx 與 Strings.cs）
python tools/add_strings.py strings.json

# 由翻譯 JSON 產生其他語言的 resx（會先驗證 key、{0} 佔位符、換行數）
python tools/build_locale_resx.py ja translations/ja.json
```

### CI 與發布

- 每次 push / PR：GitHub Actions 建置並跑單元測試（`.github/workflows/ci.yml`）
- 發新版：改 `HoverPeek.App.csproj` 的 `<Version>`、寫 `.github/release-notes/v<版本>.md`，再 `git tag v<版本> && git push origin v<版本>`；`release.yml` 會測試、打包 win-x64 zip 並建立 GitHub Release

## 🚀 使用方式

1. **啟動應用程式**
   - 執行 `HoverPeek.exe`
   - 應用程式會最小化到系統托盤（右下角通知區域）

2. **預覽檔案**
   - 在檔案總管中，將滑鼠移到檔案上
   - 懸停約 400ms（可設定）
   - 預覽視窗會自動彈出

3. **開啟設定**
   - 右鍵點擊系統托盤的 HoverPeek 圖示
   - 選擇「設定」
   - 調整各項參數後點擊「儲存」

4. **退出程式**
   - 右鍵點擊系統托盤圖示
   - 選擇「退出 HoverPeek」

## 🎨 功能展示

### 圖片預覽
懸停在圖片上，立即顯示高品質預覽，支援 GIF 動畫

### Markdown 渲染
`.md` 檔案自動渲染為格式化的 HTML，支援程式碼區塊、表格、清單等

### 語法高亮
程式碼檔案（.py、.js、.cs 等）自動套用語法顏色，並顯示行號

### 資料夾瀏覽
懸停資料夾即可瀏覽內容，雙擊檔案直接預覽

### 壓縮檔瀏覽
展開壓縮檔內容清單，雙擊檔案可直接預覽（無需解壓）

### 影片播放
自動播放影片，支援硬體加速解碼

## ⚙️ 技術架構

### 技術棧

- **框架**：WPF (.NET 8.0)
- **圖片處理**：SkiaSharp
- **壓縮檔**：SharpCompress
- **影片播放**：LibVLCSharp
- **Markdown 渲染**：Markdig + Microsoft Edge WebView2
- **語法高亮**：AvalonEdit
- **全域滑鼠鉤子**：Windows Low-Level Mouse Hook (`WH_MOUSE_LL`)
- **檔案路徑解析**：UI Automation API

### 專案結構

```
HoverPeek/
├── src/
│   ├── HoverPeek.App/          # 主應用程式（WPF）
│   │   ├── App.xaml.cs         # 應用程式入口，整合所有元件
│   │   ├── SettingsWindow.xaml # 設定視窗 UI
│   │   └── SettingsWindow.xaml.cs
│   ├── HoverPeek.Core/         # 核心邏輯（無 UI 依賴）
│   │   ├── MouseHook/          # 全域滑鼠 Hook
│   │   ├── FileResolver/       # 檔案路徑解析
│   │   ├── Preview/            # 預覽提供者
│   │   │   ├── ImagePreviewProvider.cs
│   │   │   ├── ArchivePreviewProvider.cs
│   │   │   ├── FolderPreviewProvider.cs # 🆕 資料夾預覽
│   │   │   ├── VideoPreviewProvider.cs
│   │   │   └── TextPreviewProvider.cs
│   │   └── Settings/           # 🆕 設定服務
│   │       ├── AppSettings.cs
│   │       └── SettingsService.cs
│   └── HoverPeek.UI/           # UI 元件
│       └── PreviewWindow.xaml  # 預覽視窗
└── README.md
```

### 核心機制

#### 1. 全域滑鼠 Hook
- 使用 `SetWindowsHookEx` 註冊低階滑鼠 Hook
- 不阻擋滑鼠事件傳遞（`CallNextHookEx`）
- 效能開銷 <1ms

#### 2. 懸停偵測與預覽管線
- 抖動容忍：滑鼠輕微移動（<8px）不會中斷計時
- 停穩 60ms 就開一個預覽 session，在背景找檔案、解碼；懸停時間到時直接顯示
- 同一份檔案只解碼一次，結果放進 LRU 快取（24 筆／160MB，檔案改過自動失效）
- 還沒解完時先顯示 Windows 縮圖快取，再換成清晰版
- 從一個檔案滑到另一個檔案時直接換內容，不會先消失再出現

#### 3. 檔案路徑解析
- 主要方案：UI Automation API（支援各種視圖模式）
- 備用方案：Shell COM 介面
- 錯誤處理：解析失敗時靜默忽略

#### 4. 圖片解碼
- Skia 依目標尺寸縮小解碼（JPEG 走 DCT k/8 縮放），直接交出 BGRA 像素給 WPF，不再經過 PNG
- 依 EXIF 方向轉正（手機直拍照片）
- Skia 不支援的格式（AVIF、HEIC…）改用 Windows 已安裝的 WIC codec

#### 5. 預覽視窗
- 不搶焦點：`ShowActivated=False` + `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`
- PerMonitorV2 DPI：多螢幕、不同縮放比例下位置正確、圖片 1:1 清晰
- 位置：游標旁（空間不夠時翻到另一側，不蓋住游標）或螢幕中央
- Win11 原生圓角與陰影（DWM），不用透明視窗，WebView2 才能正常顯示
- Markdown 預覽關閉腳本執行與導覽，避免惡意 `.md` 執行 JavaScript

## 🛠️ 設定檔案

設定儲存於：`%AppData%\HoverPeek\settings.json`

範例設定：

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

v1.2 以前的設定檔會在第一次啟動時自動升級：沒改過的舊預設值（懸停 500ms、置中、淡入 150ms…）換成新預設，自訂過的值保留。超出範圍的值會被拉回合法範圍。

## 🤝 貢獻

歡迎提交 Issue 和 Pull Request！

### 開發流程

1. Fork 本專案
2. 建立功能分支（`git checkout -b feature/amazing-feature`）
3. 提交變更（`git commit -m 'Add amazing feature'`）
4. 推送到分支（`git push origin feature/amazing-feature`）
5. 開啟 Pull Request

### 建議的改進方向

- [ ] PDF 預覽支援
- [x] 多螢幕支援改進（v1.3：PerMonitorV2）
- [x] Markdown 跟隨淺色／深色主題（v1.3）
- [ ] 更多語法高亮語言
- [x] 縮圖快取機制（v1.3：LRU 快取＋Windows 縮圖快取）
- [x] 效能最佳化（v1.3：推測預載、縮小解碼）

## 📄 授權

本專案採用 **MIT License** 授權 - 詳見 [LICENSE](LICENSE) 檔案

## 🙏 致謝

- [QTTabBar](https://github.com/indiff/qttabbar) - 靈感來源
- [QuickLook](https://github.com/QL-Win/QuickLook) - 參考實作
- [SkiaSharp](https://github.com/mono/SkiaSharp) - 高效能圖片處理
- [SharpCompress](https://github.com/adamhathcock/sharpcompress) - 壓縮檔解析
- [LibVLCSharp](https://github.com/videolan/libvlcsharp) - 影片播放引擎
- [Markdig](https://github.com/xoofx/markdig) - Markdown 解析器
- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) - 程式碼編輯器與語法高亮
- [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) - HTML 渲染引擎

## 📮 聯絡

有任何問題或建議，歡迎開啟 [Issue](https://github.com/kyosora/HoverPeek/issues)

---

Made with ❤️ by [kyosora](https://github.com/kyosora)
