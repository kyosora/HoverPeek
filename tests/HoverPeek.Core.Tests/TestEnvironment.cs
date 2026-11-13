using System.Runtime.CompilerServices;

namespace HoverPeek.Core.Tests;

internal static class TestEnvironment
{
    /// <summary>
    /// 測試組件載入時就把 AppLog 導到暫存資料夾。
    /// 必須在任何程式碼碰到 AppLog 之前設定，因為 AppLog.LogDirectory 只讀一次。
    /// </summary>
    [ModuleInitializer]
    internal static void RedirectAppLog()
    {
        var dir = Path.Combine(Path.GetTempPath(), "HoverPeek.Tests", "logs");
        Environment.SetEnvironmentVariable("HOVERPEEK_LOG_DIR", dir);
    }
}
