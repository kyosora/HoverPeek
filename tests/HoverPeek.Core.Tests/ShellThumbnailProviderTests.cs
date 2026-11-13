using HoverPeek.Core.Imaging;

namespace HoverPeek.Core.Tests;

public sealed class ShellThumbnailProviderTests
{
    [Fact]
    public void NormalizeAlpha_AllAlphaZero_MakesPixelsOpaque()
    {
        var pixels = new byte[] { 1, 2, 3, 0, 4, 5, 6, 0 };

        ShellThumbnailProvider.NormalizeAlpha(pixels);

        Assert.Equal(new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 }, pixels);
    }

    [Fact]
    public void NormalizeAlpha_AnyNonZeroAlpha_LeavesEntireBufferUnchanged()
    {
        var pixels = new byte[] { 1, 2, 3, 0, 4, 5, 6, 127 };
        var original = pixels.ToArray();

        ShellThumbnailProvider.NormalizeAlpha(pixels);

        Assert.Equal(original, pixels);
    }
}
