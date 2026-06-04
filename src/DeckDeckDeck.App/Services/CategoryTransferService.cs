using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class CategoryTransferService
{
    private readonly CategoryService _categoryService;
    private readonly LoggingService? _loggingService;
    private readonly SettingsService _settingsService;
    private readonly ThumbnailService? _thumbnailService;

    public CategoryTransferService(
        CategoryService categoryService,
        SettingsService settingsService,
        ThumbnailService? thumbnailService,
        LoggingService? loggingService = null)
    {
        _categoryService = categoryService;
        _settingsService = settingsService;
        _thumbnailService = thumbnailService;
        _loggingService = loggingService;
    }

    public Category CopyCategory(
        Guid sourceCategoryId,
        SlotKey targetSlotKey,
        bool sourceSlotEnabled)
    {
        var createdImageFiles = new List<ImageFileSet>();
        CategoryTransferResult result;

        try
        {
            result = _categoryService.CopyToSlot(
                sourceCategoryId,
                targetSlotKey,
                imageFiles => CopyImageFiles(imageFiles, createdImageFiles));
        }
        catch (Exception ex)
        {
            DeleteImageFiles(createdImageFiles);
            _loggingService?.Log("Category copy failed.", ex);
            throw;
        }

        try
        {
            _settingsService.SetCategorySlotEnabled(targetSlotKey, sourceSlotEnabled);

            return result.Category;
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Category copy slot settings failed.", ex);
            throw;
        }
        finally
        {
            DeleteImageFiles(result.OverwrittenImageFiles);
        }
    }

    public Category MoveCategory(
        Guid sourceCategoryId,
        SlotKey sourceSlotKey,
        SlotKey targetSlotKey,
        bool sourceSlotEnabled)
    {
        var result = _categoryService.MoveToSlot(sourceCategoryId, targetSlotKey);

        try
        {
            _settingsService.SetCategorySlotEnabled(targetSlotKey, sourceSlotEnabled);
            _settingsService.SetCategorySlotEnabled(sourceSlotKey, true);

            return result.Category;
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Category move slot settings failed.", ex);
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
