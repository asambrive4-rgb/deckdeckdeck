using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class LoadCategoryEditorStateUseCase
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISettingsRepository _settingsStore;

    public LoadCategoryEditorStateUseCase(
        ICategoryRepository categoryRepository,
        ISettingsRepository settingsStore)
    {
        _categoryRepository = categoryRepository;
        _settingsStore = settingsStore;
    }

    public CategoryEditorState Execute(LoadCategoryEditorStateRequest request)
    {
        var settings = _settingsStore.Load();
        var isSlotEnabled = !settings.EnabledCategorySlotKeys.TryGetValue(request.SlotKey, out var enabled)
            || enabled;
        var categoriesBySlot = _categoryRepository
            .GetAll()
            .Where(category => !request.CategoryId.HasValue || category.Id != request.CategoryId.Value)
            .ToDictionary(category => category.SlotKey);
        var transferTargets = SlotKeyCatalog.All
            .Where(slotKey => slotKey != request.SlotKey)
            .Select(slotKey =>
            {
                categoriesBySlot.TryGetValue(slotKey, out var category);
                return new TransferTargetState(slotKey, category?.Name);
            })
            .ToList();

        return new CategoryEditorState(isSlotEnabled, transferTargets);
    }
}

public sealed class LoadSnippetEditorStateUseCase
{
    private readonly ISnippetRepository _snippetRepository;
    private readonly ISettingsRepository _settingsStore;

    public LoadSnippetEditorStateUseCase(
        ISnippetRepository snippetRepository,
        ISettingsRepository settingsStore)
    {
        _snippetRepository = snippetRepository;
        _settingsStore = settingsStore;
    }

    public SnippetEditorState Execute(LoadSnippetEditorStateRequest request)
    {
        var settings = _settingsStore.Load();
        var isSlotEnabled = !settings.EnabledSnippetSlotKeys.TryGetValue(request.SlotKey, out var enabled)
            || enabled;
        var snippetsBySlot = _snippetRepository
            .GetByCategoryId(request.CategoryId)
            .Where(snippet => !request.SnippetId.HasValue || snippet.Id != request.SnippetId.Value)
            .ToDictionary(snippet => snippet.SlotKey);
        var transferTargets = SlotKeyCatalog.All
            .Where(slotKey => slotKey != request.SlotKey)
            .Select(slotKey =>
            {
                snippetsBySlot.TryGetValue(slotKey, out var snippet);
                return new TransferTargetState(slotKey, snippet?.Title);
            })
            .ToList();

        return new SnippetEditorState(
            isSlotEnabled,
            transferTargets,
            SpotifyConnectionState.FromSettings(settings));
    }
}

public sealed record LoadCategoryEditorStateRequest(SlotKey SlotKey, Guid? CategoryId);

public sealed record LoadSnippetEditorStateRequest(Guid CategoryId, SlotKey SlotKey, Guid? SnippetId);

public sealed record CategoryEditorState(
    bool IsSlotEnabled,
    IReadOnlyList<TransferTargetState> TransferTargets);

public sealed record SnippetEditorState(
    bool IsSlotEnabled,
    IReadOnlyList<TransferTargetState> TransferTargets,
    SpotifyConnectionState SpotifyConnection);

public sealed record TransferTargetState(SlotKey SlotKey, string? ExistingTitle);

public sealed record SpotifyConnectionState(bool IsConnected, string? DisplayName = null)
{
    public static SpotifyConnectionState FromSettings(AppSettings settings)
    {
        var isConnected = !string.IsNullOrWhiteSpace(settings.SpotifyClientId)
            && !string.IsNullOrWhiteSpace(settings.SpotifyAccessToken)
            && !string.IsNullOrWhiteSpace(settings.SpotifyRefreshToken);

        return new SpotifyConnectionState(
            isConnected,
            string.IsNullOrWhiteSpace(settings.SpotifyConnectedUserDisplayName)
                ? null
                : settings.SpotifyConnectedUserDisplayName);
    }
}
