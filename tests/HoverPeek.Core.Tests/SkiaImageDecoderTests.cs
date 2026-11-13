using HoverPeek.Core.Imaging;
using SkiaSharp;

namespace HoverPeek.Core.Tests;

public sealed class SkiaImageDecoderTests
{
    [Fact]
    public void TryDecode_LargeJpeg_DecodesToRequestedDimensionsAndMetadata()
    {
        var bytes = EncodeSolidImage(4000, 3000, SKColors.CornflowerBlue, SKEncodedImageFormat.Jpeg);

        var image = Assert.IsType<DecodedImage>(SkiaImageDecoder.TryDecode(bytes, 800, 1.0));

        Assert.Equal(800, image.Width);
        Assert.Equal(600, image.Height);
        Assert.Equal(image.Width * 4, image.Stride);
        Assert.Equal(image.Stride * image.Height, image.Pixels.Length);
        Assert.Equal(4000, image.SourceWidth);
        Assert.Equal(3000, image.SourceHeight);
        Assert.Equal("JPEG", image.Format);
    }

    [Fact]
    public void ChooseDecodeSize_WhenNearestJpegStepIsTooSmall_UsesNextStepInsteadOfFullSize()
    {
        // 6000→800 的比例約 0.133，最接近的 1/8 級距是 750px，比目標小；
        // 應該選 2/8（1500px），而不是退回 6000px 全尺寸解碼（2026-11-14 bench 抓到的回歸）
        var bytes = EncodeSolidImage(6000, 4000, SKColors.Orange, SKEncodedImageFormat.Jpeg);
        using var codec = SKCodec.Create(new MemoryStream(bytes));

        var size = SkiaImageDecoder.ChooseDecodeSize(codec, 800, 533);

        Assert.Equal(new SKSizeI(1500, 1000), size);
    }

    [Fact]
    public void TryDecode_DpiScale_SetsPhysicalDpi()
    {
        var bytes = EncodeSolidImage(300, 200, SKColors.Red, SKEncodedImageFormat.Jpeg);

        var image = Assert.IsType<DecodedImage>(SkiaImageDecoder.TryDecode(bytes, 800, 1.5));

        Assert.Equal(144, image.DpiX);
        Assert.Equal(144, image.DpiY);
    }

    [Fact]
    public void TryDecode_SmallImage_DoesNotUpscale()
    {
        var bytes = EncodeSolidImage(300, 200, SKColors.Green, SKEncodedImageFormat.Png);

        var image = Assert.IsType<DecodedImage>(SkiaImageDecoder.TryDecode(bytes, 800, 1.0));

        Assert.Equal((300, 200), (image.Width, image.Height));
    }

    [Fact]
    public void TryDecode_TransparentPng_PreservesPremultipliedTransparentPixel()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(1200, 800, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.White);
        bitmap.SetPixel(0, 0, SKColors.Transparent);
        var bytes = Encode(bitmap, SKEncodedImageFormat.Png);

        var image = Assert.IsType<DecodedImage>(SkiaImageDecoder.TryDecode(bytes, 1200, 1.0));

        Assert.Equal(new byte[] { 0, 0, 0, 0 }, image.Pixels[..4]);
    }

    [Fact]
    public void TryDecode_Garbage_ReturnsNullWithoutThrowing()
    {
        Assert.Null(SkiaImageDecoder.TryDecode(new byte[] { 12, 34, 56, 78, 90 }, 800, 1.0));
    }

    [Theory]
    [InlineData("GIF89a", true)]
    [InlineData("GIF87a", true)]
    [InlineData("GIF88a", false)]
    [InlineData("PNG89a", false)]
    [InlineData("GIF89", false)]
    [InlineData("", false)]
    public void IsGif_RecognizesOnlyCompleteGifHeaders(string header, bool expected)
    {
        Assert.Equal(expected, SkiaImageDecoder.IsGif(System.Text.Encoding.ASCII.GetBytes(header)));
    }

    private static byte[] EncodeSolidImage(int width, int height, SKColor color, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(color);
        return Encode(bitmap, format);
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
