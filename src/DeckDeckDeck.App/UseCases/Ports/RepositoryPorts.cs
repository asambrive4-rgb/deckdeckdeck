using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.UseCases.Ports;

public interface ICategoryRepository
{
    IReadOnlyList<Category> GetAll();

    Category? GetById(Guid id);

    Category? GetBySlotKey(SlotKey slotKey);

    Category Create(
        SlotKey slotKey,
        string name,
        string? description,
        string? imagePath = null,
        string? thumbnailPath = null);

    Category Update(
        Guid id,
        string name,
        string? description,
        string? imagePath,
        string? thumbnailPath);

    IReadOnlyList<ImageFileReference> Delete(Guid id);

    CategoryTransferRepositoryResult CopyToSlot(
        Guid sourceId,
        SlotKey targetSlotKey,
        Func<ImageFileReference, ImageFileReference> copyImageFiles);

    CategoryTransferRepositoryResult MoveToSlot(Guid sourceId, SlotKey targetSlotKey);
}

public interface ISnippetRepository
{
    IReadOnlyList<Snippet> GetByCategoryId(Guid categoryId);

    Snippet? GetById(Guid id);

    Snippet Create(Guid categoryId, SlotKey slotKey, SnippetSaveData data);

    Snippet Update(Guid id, SnippetSaveData data);

    ImageFileReference Delete(Guid id);

    SnippetTransferRepositoryResult CopyToSlot(
        Guid sourceId,
        SlotKey targetSlotKey,
        Func<ImageFileReference, ImageFileReference> copyImageFiles);

    SnippetTransferRepositoryResult MoveToSlot(Guid sourceId, SlotKey targetSlotKey);
}

public interface IHotkeyActionRepository
{
    IReadOnlyList<HotkeyAction> GetAll();

    HotkeyAction? GetById(Guid id);

    HotkeyAction Create(HotkeyActionSaveData data);

    HotkeyAction Update(Guid id, HotkeyActionSaveData data);

    HotkeyAction SetEnabled(Guid id, bool isEnabled);

    ImageFileReference Delete(Guid id);
}

public interface ISettingsRepository
{
    AppSettings Load();

    void EnsureDefaults();

    void Save(AppSettings settings);

    void SaveWindowPlacement(double left, double top, string screenDeviceName);

    void SetCategorySlotEnabled(SlotKey slotKey, bool enabled);

    void SetSnippetSlotEnabled(SlotKey slotKey, bool enabled);
}
