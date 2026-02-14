namespace HoverPeek.Core.Preview;

public enum PreviewKind
{
    Image,
    Archive,
    Video,
    Text,
    Unsupported
}

public sealed record PreviewResult
{
    public required PreviewKind Kind { get; init; }

    public byte[]? ImageData { get; init; }
    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }
    public string? ImageFormat { get; init; }

    public IReadOnlyList<ArchiveEntry>? Entries { get; init; }

    public string? VideoFilePath { get; init; }

    public string? TextContent { get; init; }
    public string? TextEncoding { get; init; }
    public string? FileExtension { get; init; }
    public bool IsMarkdown { get; init; }
    public string? MarkdownHtml { get; init; }
}

public sealed record ArchiveEntry
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public required long Size { get; init; }
    public required bool IsDirectory { get; init; }
    public required bool IsImage { get; init; }
    public DateTime? LastModified { get; init; }
}

public interface IPreviewProvider
{
    bool CanHandle(string filePath);
    Task<PreviewResult> GeneratePreviewAsync(
        string filePath, CancellationToken ct = default);
}
