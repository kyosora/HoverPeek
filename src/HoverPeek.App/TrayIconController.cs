using System.IO;
using System.Drawing;
using HoverPeek.Core.Diagnostics;
using HoverPeek.Core.Localization;
using WinForms = System.Windows.Forms;

namespace HoverPeek.App;

/// <summary>
/// 系統匣圖示與選單。雙擊開設定；選單可暫停預覽（例如玩全螢幕遊戲、簡報時）。
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _pauseItem;

    public event Action? SettingsRequested;
    public event Action<bool>? PauseToggled;
    public event Action? ExitRequested;

    public TrayIconController(Icon icon)
    {
        _pauseItem = new WinForms.ToolStripMenuItem(Strings.TrayPause);
        _pauseItem.Click += (_, _) => SetPaused(!IsPaused, raise: true);

        var settingsItem = new WinForms.ToolStripMenuItem(Strings.TraySettings);
        settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold); // 雙擊的預設動作
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

        var logItem = new WinForms.ToolStripMenuItem(Strings.TrayOpenLog);
        logItem.Click += (_, _) => OpenLogFolder();

        var exitItem = new WinForms.ToolStripMenuItem(Strings.TrayExitApp);
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.AddRange(new WinForms.ToolStripItem[]
        {
            _pauseItem,
            settingsItem,
            new WinForms.ToolStripSeparator(),
            logItem,
            new WinForms.ToolStripSeparator(),
            exitItem,
        });

        _icon = new WinForms.NotifyIcon
        {
            Icon = icon,
            Text = Strings.TrayTooltip,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => SettingsRequested?.Invoke();
    }

    public bool IsPaused { get; private set; }

    public void SetPaused(bool paused, bool raise = false)
    {
        IsPaused = paused;
        _pauseItem.Text = paused ? Strings.TrayResume : Strings.TrayPause;
        _pauseItem.Checked = paused;
        _icon.Text = paused ? Strings.TrayPausedTooltip : Strings.TrayTooltip;
        if (raise)
            PauseToggled?.Invoke(paused);
    }

    private static void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(AppLog.LogDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppLog.LogDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"無法開啟日誌資料夾：{AppLog.LogDirectory}", ex);
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
