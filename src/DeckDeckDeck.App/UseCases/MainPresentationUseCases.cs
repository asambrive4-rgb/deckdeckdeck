using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class PrepareSnippetActionUseCase
{
    public PrepareSnippetActionResult Execute(PrepareSnippetActionRequest request)
    {
        var shouldHideBeforeExecute =
            (request.Action.ActionType == SnippetActionType.PasteText
                || request.Action.ActionType == SnippetActionType.LaunchFile
                && request.Action.FileActionMode == FileActionMode.Paste)
            && request.Settings.AutoHideAfterPaste;

        return new PrepareSnippetActionResult(shouldHideBeforeExecute);
    }
}

public sealed class SaveWindowPlacementUseCase
{
    private readonly IAppLogger? _logger;
    private readonly ISettingsRepository _settingsRepository;

    public SaveWindowPlacementUseCase(
        ISettingsRepository settingsRepository,
        IAppLogger? logger = null)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    public void Execute(SaveWindowPlacementRequest request)
    {
        try
        {
            _settingsRepository.SaveWindowPlacement(
                request.Left,
                request.Top,
                request.ScreenDeviceName);
        }
        catch (Exception ex)
        {
            _logger?.Log("Window placement save failed.", ex);
        }
    }
}

public sealed class LoadSettingsUseCase : ILoadSettingsUseCase
{
    private readonly ISettingsRepository _settingsRepository;

    public LoadSettingsUseCase(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public AppSettings Execute()
    {
        return _settingsRepository.Load();
    }
}

public sealed class LoadHomeGridUseCase
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISettingsRepository _settingsRepository;

    public LoadHomeGridUseCase(
        ICategoryRepository categoryRepository,
        ISettingsRepository settingsRepository)
    {
        _categoryRepository = categoryRepository;
        _settingsRepository = settingsRepository;
    }

    public HomeGridState Execute()
    {
        return new HomeGridState(
            _categoryRepository.GetAll(),
            _settingsRepository.Load());
    }
}

public sealed class LoadCategoryGridUseCase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ISnippetRepository _snippetRepository;

    public LoadCategoryGridUseCase(
        ISnippetRepository snippetRepository,
        ISettingsRepository settingsRepository)
    {
        _snippetRepository = snippetRepository;
        _settingsRepository = settingsRepository;
    }

    public CategoryGridState Execute(Guid categoryId)
    {
        return new CategoryGridState(
            _snippetRepository.GetByCategoryId(categoryId),
            _settingsRepository.Load());
    }
}

public sealed record PrepareSnippetActionRequest(
    ExecutableAction Action,
    AppSettings Settings)
{
    public PrepareSnippetActionRequest(Snippet snippet, AppSettings settings)
        : this(ExecutableAction.FromSnippet(snippet), settings)
    {
    }
}

public sealed record PrepareSnippetActionResult(bool ShouldHideBeforeExecute);

public sealed record SaveWindowPlacementRequest(
    double Left,
    double Top,
    string ScreenDeviceName);

public sealed record HomeGridState(
    IReadOnlyList<Category> Categories,
    AppSettings Settings);

public sealed record CategoryGridState(
    IReadOnlyList<Snippet> Snippets,
    AppSettings Settings);
