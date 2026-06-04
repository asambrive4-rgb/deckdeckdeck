using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.Tests;

public sealed class FileIconCacheServiceTests
{
    [Fact]
    public void UnsupportedFileReturnsNullWithoutExtraction()
    {
        var storage = CreateStorage();
        var extractor = new StubFileIconExtractor();
        var service = new FileIconCacheService(storage, extractor);
        var textPath = Path.Combine(storage.TempPath, "notes.txt");
        File.WriteAllText(textPath, "notes");

        var result = service.GetOrCreateIcon(textPath, null);

        Assert.Null(result);
        Assert.Equal(0, extractor.CallCount);
    }

    [Fact]
    public void CacheHitReusesExistingIcon()
    {
        var storage = CreateStorage();
        var extractor = new StubFileIconExtractor();
        var service = new FileIconCacheService(storage, extractor);
        var exePath = CreateLaunchFile(storage, "tool.exe", "one");

        var first = service.GetOrCreateIcon(exePath, null);
        var second = service.GetOrCreateIcon(exePath, first);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public void SourceChangeCreatesNewCacheEntry()
    {
        var storage = CreateStorage();
        var extractor = new StubFileIconExtractor();
        var service = new FileIconCacheService(storage, extractor);
        var exePath = CreateLaunchFile(storage, "tool.exe", "one");
        var first = service.GetOrCreateIcon(exePath, null);

        File.WriteAllText(exePath, "changed content");
        var second = service.GetOrCreateIcon(exePath, first);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.IconPath, second.IconPath);
        Assert.Equal(2, extractor.CallCount);
    }

    [Fact]
    public void ExtractionFailureReturnsNull()
    {
        var storage = CreateStorage();
        var extractor = new StubFileIconExtractor { ShouldSucceed = false };
        var service = new FileIconCacheService(storage, extractor);
        var shortcutPath = CreateLaunchFile(storage, "broken.lnk", "shortcut");

        var result = service.GetOrCreateIcon(shortcutPath, null);

        Assert.Null(result);
        Assert.Equal(1, extractor.CallCount);
    }

    private static FileStorageService CreateStorage()
    {
        var storage = new FileStorageService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        storage.EnsureCreated();

        return storage;
    }

    private static string CreateLaunchFile(FileStorageService storage, string fileName, string content)
    {
        var path = Path.Combine(storage.TempPath, fileName);
        File.WriteAllText(path, content);

        return path;
    }
}
