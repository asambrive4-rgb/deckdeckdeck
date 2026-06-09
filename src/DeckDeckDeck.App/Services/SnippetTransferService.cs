using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class SnippetTransferService
{
    private readonly LoggingService? _loggingService;
    private readonly SettingsService _settingsService;
    private readonly SnippetService _snippetService;
    private readonly ThumbnailService? _thumbnailService;

    public SnippetTransferService(
        SnippetService snippetService,
        SettingsService settingsService,
        ThumbnailService? thumbnailService,
        LoggingService? loggingService = null)
    {
        _snippetService = snippetService;
        _settingsService = settingsService;
        _thumbnailService = thumbnailService;
        _loggingService = loggingService;
    }

    public Snippet CopySnippet(
        Guid sourceSnippetId,
        SlotKey targetSlotKey,
        bool sourceSlotEnabled)
    {
        var createdImageFiles = new List<ImageFileSet>();
        SnippetTransferResult result;

        try
        {
            result = _snippetService.CopyToSlot(
                sourceSnippetId,
                targetSlotKey,
                imageFiles => CopyImageFiles(imageFiles, createdImageFiles));
        }
        catch (Exception ex)
        {
            DeleteImageFiles(createdImageFiles);
            _loggingService?.Log("Snippet copy failed.", ex);
            throw;
        }

        try
        {
            _settingsService.SetSnippetSlotEnabled(targetSlotKey, sourceSlotEnabled);

            return result.Snippet;
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Snippet copy slot settings failed.", ex);
            throw;
        }
        finally
        {
            DeleteImageFiles(result.OverwrittenImageFiles);
        }
    }

    public Snippet MoveSnippet(
        Guid sourceSnippetId,
        SlotKey sourceSlotKey,
        SlotKey targetSlotKey,
        bool sourceSlotEnabled)
    {
        var result = _snippetService.MoveToSlot(sourceSnippetId, targetSlotKey);

        try
        {
            _settingsService.SetSnippetSlotEnabled(targetSlotKey, sourceSlotEnabled);
            _settingsService.SetSnippetSlotEnabled(sourceSlotKey, true);

            return result.Snippet;
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Snippet move slot settings failed.", ex);
            throw;
        }
        finally
        {
            DeleteImageFiles(result.OverwrittenImageFiles);
        }
    }

    private ImageFileSet CopyImageFiles(ImageFileSet imageFiles, List<ImageFileSet> createdImageFiles)
    {
        if (string.IsNullOrWhiteSpace(imageFiles.ImagePath))
        {
            return new ImageFileSet(null, null);
        }

        if (_thumbnailService is null)
        {
            throw new InvalidOperationException("이미지 저장소가 준비되지 않았습니다.");
        }

        var storedImage = _thumbnailService.StoreImage(imageFiles.ImagePath);
        var copiedImageFiles = new ImageFileSet(storedImage.ImagePath, storedImage.ThumbnailPath);
        createdImageFiles.Add(copiedImageFiles);

        return copiedImageFiles;
    }

    private void DeleteImageFiles(IEnumerable<ImageFileSet> imageFiles)
    {
        if (_thumbnailService is null)
        {
            return;
        }

        foreach (var imageFileSet in imageFiles)
        {
            _thumbnailService.DeleteImageFiles(imageFileSet);
        }
    }
}
