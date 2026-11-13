using HoverPeek.Core.Imaging;

namespace HoverPeek.Core.Tests;

public sealed class ImageMathTests
{
    [Fact]
    public void FitWithin_SmallerImage_ReturnsOriginalSize()
    {
        Assert.Equal((320, 200), ImageMath.FitWithin(320, 200, 800));
    }

    [Theory]
    [InlineData(1600, 900, 800, 800, 450)]
    [InlineData(900, 1600, 800, 450, 800)]
    public void FitWithin_LargeImage_ScalesLongSideAndPreservesAspectRatio(
        int width, int height, int max, int expectedWidth, int expectedHeight)
    {
        var result = ImageMath.FitWithin(width, height, max);

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
        Assert.InRange(
            Math.Abs((double)result.Width / result.Height - (double)width / height),
            0,
            1.0 / Math.Min(result.Width, result.Height));
    }

    [Fact]
    public void FitWithin_ExtremeAspectRatio_KeepsBothDimensionsPositive()
    {
        Assert.Equal((800, 1), ImageMath.FitWithin(20_000, 3, 800));
    }

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(-1, 10, 10)]
    [InlineData(10, 0, 10)]
    [InlineData(10, -1, 10)]
    [InlineData(10, 10, 0)]
    [InlineData(10, 10, -1)]
    public void FitWithin_NonPositiveInput_Throws(int width, int height, int max)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageMath.FitWithin(width, height, max));
    }

    [Theory]
    [InlineData(1.5, 1200)]
    [InlineData(0, 800)]
    public void ToPhysicalPixels_UsesValidScaleOrOne(double dpiScale, int expected)
    {
        Assert.Equal(expected, ImageMath.ToPhysicalPixels(800, dpiScale));
    }

    [Fact]
    public void ToPhysicalPixels_NaNScale_UsesOne()
    {
        Assert.Equal(800, ImageMath.ToPhysicalPixels(800, double.NaN));
    }
}
