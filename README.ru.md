# HoverPeek

> 🖱️ Легковесный инструмент для предпросмотра файлов в Windows — наведите курсор на файл в Проводнике, чтобы мгновенно увидеть его содержимое.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)

**[繁體中文](README.md)** | **[English](README.en.md)** | **Русский**

## ✨ Возможности

### 📸 Предпросмотр множества форматов

- **Изображения**
  - Форматы: JPG, PNG, GIF (animated), WebP, SVG, AVIF, BMP, TIFF, ICO
  - Автоматическое масштабирование с сохранением пропорций
  - Высококачественный рендеринг через SkiaSharp

- **Папки** (Новое)
  - Просмотр содержимого папки (имя, размер, дата изменения)
  - Двойной клик по файлу для прямого предпросмотра
  - Поддержка навигации назад

- **Архивы**
  - Форматы: ZIP, RAR, 7Z, TAR, GZ, BZ2, XZ, LZMA
  - Просмотр списка файлов внутри архивов
  - Двойной клик по файлу для прямого предпросмотра (без распаковки)
  - Встроенный кэш изображений для ускорения повторного просмотра

- **Видео**
  - Форматы: MP4, AVI, MKV, MOV, WMV, and more
  - На движке VLC с аппаратным декодированием
  - Настраиваемое автовоспроизведение и отключение звука

- **Текстовые файлы**
  - Поддержка **30+ форматов** файлов
  - **Рендеринг Markdown**: файлы `.md` отображаются как форматированный HTML (GitHub Flavored Markdown)
  - **Подсветка синтаксиса**: автоматическая раскраска кода для C#, Python, JavaScript, TypeScript, Java, Go, Rust, C/C++, Ruby, PHP, HTML, CSS, SQL, PowerShell, Bash, и других
  - **Умное определение кодировки**: BOM → строгий UTF-8 → Big5 → системная ANSI-кодировка, поэтому файлы Big5 больше не отображаются с искажённым текстом
  - Для файлов, превышающих ограничение по размеру или числу строк, загружается только начало с пометкой об усечении

### ⚙️ Полноценная система настроек

- **Поведение при наведении** — задержка (по умолчанию 400 мс), допуск дрожания, задержка автозакрытия (по умолчанию 400 мс)
- **Окно предпросмотра** — максимальный размер (предпросмотр изображения уменьшается до размера картинки), позиция (по умолчанию рядом с указателем или по центру экрана), анимация появления
- **Изображения** — максимальный размер предпросмотра (чёткое отображение на экранах с высоким DPI), анимация GIF, быстрый заполнитель из миниатюры Windows
- **Текст** — максимальный размер файла, максимальное количество строк, размер/семейство шрифта, переключатели Markdown и подсветки синтаксиса
- **Видео** — автовоспроизведение, отключение звука
- **Архивы** — авторазворачивание списка файлов
- **О программе** — информация о версии, авторе, ссылка на GitHub
- **Автозапуск** — запуск при старте Windows (через реестр)

### 🎯 Ключевые особенности

- ✅ **Легковесность** — работает в фоне, живёт в системном трее (предпросмотр можно приостановить через трей)
- ✅ **Скорость** — декодирование начинается, как только указатель останавливается, поэтому предпросмотр обычно готов к окончанию задержки наведения; большие JPEG декодируются в 3–13 раз быстрее, чем в v1.2
- ✅ **Многоязычность** — 繁體中文, 简体中文, English, 日本語, 한국어, Русский; при первом запуске язык выбирается по языку интерфейса Windows
- ✅ **Следование системной теме** — окна предпросмотра и настроек автоматически переключаются между светлой и тёмной темами
- ✅ **Ошибки не мешают работе** — проблемы отображаются внутри окна предпросмотра и записываются в `%AppData%\HoverPeek\logs\`
- ✅ **Изящная деградация** — при сбое Markdown/подсветки синтаксиса откатывается к обычному тексту
- ✅ **Ненавязчивость** — окно предпросмотра никогда не перехватывает фокус
- ✅ **Умное позиционирование** — автоматически корректируется, чтобы не выходить за границы экрана
- ✅ **Сохранение настроек** — сохраняются в JSON, переживают перезапуск

## 📦 Установка

### Требования

- **ОС**: Windows 10/11 (64-bit)
- **.NET Runtime**: .NET 8.0 Desktop Runtime
- **WebView2** (опционально): для рендеринга Markdown — обычно предустановлен в Windows 10/11

### Загрузка

Перейдите на страницу [Releases](https://github.com/kyosora/HoverPeek/releases) за последней версией.

### Сборка из исходников

```bash
git clone https://github.com/kyosora/HoverPeek.git
cd HoverPeek

# Сборка в режиме Release
dotnet build HoverPeek.slnx -c Release

# Запуск модульных тестов
dotnet test HoverPeek.slnx

# Результат:
# src/HoverPeek.App/bin/Release/net8.0-windows/HoverPeek.exe
```

Инструменты разработчика (не входят в solution, запускаются вручную):

```bash
# Бенчмарк предпросмотра изображений: конвейер v1.2 и текущий при масштабировании 100% и 150%
dotnet run -c Release --project tools/HoverPeek.Bench -- <image paths...>

# Снимки окон предпросмотра и настроек вне экрана (светлая + тёмная темы), на экране ничего не отображается
dotnet run --project tools/HoverPeek.UiSnapshot -- <output dir> <image> <code file> <folder>

# Добавление строк интерфейса (записывает resx для zh-TW / English и Strings.cs)
python tools/add_strings.py strings.json

# Создание resx другого языка из JSON перевода (сначала проверяет ключи, заполнители {0} и переносы строк)
python tools/build_locale_resx.py ja translations/ja.json
```

### CI и релизы

- Каждый push / PR: GitHub Actions собирает проект и запускает модульные тесты (`.github/workflows/ci.yml`)
- Новый релиз: обновите `<Version>` в `HoverPeek.App.csproj`, создайте `.github/release-notes/v<version>.md`, затем выполните `git tag v<version> && git push origin v<version>`; `release.yml` запускает тесты, упаковывает zip для win-x64 и создаёт GitHub Release

## 🚀 Использование

1. **Запуск** — Запустите `HoverPeek.exe`. Программа запустится свёрнутой в системный трей.
2. **Предпросмотр** — Наведите курсор на файл в Проводнике примерно на 400 мс (настраивается). Окно предпросмотра появится автоматически.
3. **Настройки** — Правый клик по иконке в трее → «Settings». Настройте и сохраните.
4. **Выход** — Правый клик по иконке в трее → «Exit HoverPeek».

## ⚙️ Технологический стек

| Компонент | Технология |
|-----------|-----------|
| Framework | WPF (.NET 8.0) |
| Image Processing | SkiaSharp |
| Archive Handling | SharpCompress |
| Video Playback | LibVLCSharp |
| Markdown Rendering | Markdig + Microsoft Edge WebView2 |
| Syntax Highlighting | AvalonEdit |
| Mouse Hook | Windows Low-Level Mouse Hook (`WH_MOUSE_LL`) |
| File Path Resolution | UI Automation API |

### Структура проекта

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

### Как это работает

1. **Глобальный хук мыши** — Регистрирует низкоуровневый хук мыши через `SetWindowsHookEx`. Не блокирует события мыши. Накладные расходы < 1 мс.
2. **Обнаружение наведения и конвейер предпросмотра** — Допуск дрожания (< 8 px) предотвращает случайные срабатывания. Сеанс предпросмотра начинается через 60 мс после остановки указателя, а определение и декодирование файла выполняются в фоне. Каждый файл декодируется один раз и хранится в LRU-кэше (24 элемента / 160 МБ, сбрасывается при изменении файла). Во время декодирования сначала отображается кэшированная миниатюра Windows. При переходе от одного файла к другому содержимое заменяется без мерцания окна.
3. **Определение пути к файлу** — Основной способ: UI Automation API (поддерживает все режимы просмотра Проводника). Резервный: COM-интерфейс Shell. Тихий отказ при ошибках.
4. **Декодирование изображений** — Skia декодирует сразу до целевого размера (для JPEG используется DCT-масштабирование k/8) и передаёт пиксели BGRA в WPF без промежуточного преобразования в PNG. Учитывается ориентация EXIF. Форматы, которые Skia не может прочитать (AVIF, HEIC…), обрабатываются установленными кодеками Windows WIC.
5. **Окно предпросмотра** — Никогда не перехватывает фокус (`ShowActivated=False` + `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`). Поддержка DPI PerMonitorV2 обеспечивает правильное позиционирование и попиксельную чёткость изображений на разных мониторах. Окно размещается рядом с указателем (при необходимости меняет сторону и никогда его не перекрывает) или по центру. Нативные скруглённые углы и тень Windows 11 создаются через DWM (без прозрачного окна, поэтому WebView2 работает). В предпросмотре Markdown отключены скрипты и навигация.

## 🛠️ Конфигурация

Настройки хранятся в: `%AppData%\HoverPeek\settings.json`

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

Файлы настроек из v1.2 и более ранних версий обновляются при первом запуске: неизменённые старые значения по умолчанию (наведение 500 мс, окно по центру, появление 150 мс…) заменяются новыми, пользовательские значения сохраняются, а значения вне допустимого диапазона приводятся к его границам.

## 🤝 Участие в разработке

Issues и Pull Requests приветствуются!

1. Сделайте форк репозитория
2. Создайте ветку для фичи (`git checkout -b feature/amazing-feature`)
3. Закоммитьте изменения (`git commit -m 'Add amazing feature'`)
4. Запушьте в ветку (`git push origin feature/amazing-feature`)
5. Откройте Pull Request

### Планы развития

- [ ] Поддержка предпросмотра PDF
- [x] Улучшения для мультимониторных конфигураций (v1.3: PerMonitorV2)
- [x] Markdown следует светлой / тёмной теме (v1.3)
- [ ] Больше языков подсветки синтаксиса
- [x] Кэширование миниатюр (v1.3: LRU-кэш + кэш миниатюр Windows)
- [x] Оптимизация производительности (v1.3: упреждающая загрузка, масштабированное декодирование)

## 📄 Лицензия

Распространяется под **MIT License** — подробности в [LICENSE](LICENSE).

## 🙏 Благодарности

- [QTTabBar](https://github.com/indiff/qttabbar) — Вдохновение
- [QuickLook](https://github.com/QL-Win/QuickLook) — Референсная реализация
- [SkiaSharp](https://github.com/mono/SkiaSharp) — Высокопроизводительная обработка изображений
- [SharpCompress](https://github.com/adamhathcock/sharpcompress) — Разбор архивов
- [LibVLCSharp](https://github.com/videolan/libvlcsharp) — Движок воспроизведения видео
- [Markdig](https://github.com/xoofx/markdig) — Парсер Markdown
- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) — Редактор кода и подсветка синтаксиса
- [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) — Движок рендеринга HTML

## 📮 Контакты

Вопросы или предложения? Откройте [Issue](https://github.com/kyosora/HoverPeek/issues).

---

Сделано с ❤️ by [kyosora](https://github.com/kyosora)
