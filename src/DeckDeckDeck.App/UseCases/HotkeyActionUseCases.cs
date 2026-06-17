using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class LoadHotkeyActionsUseCase
{
    private readonly IHotkeyActionRepository _hotkeyActionRepository;

    public LoadHotkeyActionsUseCase(IHotkeyActionRepository hotkeyActionRepository)
    {
        _hotkeyActionRepository = hotkeyActionRepository;
    }

    public IReadOnlyList<HotkeyAction> Execute()
    {
        return _hotkeyActionRepository.GetAll();
    }
}

public sealed class GetHotkeyActionByIdUseCase
{
    private readonly IHotkeyActionRepository _hotkeyActionRepository;

    public GetHotkeyActionByIdUseCase(IHotkeyActionRepository hotkeyActionRepository)
    {
        _hotkeyActionRepository = hotkeyActionRepository;
    }

    public HotkeyAction? Execute(Guid id)
    {
        return _hotkeyActionRepository.GetById(id);
    }
}

public sealed class LoadHotkeyActionEditorStateUseCase
{
    private readonly ISettingsRepository _settingsRepository;

    public LoadHotkeyActionEditorStateUseCase(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public HotkeyActionEditorState Execute()
    {
        return new HotkeyActionEditorState(
            SpotifyConnectionState.FromSettings(_settingsRepository.Load()));
    }
}

public sealed class SaveHotkeyActionUseCase
{
    public const string HotkeyRequiredMessage = "사용할 핫키를 입력해 주세요.";
    public const string HotkeyModifierOnlyMessage = "Ctrl, Shift, Alt, Win만으로는 핫키를 저장할 수 없습니다.";

    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly IHotkeyActionRepository _hotkeyActionRepository;

    public SaveHotkeyActionUseCase(
        IHotkeyActionRepository hotkeyActionRepository,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _hotkeyActionRepository = hotkeyActionRepository;
        _autoBackupRequester = autoBackupRequester;
    }

    public SaveHotkeyActionResult Execute(SaveHotkeyActionRequest request)
    {
        if (request.IsEnabled && request.Gesture is null)
        {
            return SaveHotkeyActionResult.Failure(HotkeyRequiredMessage);
        }

        if (request.Gesture is not null && !request.Gesture.IsComplete)
        {
            return SaveHotkeyActionResult.Failure(HotkeyModifierOnlyMessage);
        }

        var validation = SnippetRules.ValidateForSave(
            request.Title,
            request.Content,
            request.ActionType,
            request.LaunchPath,
            request.LaunchUrl,
            request.SelectedMediaProvider,
            request.SelectedMediaCommand,
            request.TerminalCommand,
            request.SelectedTerminalShell,
            request.RunAsAdministrator);

        if (!validation.Succeeded)
        {
            return SaveHotkeyActionResult.Failure(validation.ErrorMessage!);
        }

        var saveData = new HotkeyActionSaveData(
            request.Title,
            request.Gesture,
            request.IsEnabled,
            request.Content,
            request.Description,
            request.ImagePath,
            request.ThumbnailPath,
            request.ActionType,
            request.LaunchPath,
            request.SlotImageMode,
            request.AutoIcon,
            validation.NormalizedLaunchUrl,
            validation.MediaProvider,
            validation.MediaCommand,
            request.PasteShortcutMode,
            validation.NormalizedTerminalCommand,
            validation.TerminalShell,
            validation.RunAsAdministrator);

        var action = request.HotkeyActionId.HasValue
            ? _hotkeyActionRepository.Update(request.HotkeyActionId.Value, saveData)
            : _hotkeyActionRepository.Create(saveData);

        _autoBackupRequester?.RequestAutoBackup();

        return SaveHotkeyActionResult.Success(action, validation.NormalizedLaunchUrl);
    }
}

public sealed class DeleteHotkeyActionUseCase
{
    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly IHotkeyActionRepository _hotkeyActionRepository;
    private readonly IImageFileRepository? _imageFileRepository;

    public DeleteHotkeyActionUseCase(
        IHotkeyActionRepository hotkeyActionRepository,
        IImageFileRepository? imageFileRepository = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _hotkeyActionRepository = hotkeyActionRepository;
        _imageFileRepository = imageFileRepository;
        _autoBackupRequester = autoBackupRequester;
    }

    public void Execute(Guid hotkeyActionId)
    {
        var deletedImageFiles = _hotkeyActionRepository.Delete(hotkeyActionId);
        _imageFileRepository?.DeleteImageFiles(deletedImageFiles);
        _autoBackupRequester?.RequestAutoBackup();
    }
}

public sealed record HotkeyActionEditorState(SpotifyConnectionState SpotifyConnection);

public sealed record SaveHotkeyActionRequest(
    Guid? HotkeyActionId,
    string Title,
    HotkeyGesture? Gesture,
    bool IsEnabled,
    string Content,
    string? Description,
    string? ImagePath,
    string? ThumbnailPath,
    SnippetActionType ActionType,
    string LaunchPath,
    SlotImageMode SlotImageMode,
    AutoIconCacheEntry? AutoIcon,
    string? LaunchUrl,
    SnippetMediaProvider SelectedMediaProvider,
    SnippetMediaCommand SelectedMediaCommand,
    PasteShortcutMode PasteShortcutMode = PasteShortcutMode.CtrlV,
    string TerminalCommand = "",
    SnippetTerminalShell SelectedTerminalShell = SnippetTerminalShell.Cmd,
    bool RunAsAdministrator = true);

public sealed record SaveHotkeyActionResult(
    bool Succeeded,
    HotkeyAction? HotkeyAction = null,
    string? ErrorMessage = null,
    string? NormalizedLaunchUrl = null)
{
    public static SaveHotkeyActionResult Success(HotkeyAction action, string? normalizedLaunchUrl)
    {
        return new SaveHotkeyActionResult(true, action, NormalizedLaunchUrl: normalizedLaunchUrl);
    }

    public static SaveHotkeyActionResult Failure(string errorMessage)
    {
        return new SaveHotkeyActionResult(false, ErrorMessage: errorMessage);
    }
}
