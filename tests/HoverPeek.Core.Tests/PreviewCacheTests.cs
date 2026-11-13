using HoverPeek.Core.Imaging;
using HoverPeek.Core.Preview;

namespace HoverPeek.Core.Tests;

public sealed class PreviewCacheTests
{
    [Fact]
    public void SetThenGet_ReturnsSameInstance()
    {
        var cache = new PreviewCache();
        var key = Key("A");
        var value = Result();

        cache.Set(key, value);

        Assert.True(cache.TryGet(key, out var cached));
        Assert.Same(value, cached);
    }

    [Fact]
    public void Set_WhenCapacityExceeded_EvictsLeastRecentlyUsedEntry()
    {
        var cache = new PreviewCache(maxEntries: 2);
        var a = Key("A");
        var b = Key("B");
        var c = Key("C");
        cache.Set(a, Result());
        cache.Set(b, Result());
        Assert.True(cache.TryGet(a, out _));

        cache.Set(c, Result());

        Assert.True(cache.TryGet(a, out _));
        Assert.False(cache.TryGet(b, out _));
        Assert.True(cache.TryGet(c, out _));
    }

    [Fact]
    public void Set_WhenByteLimitExceeded_EvictsLeastRecentlyUsedEntry()
    {
        var cache = new PreviewCache(maxEntries: 10, maxBytes: 2_500);
        var a = Key("A");
        var b = Key("B");
        cache.Set(a, ImageResult(500));

        cache.Set(b, ImageResult(500));

        Assert.False(cache.TryGet(a, out _));
        Assert.True(cache.TryGet(b, out _));
        Assert.Equal(1, cache.Count);
    }

    [Theory]
    [InlineData(PreviewKind.Error, false)]
    [InlineData(PreviewKind.Text, true)]
    public void Set_ErrorOrPlaceholder_DoesNotStore(PreviewKind kind, bool placeholder)
    {
        var cache = new PreviewCache();
        var key = Key("A");

        cache.Set(key, new PreviewResult { Kind = kind, IsPlaceholder = placeholder });

        Assert.False(cache.TryGet(key, out _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Set_ItemLargerThanByteLimit_DoesNotStoreOrEvictExistingEntry()
    {
        var cache = new PreviewCache(maxEntries: 10, maxBytes: 2_000);
        var small = Key("small");
        var large = Key("large");
        cache.Set(small, ImageResult(10));

        cache.Set(large, ImageResult(1_000));

        Assert.True(cache.TryGet(small, out _));
        Assert.False(cache.TryGet(large, out _));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void KeyFor_NonexistentPath_ReturnsNull()
    {
        using var temp = new TempDirectory();

        Assert.Null(PreviewCache.KeyFor(new PreviewRequest { Path = temp.File("missing.txt") }));
    }

    [Fact]
    public void KeyFor_RealFile_ChangesWithLengthAndImageLimit()
    {
        using var temp = new TempDirectory();
        var path = temp.File("data.txt");
        File.WriteAllText(path, "a");
        var first = PreviewCache.KeyFor(new PreviewRequest { Path = path, MaxImagePixels = 800 });
        File.WriteAllText(path, "different length");
        var rewritten = PreviewCache.KeyFor(new PreviewRequest { Path = path, MaxImagePixels = 800 });
        var differentLimit = PreviewCache.KeyFor(new PreviewRequest { Path = path, MaxImagePixels = 1200 });

        Assert.NotNull(first);
        Assert.NotNull(rewritten);
        Assert.NotNull(differentLimit);
        Assert.NotEqual(first, rewritten);
        Assert.NotEqual(rewritten, differentLimit);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Constructor_NonPositiveLimit_Throws(int maxEntries, long maxBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreviewCache(maxEntries, maxBytes));
    }

    private static PreviewCache.Key Key(string path) =>
        new(path, 1, DateTime.UnixEpoch, 800, true, 1024, 100, true);

    private static PreviewResult Result() => new() { Kind = PreviewKind.Text };

    private static PreviewResult ImageResult(int bytes) => new()
    {
        Kind = PreviewKind.Image,
        Image = new DecodedImage
        {
            Width = 1,
            Height = 1,
            Stride = 4,
            Pixels = new byte[bytes],
        },
    };
}
