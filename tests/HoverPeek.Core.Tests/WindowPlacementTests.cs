using HoverPeek.Core.Interop;
using HoverPeek.UI;

namespace HoverPeek.Core.Tests;

public sealed class WindowPlacementTests
{
    private static readonly DisplayInfo.PixelRect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void Compute_CenterMode_CentersWindow()
    {
        var result = WindowPlacement.Compute(100, 100, 800, 600, WorkArea, center: true, dpiScale: 1.0);

        Assert.Equal(new DisplayInfo.PixelRect(560, 220, 1360, 820), result);
    }

    [Fact]
    public void Compute_CursorModeWithRoom_PlacesWindowToRightByGap()
    {
        var result = WindowPlacement.Compute(100, 100, 300, 200, WorkArea, center: false, dpiScale: 1.0);

        Assert.Equal(120, result.Left);
    }

    [Fact]
    public void Compute_CursorNearRightEdge_PlacesWindowLeftOfCursor()
    {
        const int cursorX = 1900;

        var result = WindowPlacement.Compute(cursorX, 500, 300, 200, WorkArea, center: false, dpiScale: 1.0);

        Assert.True(result.Right <= cursorX - WindowPlacement.CursorGap);
    }

    [Fact]
    public void Compute_CursorNearBottom_AlignsWindowToWorkAreaBottom()
    {
        var result = WindowPlacement.Compute(500, 1030, 300, 200, WorkArea, center: false, dpiScale: 1.0);

        Assert.Equal(WorkArea.Bottom, result.Bottom);
    }

    [Fact]
    public void Compute_WindowLargerThanWorkArea_ClampsSizeAndPosition()
    {
        var result = WindowPlacement.Compute(500, 500, 3000, 2000, WorkArea, center: false, dpiScale: 1.0);

        Assert.Equal(WorkArea, result);
    }

    [Theory]
    [InlineData(500, 500)]
    [InlineData(1500, 500)]
    public void Compute_CursorModeWithHorizontalRoom_DoesNotContainCursor(int cursorX, int cursorY)
    {
        var result = WindowPlacement.Compute(cursorX, cursorY, 300, 200, WorkArea, center: false, dpiScale: 1.0);

        Assert.False(result.Contains(cursorX, cursorY));
    }

    [Fact]
    public void Compute_HighDpi_ScalesCursorGap()
    {
        var result = WindowPlacement.Compute(100, 100, 300, 200, WorkArea, center: false, dpiScale: 1.5);

        Assert.Equal(130, result.Left);
    }
}
