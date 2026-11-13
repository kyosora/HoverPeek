using System.Text;
using HoverPeek.Core.Text;

namespace HoverPeek.Core.Tests;

public sealed class TextEncodingDetectorTests
{
    [Fact]
    public void Decode_Utf8WithoutBom_RoundTripsChineseText()
    {
        const string text = "中文測試";

        var result = TextEncodingDetector.Decode(Encoding.UTF8.GetBytes(text));

        Assert.Equal("UTF-8", result.EncodingName);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void Decode_Utf8WithBom_RemovesBom()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("中文")).ToArray();

        var result = TextEncodingDetector.Decode(bytes);

        Assert.Equal("UTF-8 BOM", result.EncodingName);
        Assert.Equal("中文", result.Text);
        Assert.DoesNotContain('\uFEFF', result.Text);
    }

    [Fact]
    public void Decode_Utf16LittleEndianWithBom_RemovesBom()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("中文")).ToArray();

        var result = TextEncodingDetector.Decode(bytes);

        Assert.Equal("UTF-16 LE", result.EncodingName);
        Assert.Equal("中文", result.Text);
    }

    [Fact]
    public void Decode_Big5_RoundTripsChineseText()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var big5 = Encoding.GetEncoding(950);
        const string text = "中文測試";

        var result = TextEncodingDetector.Decode(big5.GetBytes(text));

        Assert.Equal("Big5", result.EncodingName);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void Decode_TruncatedUtf8_DropsIncompleteCharacterWithoutReplacement()
    {
        var complete = Encoding.UTF8.GetBytes("中文測");
        var cutAfterTwoBytesOfLastCharacter = complete[..^1];

        var result = TextEncodingDetector.Decode(cutAfterTwoBytesOfLastCharacter, truncated: true);

        Assert.Equal("UTF-8", result.EncodingName);
        Assert.Equal("中文", result.Text);
        Assert.DoesNotContain('\uFFFD', result.Text);
    }

    [Fact]
    public void Utf8SafeLength_CompleteText_ReturnsFullLength()
    {
        var bytes = Encoding.UTF8.GetBytes("A中");

        Assert.Equal(bytes.Length, TextEncodingDetector.Utf8SafeLength(bytes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Utf8SafeLength_IncompleteThreeByteCharacter_TrimsPartialBytes(int includedBytes)
    {
        var prefix = Encoding.ASCII.GetBytes("A");
        var character = Encoding.UTF8.GetBytes("中");
        var bytes = prefix.Concat(character[..includedBytes]).ToArray();

        Assert.Equal(prefix.Length, TextEncodingDetector.Utf8SafeLength(bytes));
    }

    [Fact]
    public void Utf8SafeLength_AsciiAndEmpty_ReturnFullLength()
    {
        Assert.Equal(3, TextEncodingDetector.Utf8SafeLength("abc"u8));
        Assert.Equal(0, TextEncodingDetector.Utf8SafeLength(ReadOnlySpan<byte>.Empty));
    }
}
