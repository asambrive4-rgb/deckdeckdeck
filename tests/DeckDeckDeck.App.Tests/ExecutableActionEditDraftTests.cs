using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class ExecutableActionEditDraftTests
{
    [Fact]
    public void DeleteCurrentUnsavedImageDeletesReplacementOnCancel()
    {
        var imageRepository = new RecordingImageFileRepository();
        var draft = ExecutableActionEditDraft.FromSnippet(null, imageRepository);

        draft.ReplaceImageFromPath("source.png");
        draft.DeleteCurrentUnsavedImage();

        var deleted = Assert.Single(imageRepository.DeletedImages);
        Assert.Equal("stored-source.png", deleted.ImagePath);
        Assert.Equal("stored-source-thumb.png", deleted.ThumbnailPath);
    }

    [Fact]
    public void MarkSavedDeletesOriginalImageAfterReplacement()
    {
        var imageRepository = new RecordingImageFileRepository();
        var draft = ExecutableActionEditDraft.FromSnippet(
            new Snippet
            {
                ImagePath = "original.png",
                ThumbnailPath = "original-thumb.png",
                SlotImageMode = SlotImageMode.Custom
            },
            imageRepository);

        draft.ReplaceImageFromPath("source.png");
        draft.MarkSaved();
        draft.DeleteCurrentUnsavedImage();

        var deleted = Assert.Single(imageRepository.DeletedImages);
        Assert.Equal("original.png", deleted.ImagePath);
        Assert.Equal("original-thumb.png", deleted.ThumbnailPath);
    }

    [Fact]
    public void PrepareAutoIconUsesResolverAndPreviewPathResolver()
    {
        var imageResolver = new RecordingSnippetImageResolver();
        var pathResolver = new PrefixPathResolver("display:");
        var draft = ExecutableActionEditDraft.FromSnippet(
            null,
            imageFileRepository: null,
            imageResolver,
            pathResolver);

        draft.SetActionType(SnippetActionType.LaunchFile);
        draft.SetLaunchPath(@"C:\tools\app.exe");
        var autoIcon = draft.PrepareAutoIconForSave();

        Assert.NotNull(autoIcon);
        Assert.Equal("auto-icon.png", autoIcon.IconPath);
        Assert.Equal("display:auto-icon.png", draft.GetPreviewThumbnailPath());
        Assert.Equal(SnippetActionType.LaunchFile, imageResolver.LastActionType);
        Assert.Equal(@"C:\tools\app.exe", imageResolver.LastLaunchPath);
    }

    [Fact]
    public void SetMediaProviderCorrectsInvalidCommandForProvider()
    {
        var draft = ExecutableActionEditDraft.FromSnippet(null, imageFileRepository: null);

        draft.SetActionType(SnippetActionType.MediaAction);
        draft.SetMediaProvider(SnippetMediaProvider.Spotify);
        draft.SetMediaCommand(SnippetMediaCommand.ToggleShuffle);
        draft.SetMediaProvider(SnippetMediaProvider.System);

        Assert.Equal(SnippetMediaProvider.System, draft.MediaProvider);
        Assert.Equal(SnippetMediaCommand.PlayPause, draft.MediaCommand);
        Assert.Equal(
            MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.PlayPause),
            draft.GetPreviewThumbnailPath());
    }

    [Fact]
    public void NormalizeForStorageKeepsAdministratorOnlyForTerminalCommand()
    {
        var draft = ExecutableActionEditDraft.FromSnippet(null, imageFileRepository: null);
        draft.SetActionType(SnippetActionType.TerminalCommand);
        draft.TerminalCommand = "echo hello";
        draft.RunAsAdministrator = true;

        var terminalData = draft.ToSnippetSaveData().NormalizeForStorage();

        draft.SetActionType(SnippetActionType.LaunchUrl);
        draft.SetLaunchUrl("https://example.com");
        var urlData = draft.ToSnippetSaveData().NormalizeForStorage();

        Assert.True(terminalData.RunAsAdministrator);
        Assert.False(urlData.RunAsAdministrator);
    }

    [Fact]
    public void ToSnippetSaveDataUsesCurrentDraftValues()
    {
        var draft = ExecutableActionEditDraft.FromSnippet(null, imageFileRepository: null);
        draft.Title = "Docs";
        draft.Content = "Hello";
        draft.Description = "Greeting";
        draft.SetActionType(SnippetActionType.LaunchUrl);
        draft.SetLaunchUrl("example.com");
        draft.ApplyNormalizedLaunchUrl("https://example.com");

        var data = draft.ToSnippetSaveData();

        Assert.Equal("Docs", data.Title);
        Assert.Equal("Hello", data.Content);
        Assert.Equal("Greeting", data.Description);
        Assert.Equal(SnippetActionType.LaunchUrl, data.ActionType);
        Assert.Equal("https://example.com", data.LaunchUrl);
    }

    [Fact]
    public void ToHotkeyActionSaveDataAddsGestureAndEnabledState()
    {
        var draft = ExecutableActionEditDraft.FromHotkeyAction(null, imageFileRepository: null);
        var gesture = new HotkeyGesture(0x67, HotkeyModifiers.None);
        draft.Title = "Paste";
        draft.Content = "Hello";

        var data = draft.ToHotkeyActionSaveData(gesture, isEnabled: false);

        Assert.Equal("Paste", data.Title);
        Assert.Equal("Hello", data.Content);
        Assert.Equal(gesture, data.Gesture);
        Assert.False(data.IsEnabled);
    }

    private sealed class RecordingImageFileRepository : IImageFileRepository
    {
        public List<ImageFileReference> DeletedImages { get; } = [];

        public StoredImageReference StoreImage(string sourcePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            return new StoredImageReference(
                $"stored-{fileName}.png",
                $"stored-{fileName}-thumb.png");
        }

        public void DeleteImageFiles(ImageFileReference imageFiles)
        {
            DeletedImages.Add(imageFiles);
        }
    }

    private sealed class RecordingSnippetImageResolver : ISnippetImageResolver
    {
        public SnippetActionType? LastActionType { get; private set; }

        public string? LastLaunchPath { get; private set; }

        public string? GetDisplayImagePath(Snippet? snippet)
        {
            return snippet?.ThumbnailPath;
        }

        public AutoIconCacheEntry? PrepareAutoIcon(
            SnippetActionType actionType,
            string? launchPath,
            AutoIconCacheEntry? current)
        {
            LastActionType = actionType;
            LastLaunchPath = launchPath;
            return new AutoIconCacheEntry(
                "auto-icon.png",
                launchPath ?? string.Empty,
                new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc),
                123);
        }
    }

    private sealed class PrefixPathResolver : IStoredImagePathResolver
    {
        private readonly string _prefix;

        public PrefixPathResolver(string prefix)
        {
            _prefix = prefix;
        }

        public string? ResolveDisplayPath(string? storedPath)
        {
            return storedPath is null ? null : _prefix + storedPath;
        }

        public bool FileExists(string? storedPath)
        {
            return !string.IsNullOrWhiteSpace(storedPath);
        }
    }
}
