using System.Windows.Automation;

namespace HoverPeek.Core.FileResolver;

/// <summary>
/// 透過 UI Automation 取得檔案總管中滑鼠位置對應的檔案路徑。
///
/// 原理：
/// 1. 用 AutomationElement.FromPoint() 取得滑鼠位置的 UI 元素
/// 2. 檢查該元素是否屬於 explorer.exe 的 ListView/TreeView
/// 3. 從元素的 Name 屬性取得檔案名稱
/// 4. 結合當前資料夾路徑，組合出完整檔案路徑
/// </summary>
public sealed class ExplorerFileResolver
{
    public string? ResolveFileAtPoint(int screenX, int screenY)
    {
        try
        {
            var point = new System.Windows.Point(screenX, screenY);
            var element = AutomationElement.FromPoint(point);

            if (element == null)
                return null;

            var fileElement = FindFileElement(element);
            if (fileElement == null)
                return null;

            var fileName = fileElement.Current.Name;
            if (string.IsNullOrEmpty(fileName))
                return null;

            var folderPath = GetCurrentExplorerFolder(fileElement);
            if (string.IsNullOrEmpty(folderPath))
                return null;

            var fullPath = Path.Combine(folderPath, fileName);
            return File.Exists(fullPath) || Directory.Exists(fullPath) ? fullPath : null;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static AutomationElement? FindFileElement(AutomationElement element)
    {
        if (IsExplorerListItem(element))
            return element;

        var walker = TreeWalker.ControlViewWalker;
        var current = element;

        for (int i = 0; i < 5 && current != null; i++)
        {
            current = walker.GetParent(current);
            if (current != null && IsExplorerListItem(current))
                return current;
        }

        return null;
    }

    private static bool IsExplorerListItem(AutomationElement element)
    {
        var controlType = element.Current.ControlType;

        if (controlType != ControlType.ListItem &&
            controlType != ControlType.DataItem &&
            controlType != ControlType.TreeItem &&
            controlType != ControlType.Text &&
            controlType != ControlType.Image &&
            controlType != ControlType.Group &&
            controlType != ControlType.Custom)
        {
            return false;
        }

        var walker = TreeWalker.ControlViewWalker;
        var current = element;
        int depth = 0;

        while (current != null && depth < 10)
        {
            var className = current.Current.ClassName;

            if (className == "CabinetWClass" || className == "ExploreWClass")
                return true;

            if (className == "UIItemsView" ||
                className == "DirectUIHWND" ||
                className == "ShellView" ||
                className == "SHELLDLL_DefView")
            {
                return true;
            }

            current = walker.GetParent(current);
            depth++;
        }

        return false;
    }

    private static string? GetCurrentExplorerFolder(AutomationElement listItem)
    {
        var walker = TreeWalker.ControlViewWalker;
        var current = listItem;

        while (current != null &&
               current.Current.ClassName != "CabinetWClass" &&
               current.Current.ClassName != "ExploreWClass")
        {
            current = walker.GetParent(current);
        }

        if (current == null)
            return null;

        var addressBar = current.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(
                AutomationElement.AutomationIdProperty, "1001"));

        if (addressBar != null)
        {
            try
            {
                var valuePattern = addressBar.GetCurrentPattern(
                    ValuePattern.Pattern) as ValuePattern;
                var path = valuePattern?.Current.Value;
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
            catch
            {
            }
        }

        return GetFolderPathViaCom(current);
    }

    private static string? GetFolderPathViaCom(AutomationElement explorerWindow)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
                return null;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null)
                return null;

            var windows = shell.Windows();
            var hwnd = explorerWindow.Current.NativeWindowHandle;

            for (int i = 0; i < windows.Count; i++)
            {
                dynamic? window = windows.Item(i);
                if (window == null)
                    continue;

                try
                {
                    if (window.HWND == hwnd)
                    {
                        string url = window.LocationURL;
                        if (url.StartsWith("file:///"))
                        {
                            return new Uri(url).LocalPath;
                        }
                        return window.Document?.Folder?.Self?.Path;
                    }
                }
                catch
                {
                    continue;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
