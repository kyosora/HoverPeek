using System.Windows.Media;
using System.Windows.Media.Imaging;
using HoverPeek.Core.Imaging;

namespace HoverPeek.Core.Tests;

public sealed class GifFrameStreamerTests
{
    [Fact]
    public void TryCreate_StaticGif_ReturnsNull()
    {
        Assert.Null(GifFrameStreamer.TryCreate(EncodeGif(Colors.Red), 800));
    }

    [Fact]
    public void TryCreate_NotAGif_ReturnsNull()
    {
        Assert.Null(GifFrameStreamer.TryCreate(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 800));
    }

    [Fact]
    public void DecodeNext_PlaysFramesInOrderAndLoops()
    {
        var gif = EncodeGif(Colors.Red, Colors.Lime, Colors.Blue);
        using var streamer = GifFrameStreamer.TryCreate(gif, 800)!;
        Assert.Equal(3, streamer.FrameCount);

        var buffer = new byte[streamer.Stride * streamer.Height];
        var seen = new List<(byte B, byte G, byte R)>();
        for (var i = 0; i < 4; i++)
        {
            var duration = streamer.DecodeNext(buffer);
            Assert.True(duration >= 20, $"影格延遲 {duration}ms 太短，應套用最小值");
            var center = (streamer.Height / 2 * streamer.Stride) + (streamer.Width / 2 * 4);
            seen.Add((buffer[center], buffer[center + 1], buffer[center + 2]));
        }

        Assert.Equal((0, 0, 255), (seen[0].B, seen[0].G, seen[0].R)); // 紅
        Assert.Equal((0, 255, 0), (seen[1].B, seen[1].G, seen[1].R)); // 綠
        Assert.Equal((255, 0, 0), (seen[2].B, seen[2].G, seen[2].R)); // 藍
        Assert.Equal(seen[0], seen[3]); // 播完從頭開始
    }

    [Fact]
    public void Output_IsScaledToMaxPixels_SoMemoryDoesNotDependOnSourceSize()
    {
        var gif = EncodeGif(1600, 900, Colors.Red, Colors.Blue);
        using var streamer = GifFrameStreamer.TryCreate(gif, 400)!;

        Assert.Equal(400, streamer.Width);
        Assert.Equal(225, streamer.Height);
    }

    [Fact]
    public void DecodeNext_RejectsTooSmallBuffer()
    {
        using var streamer = GifFrameStreamer.TryCreate(EncodeGif(Colors.Red, Colors.Blue), 800)!;
        Assert.Throws<ArgumentException>(() => streamer.DecodeNext(new byte[16]));
    }

    private static byte[] EncodeGif(params Color[] frames) => EncodeGif(64, 48, frames);

    private static byte[] EncodeGif(int width, int height, params Color[] frames)
    {
        var encoder = new GifBitmapEncoder();
        foreach (var color in frames)
        {
            var pixels = new byte[width * height * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
                pixels[i + 3] = 255;
            }
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4)));
        }

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
