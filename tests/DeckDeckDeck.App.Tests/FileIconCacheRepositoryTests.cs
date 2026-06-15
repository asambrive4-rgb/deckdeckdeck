using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class FileIconCacheRepositoryTests
{
    [Fact]
    public void UnsupportedFileReturnsNullWithoutExtraction()
    {
        var storage = CreateStorage();
        var extractor = new StubFileIconExtractor();
        var service = new FileIconCacheRepository(storage, extractor);
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
        var service = new FileIconCacheRepository(storage, extractor);
        var exePath = CreateLaunchFile(storage, "tool.exe", "one");

        var first = service.GetOrCreateIcon(exePath, null);
        var second = service.GetOrCreateIcon(exePath, first);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.StartsWith("icon-cache/", first.IconPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(storage.ToAbsolutePath(first.IconPath)));
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public void SourceChangeCreatesNewCacheEntry()
    {
        var storage = CreateStorage();
        var extractor = new StubFileIconExtractor();
        var service = new FileIconCacheRepository(storage, extractor);
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
        var service = new FileIconCacheRepository(storage, extractor);
        var shortcutPath = CreateLaunchFile(storage, "broken.lnk", "shortcut");

        var result = service.GetOrCreateIcon(shortcutPath, null);

        Assert.Null(result);
        Assert.Equal(1, extractor.CallCount);
    }

    private static AppStoragePaths CreateStorage()
    {
        var storage = new AppStoragePaths(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        storage.EnsureCreated();

        return storage;
    }

    private static string CreateLaunchFile(AppStoragePaths storage, string fileName, string content)
    {
        var path = Path.Combine(storage.TempPath, fileName);
        File.WriteAllText(path, content);

        return path;
    }
}
