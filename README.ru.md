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
  - **Умное определение кодировки**: автоопределение UTF-8, Big5 и системной кодировки по умолчанию
  - Настраиваемые ограничения на размер файла и количество строк

### ⚙️ Полноценная система настроек
- **Поведение при наведении** — задержка, допуск дрожания, задержка автозакрытия
- **Окно предпросмотра** — размер, позиция (по центру экрана / следовать за мышью), анимации затухания
- **Изображения** — максимальный размер предпросмотра, переключатель анимации GIF
- **Текст** — максимальный размер файла, максимальное количество строк, размер/семейство шрифта, переключатели Markdown и подсветки синтаксиса
- **Видео** — автовоспроизведение, отключение звука
- **Архивы** — авторазворачивание списка файлов
- **О программе** — информация о версии, авторе, ссылка на GitHub
- **Автозапуск** — запуск при старте Windows (через реестр)

### 🎯 Ключевые особенности

- ✅ **Легковесность** — работает в фоне, живёт в системном трее
- ✅ **Мгновенный предпросмотр** — не нужно открывать никаких приложений
- ✅ **Изящная деградация** — при сбое Markdown/подсветки синтаксиса откатывается к обычному тексту
- ✅ **Ненавязчивость** — окно предпросмотра никогда не перехватывает фокус
- ✅ **Умное позиционирование** — автоматически корректируется, чтобы не выходить за границы экрана
- ✅ **Сохранение настроек** — сохраняются в JSON, переживают перезапуск

## 📦 Установка

### Требования

- **ОС**: Windows 10/11 (64-bit)
- **.NET Runtime**: .NET 8.0 Desktop Runtime
- **WebView2** (опционально): для рендеринга Markdown — обычно предустановлен в  Windows 10/11

### Загрузка

Перейдите на страницу [Releases](https://github.com/kyosora/HoverPeek/releases) за последней версией.

### Сборка из исходников

```bash
git clone https://github.com/kyosora/HoverPeek.git
cd HoverPeek

# Сборка в режиме Release
dotnet build src/HoverPeek.App/HoverPeek.App.csproj -c Release

# Результат:
# src/HoverPeek.App/bin/Release/net8.0-windows/HoverPeek.exe
```

## 🚀 Использование

1. **Запуск** — Запустите `HoverPeek.App.exe`. Программа запустится свёрнутой в системный трей.
2. **Предпросмотр** — Наведите курсор на файл в Проводнике примерно на 500 мс (настраивается). Окно предпросмотра появится автоматически.
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
│   ├── HoverPeek.App/          # Основное приложение (WPF)
│   │   ├── App.xaml.cs         # Точка входа, оркестрация всех компонентов
│   │   ├── SettingsWindow.xaml # UI настроек
│   │   └── SettingsWindow.xaml.cs
│   ├── HoverPeek.Core/         # Основная логика (независимая от UI)
│   │   ├── MouseHook/          # Глобальный хук мыши
│   │   ├── FileResolver/       # Определение пути к файлу
│   │   ├── Preview/            # Провайдеры предпросмотра
│   │   │   ├── ImagePreviewProvider.cs # Предпросмотр изображений
│   │   │   ├── ArchivePreviewProvider.cs # Предпросмотр архивов
│   │   │   ├── FolderPreviewProvider.cs # Предпросмотр папок
│   │   │   ├── VideoPreviewProvider.cs # Предпросмотр видео
│   │   │   └── TextPreviewProvider.cs # Предпросмотр текста
│   │   └── Settings/           # Сервис настроек
│   │       ├── AppSettings.cs
│   │       └── SettingsService.cs
│   └── HoverPeek.UI/           # Компоненты UI
│       └── PreviewWindow.xaml  # Окно предпросмотра
└── README.md
```

### Как это работает

1. **Глобальный хук мыши** —  Регистрирует низкоуровневый хук мыши через `SetWindowsHookEx`. Не блокирует события мыши. Накладные расходы < 1 мс.
2. **Обнаружение наведения** — Допуск дрожания (< 8 px) предотвращает случайные срабатывания. Отменяемо через `CancellationTokenSource`. Блокирует предпросмотр, чтобы избежать повторного срабатывания при движении мыши.
3. **Определение пути к файлу** — Основной способ: UI Automation API (поддерживает все режимы просмотра Проводника). Резервный: COM-интерфейс Shell. Тихий отказ при ошибках.
4. **Окно предпросмотра** — Никогда не перехватывает фокус (`ShowActivated=False` + `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`). Умное позиционирование предотвращает выход за границы экрана. Настраиваемые анимации затухания.

## 🛠️ Конфигурация

Настройки хранятся в: `%AppData%\HoverPeek\settings.json`

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

## 🤝 Участие в разработке

Issues и Pull Requests приветствуются!

1. Сделайте форк репозитория
2. Создайте ветку для фичи (`git checkout -b feature/amazing-feature`)
3. Закоммитьте изменения (`git commit -m 'Add amazing feature'`)
4. Запушьте в ветку (`git push origin feature/amazing-feature`)
5. Откройте Pull Request

### Планы развития

- [ ] Поддержка предпросмотра PDF
- [ ] Улучшения для мультимониторных конфигураций
- [ ] Пользовательские CSS-темы для Markdown
- [ ] Больше языков подсветки синтаксиса
- [ ] Кэширование миниатюр
- [ ] Мониторинг и оптимизация производительности

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
