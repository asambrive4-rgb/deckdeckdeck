using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Tests;

public sealed class ViewModelBoundaryTests
{
    [Fact]
    public void PrepareSnippetActionUseCaseHidesBeforePasteOnlyWhenAutoHideIsEnabled()
    {
        var useCase = new PrepareSnippetActionUseCase();
        var snippet = new Snippet { ActionType = SnippetActionType.PasteText };
        var settings = new AppSettings { AutoHideAfterPaste = true };

        var result = useCase.Execute(new PrepareSnippetActionRequest(snippet, settings));

        Assert.True(result.ShouldHideBeforeExecute);
    }

    [Fact]
    public void PrepareSnippetActionUseCaseDoesNotHideBeforeLaunchActions()
    {
        var useCase = new PrepareSnippetActionUseCase();
        var snippet = new Snippet { ActionType = SnippetActionType.LaunchUrl };
        var settings = new AppSettings { AutoHideAfterPaste = true };

        var result = useCase.Execute(new PrepareSnippetActionRequest(snippet, settings));

        Assert.False(result.ShouldHideBeforeExecute);
    }

    [Fact]
    public void PrepareSnippetActionUseCaseHidesBeforeFilePasteWhenAutoHideIsEnabled()
    {
        var useCase = new PrepareSnippetActionUseCase();
        var snippet = new Snippet
        {
            ActionType = SnippetActionType.LaunchFile,
            FileActionMode = FileActionMode.Paste
        };
        var settings = new AppSettings { AutoHideAfterPaste = true };

        var result = useCase.Execute(new PrepareSnippetActionRequest(snippet, settings));

        Assert.True(result.ShouldHideBeforeExecute);
    }

    [Fact]
    public void SaveWindowPlacementUseCaseSavesPlacementThroughSettingsPort()
    {
        var settingsRepository = new RecordingSettingsRepository();
        var useCase = new SaveWindowPlacementUseCase(settingsRepository);

        useCase.Execute(new SaveWindowPlacementRequest(10, 20, "Monitor1"));

        Assert.Equal(10, settingsRepository.LastWindowLeft);
        Assert.Equal(20, settingsRepository.LastWindowTop);
        Assert.Equal("Monitor1", settingsRepository.LastWindowScreenDeviceName);
    }

    [Fact]
    public void SaveWindowPlacementUseCaseLogsAndSwallowsSaveFailure()
    {
        var settingsRepository = new RecordingSettingsRepository { ThrowOnSaveWindowPlacement = true };
        var logger = new RecordingLogger();
        var useCase = new SaveWindowPlacementUseCase(settingsRepository, logger);

        useCase.Execute(new SaveWindowPlacementRequest(10, 20, "Monitor1"));

        Assert.Equal("Window placement save failed.", logger.Message);
        Assert.IsType<InvalidOperationException>(logger.Exception);
    }

    [Fact]
    public void HomeViewModelBuildsSlotsFromUseCaseStateWithoutRepository()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            SlotKey = SlotKey.Numpad1,
            Name = "Writing"
        };
        var selectedCategory = default(Category);
        var viewModel = new HomeViewModel(
            new HomeGridState([category], new AppSettings()),
            new SlotGridViewModelFactory(),
            opened => selectedCategory = opened,
            _ => { },
            _ => { },
            () => { },
            () => { });

        viewModel.SelectSlot(SlotKey.Numpad1);

        Assert.Equal(category.Id, selectedCategory?.Id);
    }

    [Fact]
    public void CategoryViewModelBuildsSlotsFromUseCaseStateWithoutRepository()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            SlotKey = SlotKey.Numpad1,
            Name = "Writing"
        };
        var snippet = new Snippet
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            SlotKey = SlotKey.Numpad3,
            Title = "Paste",
            Content = "Hello"
        };
        var executedSnippet = default(Snippet);
        var viewModel = new CategoryViewModel(
            category,
            new CategoryGridState([snippet], new AppSettings()),
            new SlotGridViewModelFactory(),
            () => { },
            () => { },
            (_, _, _) => { },
            pasted =>
            {
                executedSnippet = pasted;
                return Task.CompletedTask;
            });

        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.Equal(snippet.Id, executedSnippet?.Id);
    }

    [Fact]
    public void EditableImageDraftDeletesCurrentUnsavedImageOnCancel()
    {
        var imageRepository = new RecordingImageFileRepository();
        var draft = new EditableImageDraft(null, null, imageRepository);

        draft.ReplaceWithStoredImage("source.png");
        draft.DeleteCurrentUnsavedImage();

        var deleted = Assert.Single(imageRepository.DeletedImages);
        Assert.Equal("stored-source.png", deleted.ImagePath);
        Assert.Equal("stored-source-thumb.png", deleted.ThumbnailPath);
    }

    [Fact]
    public void EditableImageDraftDeletesOriginalImageAfterSuccessfulReplacement()
    {
        var imageRepository = new RecordingImageFileRepository();
        var draft = new EditableImageDraft("original.png", "original-thumb.png", imageRepository);

        draft.ReplaceWithStoredImage("source.png");
        draft.DeleteOriginalImageIfReplaced();
        draft.MarkCurrentAsOriginal();
        draft.DeleteCurrentUnsavedImage();

        var deleted = Assert.Single(imageRepository.DeletedImages);
        Assert.Equal("original.png", deleted.ImagePath);
        Assert.Equal("original-thumb.png", deleted.ThumbnailPath);
    }

    private sealed class RecordingSettingsRepository : ISettingsRepository
    {
        public bool ThrowOnSaveWindowPlacement { get; init; }

        public double LastWindowLeft { get; private set; }

        public double LastWindowTop { get; private set; }

        public string? LastWindowScreenDeviceName { get; private set; }

        public AppSettings Load()
        {
            return new AppSettings();
        }

        public void EnsureDefaults()
        {
        }

        public void Save(AppSettings settings)
        {
        }

        public void SaveWindowPlacement(double left, double top, string screenDeviceName)
        {
            if (ThrowOnSaveWindowPlacement)
            {
                throw new InvalidOperationException("save failed");
            }

            LastWindowLeft = left;
            LastWindowTop = top;
            LastWindowScreenDeviceName = screenDeviceName;
        }

        public void SetCategorySlotEnabled(SlotKey slotKey, bool enabled)
        {
        }

        public void SetSnippetSlotEnabled(SlotKey slotKey, bool enabled)
        {
        }
    }

    private sealed class RecordingLogger : IAppLogger
    {
        public string? Message { get; private set; }

        public Exception? Exception { get; private set; }

        public void Log(string message, Exception? exception = null)
        {
            Message = message;
            Exception = exception;
        }
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
}
