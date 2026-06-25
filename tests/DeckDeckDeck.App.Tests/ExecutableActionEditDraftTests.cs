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

        var terminalData = CreateSaveData(draft).NormalizeForStorage();

        draft.SetActionType(SnippetActionType.LaunchUrl);
        draft.SetLaunchUrl("https://example.com");
        var urlData = CreateSaveData(draft).NormalizeForStorage();

        Assert.True(terminalData.RunAsAdministrator);
        Assert.False(urlData.RunAsAdministrator);
    }

    private static SnippetSaveData CreateSaveData(ExecutableActionEditDraft draft)
    {
        return new SnippetSaveData(
            "Title",
            "Content",
            "Description",
            draft.ImagePath,
            draft.ThumbnailPath,
            draft.ActionType,
            draft.LaunchPath,
            draft.SlotImageMode,
            draft.AutoIcon,
            draft.LaunchUrl,
            draft.MediaProvider,
            draft.MediaCommand,
            draft.PasteShortcutMode,
            draft.TerminalCommand,
            draft.TerminalShell,
            draft.RunAsAdministrator,
            draft.FileActionMode);
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
