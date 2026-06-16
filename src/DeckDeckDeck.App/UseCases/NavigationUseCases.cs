using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class ResolveCategoryHotkeyUseCase
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISettingsRepository _settingsStore;

    public ResolveCategoryHotkeyUseCase(
        ICategoryRepository categoryRepository,
        ISettingsRepository settingsStore)
    {
        _categoryRepository = categoryRepository;
        _settingsStore = settingsStore;
    }

    public CategoryHotkeyResolution Execute(SlotKey slotKey)
    {
        if (slotKey == SlotKey.Numpad0)
        {
            return CategoryHotkeyResolution.Unsupported(
                "카테고리 바로 열기 단축키는 Ctrl+Numpad 1~9와 기호를 지원합니다.");
        }

        var settings = _settingsStore.Load();
        if (settings.EnabledCategorySlotKeys.TryGetValue(slotKey, out var isEnabled) && !isEnabled)
        {
            return CategoryHotkeyResolution.Blocked(
                $"슬롯 {slotKey.GetDisplayText()}은 사용 안 함 상태입니다.");
        }

        var category = _categoryRepository.GetBySlotKey(slotKey);
        return category is null
            ? CategoryHotkeyResolution.CreateNew(slotKey)
            : CategoryHotkeyResolution.OpenExisting(category);
    }
}

public sealed class GetCategoryByIdUseCase
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoryByIdUseCase(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public Category? Execute(Guid categoryId)
    {
        return _categoryRepository.GetById(categoryId);
    }
}

public enum CategoryHotkeyResolutionKind
{
    OpenExisting,
    CreateNew,
    Blocked,
    Unsupported
}

public sealed record CategoryHotkeyResolution(
    CategoryHotkeyResolutionKind Kind,
    SlotKey SlotKey,
    Category? Category = null,
    string? StatusMessage = null)
{
    public static CategoryHotkeyResolution OpenExisting(Category category)
    {
        return new CategoryHotkeyResolution(
            CategoryHotkeyResolutionKind.OpenExisting,
            category.SlotKey,
            Category: category);
    }

    public static CategoryHotkeyResolution CreateNew(SlotKey slotKey)
    {
        return new CategoryHotkeyResolution(CategoryHotkeyResolutionKind.CreateNew, slotKey);
    }

    public static CategoryHotkeyResolution Blocked(string statusMessage)
    {
        return new CategoryHotkeyResolution(
            CategoryHotkeyResolutionKind.Blocked,
            SlotKey.Numpad0,
            StatusMessage: statusMessage);
    }

    public static CategoryHotkeyResolution Unsupported(string statusMessage)
    {
        return new CategoryHotkeyResolution(
            CategoryHotkeyResolutionKind.Unsupported,
            SlotKey.Numpad0,
            StatusMessage: statusMessage);
    }
}

