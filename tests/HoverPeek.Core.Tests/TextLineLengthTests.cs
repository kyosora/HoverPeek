using HoverPeek.Core.Preview;

namespace HoverPeek.Core.Tests;

public sealed class TextLineLengthTests
{
    [Fact]
    public void LimitLineLength_ShortLines_ReturnsSameInstance()
    {
        const string text = "a\nbb\nccc";
        var (result, truncated) = TextPreviewProvider.LimitLineLength(text, 5);

        Assert.Same(text, result);
        Assert.False(truncated);
    }

    [Fact]
    public void LimitLineLength_LongLine_IsCutWithMarker()
    {
        var text = "short\n" + new string('x', 50) + "\nend";
        var (result, truncated) = TextPreviewProvider.LimitLineLength(text, 10);

        Assert.True(truncated);
        Assert.Equal("short\n" + new string('x', 10) + " …\nend", result);
    }

    [Fact]
    public async Task Generate_MinifiedJsonSingleLine_IsCappedSoUiCanRenderIt()
    {
        // 2026-11-14 回歸：D:\db.text.json 前 1MB 整段只有一行，AvalonEdit 排版卡死 UI 數十秒
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "minified.json");
        File.WriteAllText(path, "[" + string.Join(",", Enumerable.Range(0, 200_000).Select(i => $"{{\"id\":{i}}}")) + "]");

        var result = await new TextPreviewProvider().GeneratePreviewAsync(new PreviewRequest { Path = path });

        Assert.True(result.IsTruncated);
        var longest = result.TextContent!.Split('\n').Max(l => l.Length);
        Assert.True(longest <= TextPreviewProvider.MaxLineChars + 2, $"最長一行 {longest} 字元，超過上限");
    }

    [Fact]
    public async Task Generate_Markdown_LongLinesAreNotCut()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "long.md");
        var paragraph = string.Join(" ", Enumerable.Repeat("word", 1000)); // 約 5000 字元的一段
        File.WriteAllText(path, "# Title\n\n" + paragraph);

        var result = await new TextPreviewProvider().GeneratePreviewAsync(new PreviewRequest { Path = path, RenderMarkdown = true });

        Assert.True(result.IsMarkdown);
        Assert.Contains(paragraph, result.TextContent);
        Assert.False(result.IsTruncated);
    }
}
