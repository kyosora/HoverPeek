using System.Text;
using HoverPeek.Core.Preview;

namespace HoverPeek.Core.Tests;

public sealed class TextPreviewProviderTests
{
    [Fact]
    public void LimitLines_ContentBeyondLimit_TruncatesAtLineBoundary()
    {
        Assert.Equal(("a\nb", true), TextPreviewProvider.LimitLines("a\nb\nc", 2));
    }

    [Theory]
    [InlineData("a\nb\n")]
    [InlineData("a\nb")]
    public void LimitLines_ContentAtLimit_IsNotTruncated(string text)
    {
        Assert.Equal((text, false), TextPreviewProvider.LimitLines(text, 2));
    }

    [Fact]
    public void LimitLines_ZeroLimit_ReturnsInputUnchanged()
    {
        Assert.Equal(("a\nb\nc", false), TextPreviewProvider.LimitLines("a\nb\nc", 0));
    }

    [Fact]
    public void LooksBinary_NulInFirstEightKilobytes_ReturnsTrue()
    {
        var bytes = Enumerable.Repeat((byte)'a', 9000).ToArray();
        bytes[8191] = 0;

        Assert.True(TextPreviewProvider.LooksBinary(bytes));
    }

    [Fact]
    public void LooksBinary_PlainText_ReturnsFalse()
    {
        Assert.False(TextPreviewProvider.LooksBinary("plain text"u8));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x2D, 0x4E, 0x00, 0x00 })]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x4E, 0x2D, 0x00, 0x00 })]
    public void LooksBinary_Utf16BomWithZeros_ReturnsFalse(byte[] bytes)
    {
        Assert.False(TextPreviewProvider.LooksBinary(bytes));
    }

    [Fact]
    public async Task GeneratePreviewAsync_FileOverByteLimit_ReportsTruncationAndRealSize()
    {
        using var temp = new TempDirectory();
        var path = temp.File("large.txt");
        await File.WriteAllTextAsync(path, new string('a', 2048), Encoding.UTF8);
        var provider = new TextPreviewProvider();

        var result = await provider.GeneratePreviewAsync(new PreviewRequest
        {
            Path = path,
            TextMaxBytes = 1024,
        });

        Assert.True(result.IsTruncated);
        Assert.Equal(new FileInfo(path).Length, result.FileSize);
        Assert.Equal(PreviewKind.Text, result.Kind);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task GeneratePreviewAsync_Markdown_RespectsRenderMarkdown(bool renderMarkdown, bool expectedMarkdown)
    {
        using var temp = new TempDirectory();
        var path = temp.File("readme.md");
        await File.WriteAllTextAsync(path, "# Heading\n\nBody", Encoding.UTF8);
        var provider = new TextPreviewProvider();

        var result = await provider.GeneratePreviewAsync(new PreviewRequest
        {
            Path = path,
            RenderMarkdown = renderMarkdown,
        });

        Assert.Equal(expectedMarkdown, result.IsMarkdown);
        if (renderMarkdown)
            Assert.Contains("<h1", result.MarkdownHtml);
        else
            Assert.Null(result.MarkdownHtml);
    }
}
