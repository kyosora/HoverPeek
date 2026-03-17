using System.Globalization;
using System.Resources;

namespace HoverPeek.Core.Localization;

public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("HoverPeek.Core.Localization.Strings", typeof(Strings).Assembly);

    public static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object[] args) =>
        string.Format(Get(key), args);

    // Core - Error messages
    public static string FileNotFound => Get(nameof(FileNotFound));
    public static string FileEmpty => Get(nameof(FileEmpty));
    public static string CannotDecodeImage => Get(nameof(CannotDecodeImage));
    public static string FileTooSmallHeader => Get(nameof(FileTooSmallHeader));
    public static string SKCodecNull => Get(nameof(SKCodecNull));
    public static string SKBitmapDecodeNull => Get(nameof(SKBitmapDecodeNull));
    public static string ResizeNull => Get(nameof(ResizeNull));
    public static string GeneratePreviewFailed => Get(nameof(GeneratePreviewFailed));
    public static string GenerateFromBytesFailed => Get(nameof(GenerateFromBytesFailed));
    public static string SvgFileEmpty => Get(nameof(SvgFileEmpty));
    public static string CannotLoadSvg => Get(nameof(CannotLoadSvg));
    public static string SvgInvalidSize => Get(nameof(SvgInvalidSize));
    public static string GenerateSvgFailed => Get(nameof(GenerateSvgFailed));
    public static string ReadTextFailed => Get(nameof(ReadTextFailed));
    public static string SaveSettingsFailed => Get(nameof(SaveSettingsFailed));

    // Core - User-facing text
    public static string FileTooLargeTitle => Get(nameof(FileTooLargeTitle));
    public static string FileSizeLabel => Get(nameof(FileSizeLabel));
    public static string MaxSizeLabel => Get(nameof(MaxSizeLabel));
    public static string TruncatedMessage => Get(nameof(TruncatedMessage));

    // UI - PreviewWindow
    public static string ExpandFileList => Get(nameof(ExpandFileList));
    public static string CollapseFileList => Get(nameof(CollapseFileList));
    public static string ArchiveItemCount => Get(nameof(ArchiveItemCount));
    public static string UnsupportedFileType => Get(nameof(UnsupportedFileType));
    public static string VideoCannotPlay => Get(nameof(VideoCannotPlay));
    public static string HeaderName => Get(nameof(HeaderName));
    public static string HeaderSize => Get(nameof(HeaderSize));
    public static string HeaderType => Get(nameof(HeaderType));
    public static string EncodingUnknown => Get(nameof(EncodingUnknown));

    // App - System tray
    public static string TrayTooltip => Get(nameof(TrayTooltip));
    public static string TraySettings => Get(nameof(TraySettings));
    public static string TrayExitApp => Get(nameof(TrayExitApp));
    public static string TrayRunningTitle => Get(nameof(TrayRunningTitle));
    public static string TrayRunningMessage => Get(nameof(TrayRunningMessage));
    public static string MouseHookFailed => Get(nameof(MouseHookFailed));
    public static string StartupFailedTitle => Get(nameof(StartupFailedTitle));

    // App - Preview errors
    public static string PreviewErrorTitle => Get(nameof(PreviewErrorTitle));
    public static string PreviewErrorMessage => Get(nameof(PreviewErrorMessage));
    public static string ErrorLabel => Get(nameof(ErrorLabel));
    public static string FullStackTrace => Get(nameof(FullStackTrace));

    // App - Unhandled exceptions
    public static string UnhandledExceptionMessage => Get(nameof(UnhandledExceptionMessage));
    public static string FatalErrorMessage => Get(nameof(FatalErrorMessage));
    public static string TaskExceptionMessage => Get(nameof(TaskExceptionMessage));
    public static string ErrorTitle => Get(nameof(ErrorTitle));
    public static string FatalErrorTitle => Get(nameof(FatalErrorTitle));
    public static string TaskErrorTitle => Get(nameof(TaskErrorTitle));

    // Settings window
    public static string SettingsWindowTitle => Get(nameof(SettingsWindowTitle));
    public static string TabHoverBehavior => Get(nameof(TabHoverBehavior));
    public static string TabPreviewWindow => Get(nameof(TabPreviewWindow));
    public static string TabImagePreview => Get(nameof(TabImagePreview));
    public static string TabTextPreview => Get(nameof(TabTextPreview));
    public static string TabVideoPreview => Get(nameof(TabVideoPreview));
    public static string TabArchivePreview => Get(nameof(TabArchivePreview));
    public static string TabStartup => Get(nameof(TabStartup));

    // Settings - Labels
    public static string LabelHoverDelay => Get(nameof(LabelHoverDelay));
    public static string LabelJitterTolerance => Get(nameof(LabelJitterTolerance));
    public static string LabelAutoCloseDelay => Get(nameof(LabelAutoCloseDelay));
    public static string DescriptionLabel => Get(nameof(DescriptionLabel));
    public static string DescHoverDelay => Get(nameof(DescHoverDelay));
    public static string DescJitterTolerance => Get(nameof(DescJitterTolerance));
    public static string DescAutoCloseDelay => Get(nameof(DescAutoCloseDelay));

    public static string LabelWindowWidth => Get(nameof(LabelWindowWidth));
    public static string LabelWindowHeight => Get(nameof(LabelWindowHeight));
    public static string LabelCenterWindow => Get(nameof(LabelCenterWindow));
    public static string LabelFadeInDuration => Get(nameof(LabelFadeInDuration));
    public static string LabelFadeOutDuration => Get(nameof(LabelFadeOutDuration));

    public static string LabelImageMaxDimension => Get(nameof(LabelImageMaxDimension));
    public static string LabelEnableGifAnimation => Get(nameof(LabelEnableGifAnimation));
    public static string DescImageScale => Get(nameof(DescImageScale));
    public static string DescGifStatic => Get(nameof(DescGifStatic));

    public static string LabelTextMaxFileSize => Get(nameof(LabelTextMaxFileSize));
    public static string LabelTextMaxLines => Get(nameof(LabelTextMaxLines));
    public static string LabelTextFontSize => Get(nameof(LabelTextFontSize));
    public static string LabelTextFontFamily => Get(nameof(LabelTextFontFamily));
    public static string LabelEnableMarkdownRendering => Get(nameof(LabelEnableMarkdownRendering));
    public static string LabelEnableSyntaxHighlighting => Get(nameof(LabelEnableSyntaxHighlighting));
    public static string DescMarkdownRendering => Get(nameof(DescMarkdownRendering));
    public static string DescSyntaxHighlighting => Get(nameof(DescSyntaxHighlighting));

    public static string LabelVideoAutoPlay => Get(nameof(LabelVideoAutoPlay));
    public static string LabelVideoMuted => Get(nameof(LabelVideoMuted));
    public static string DescVideoThumbnail => Get(nameof(DescVideoThumbnail));
    public static string DescVideoMuted => Get(nameof(DescVideoMuted));

    public static string LabelArchiveAutoExpand => Get(nameof(LabelArchiveAutoExpand));
    public static string DescArchiveAutoExpand => Get(nameof(DescArchiveAutoExpand));
    public static string DescArchiveManual => Get(nameof(DescArchiveManual));

    public static string LabelStartWithWindows => Get(nameof(LabelStartWithWindows));
    public static string DescStartup => Get(nameof(DescStartup));
    public static string DescTrayMinimize => Get(nameof(DescTrayMinimize));

    public static string LabelLanguage => Get(nameof(LabelLanguage));
    public static string LanguageZhTW => Get(nameof(LanguageZhTW));
    public static string LanguageEn => Get(nameof(LanguageEn));

    // Settings - Buttons
    public static string ButtonReset => Get(nameof(ButtonReset));
    public static string ButtonCancel => Get(nameof(ButtonCancel));
    public static string ButtonSave => Get(nameof(ButtonSave));

    // Settings - Messages
    public static string SaveSuccessMessage => Get(nameof(SaveSuccessMessage));
    public static string SaveSuccessLanguageChanged => Get(nameof(SaveSuccessLanguageChanged));
    public static string SaveErrorMessage => Get(nameof(SaveErrorMessage));
    public static string SaveErrorTitle => Get(nameof(SaveErrorTitle));
    public static string ResetConfirmMessage => Get(nameof(ResetConfirmMessage));
}
