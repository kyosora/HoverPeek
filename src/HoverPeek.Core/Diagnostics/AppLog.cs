namespace HoverPeek.Core.Diagnostics;

/// <summary>
/// 極簡檔案日誌：%APPDATA%\HoverPeek\logs\hoverpeek.log，超過 1MB 輪替成 .1。
/// 懸停預覽是背景工具，錯誤不該用 MessageBox 打斷使用者；寫到這裡方便事後追查。
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Lock = new();

    /// <summary>
    /// 日誌資料夾。環境變數 HOVERPEEK_LOG_DIR 可改寫（單元測試用，避免寫進使用者真正的日誌）。
    /// </summary>
    public static string LogDirectory { get; } =
        Environment.GetEnvironmentVariable("HOVERPEEK_LOG_DIR") is { Length: > 0 } overridden
            ? overridden
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HoverPeek", "logs");

    public static string LogFile => Path.Combine(LogDirectory, "hoverpeek.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [T{Environment.CurrentManagedThreadId}] {message}";
        if (ex != null)
            line += Environment.NewLine + ex;

        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(LogDirectory);
                var file = new FileInfo(LogFile);
                if (file.Exists && file.Length > MaxBytes)
                    File.Move(LogFile, LogFile + ".1", overwrite: true);
                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
        }
        catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException)
        {
            // 日誌本身寫不進去時不能再丟例外（會在錯誤處理裡造成第二個錯誤），退到偵錯輸出
            System.Diagnostics.Debug.WriteLine($"{line}\n(寫入日誌失敗：{logEx.Message})");
        }
    }
}
