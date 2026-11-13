using HoverPeek.Core.Interop;

namespace HoverPeek.UI;

/// <summary>
/// 預覽視窗位置計算（純函式，全部用實體像素）。
/// 游標模式比照 QTTabBar：放在游標右下，空間不夠就翻到左邊／上面，絕不蓋住游標本身，
/// 否則滑鼠一進入預覽視窗就會被當成「要操作預覽」，檔案總管那邊就換不了下一個檔案。
/// </summary>
public static class WindowPlacement
{
    /// <summary>游標與視窗的間距（邏輯像素）。</summary>
    public const int CursorGap = 20;

    public static DisplayInfo.PixelRect Compute(
        int cursorX, int cursorY, int width, int height,
        DisplayInfo.PixelRect workArea, bool center, double dpiScale)
    {
        width = Math.Min(width, workArea.Width);
        height = Math.Min(height, workArea.Height);

        int left, top;
        if (center)
        {
            left = workArea.Left + (workArea.Width - width) / 2;
            top = workArea.Top + (workArea.Height - height) / 2;
        }
        else
        {
            var gap = (int)Math.Round(CursorGap * dpiScale);

            // 水平：優先右側，放不下改左側，兩邊都放不下就選空間大的一側貼齊
            var rightSpace = workArea.Right - (cursorX + gap);
            var leftSpace = (cursorX - gap) - workArea.Left;
            if (rightSpace >= width)
                left = cursorX + gap;
            else if (leftSpace >= width)
                left = cursorX - gap - width;
            else
                left = rightSpace >= leftSpace ? workArea.Right - width : workArea.Left;

            // 垂直：視窗頂端對齊游標附近，往下放不下就往上推到工作區底
            top = cursorY - gap;
            if (top + height > workArea.Bottom)
                top = workArea.Bottom - height;
        }

        left = Math.Clamp(left, workArea.Left, workArea.Right - width);
        top = Math.Clamp(top, workArea.Top, workArea.Bottom - height);
        return new DisplayInfo.PixelRect(left, top, left + width, top + height);
    }
}
