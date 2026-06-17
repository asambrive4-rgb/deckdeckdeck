using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class HotkeyListViewModel
{
    public HotkeyListViewModel(
        IReadOnlyList<HotkeyAction> actions,
        SetHotkeyActionEnabledUseCase setHotkeyActionEnabledUseCase,
        DeleteHotkeyActionUseCase deleteHotkeyActionUseCase,
        IDialogAdapter dialogAdapter,
        Action addHotkey,
        Action<HotkeyAction> editHotkey,
        Action back,
        Action reload,
        Action notifyHotkeysChanged,
        Action<string> showStatus)
    {
        AddCommand = new RelayCommand(addHotkey);
        BackCommand = new RelayCommand(back);
        Items = actions.Select(action => new HotkeyActionListItemViewModel(
                action,
                setHotkeyActionEnabledUseCase,
                deleteHotkeyActionUseCase,
                dialogAdapter,
                editHotkey,
                reload,
                notifyHotkeysChanged,
                showStatus))
            .ToList();
    }

    public IReadOnlyList<HotkeyActionListItemViewModel> Items { get; }

    public bool HasItems => Items.Count > 0;

    public bool IsEmpty => !HasItems;

    public ICommand AddCommand { get; }

    public ICommand BackCommand { get; }
}

public sealed class HotkeyActionListItemViewModel
{
    private readonly HotkeyAction _action;
    private readonly DeleteHotkeyActionUseCase _deleteHotkeyActionUseCase;
    private readonly IDialogAdapter _dialogAdapter;
    private readonly Action<HotkeyAction> _editHotkey;
    private readonly Action _notifyHotkeysChanged;
    private readonly Action _reload;
    private readonly SetHotkeyActionEnabledUseCase _setHotkeyActionEnabledUseCase;
    private readonly Action<string> _showStatus;

    public HotkeyActionListItemViewModel(
        HotkeyAction action,
        SetHotkeyActionEnabledUseCase setHotkeyActionEnabledUseCase,
        DeleteHotkeyActionUseCase deleteHotkeyActionUseCase,
        IDialogAdapter dialogAdapter,
        Action<HotkeyAction> editHotkey,
        Action reload,
        Action notifyHotkeysChanged,
        Action<string> showStatus)
    {
        _action = action;
        _setHotkeyActionEnabledUseCase = setHotkeyActionEnabledUseCase;
        _deleteHotkeyActionUseCase = deleteHotkeyActionUseCase;
        _dialogAdapter = dialogAdapter;
        _editHotkey = editHotkey;
        _reload = reload;
        _notifyHotkeysChanged = notifyHotkeysChanged;
        _showStatus = showStatus;
        EditCommand = new RelayCommand(() => _editHotkey(_action));
        ToggleEnabledCommand = new RelayCommand(ToggleEnabled);
        DeleteCommand = new RelayCommand(Delete);
    }

    public Guid Id => _action.Id;

    public string Title => _action.Title;

    public string HotkeyText => _action.HotkeyDisplayText;

    public string ActionTypeText => HotkeyActionDisplayText.GetActionTypeLabel(_action.ActionType);

    public string Summary => HotkeyActionDisplayText.GetSummary(_action);

    public bool IsEnabled => _action.IsEnabled;

    public string EnabledText => IsEnabled ? "사용 중" : "사용 안 함";

    public string ToggleEnabledText => IsEnabled ? "사용 중지" : "사용";

    public ICommand EditCommand { get; }

    public ICommand ToggleEnabledCommand { get; }

    public ICommand DeleteCommand { get; }

    private void ToggleEnabled()
    {
        var result = _setHotkeyActionEnabledUseCase.Execute(_action.Id, !_action.IsEnabled);
        if (!result.Succeeded)
        {
            _showStatus(result.ErrorMessage ?? "핫키 상태를 저장하지 못했습니다.");
            return;
        }

        _notifyHotkeysChanged();
        _showStatus(_action.IsEnabled ? "핫키를 사용 중지했습니다." : "핫키를 사용하도록 설정했습니다.");
        _reload();
    }

    private void Delete()
    {
        var confirmed = _dialogAdapter.Confirm(
            "핫키 삭제",
            $"'{_action.Title}' 핫키를 삭제할까요?");
        if (!confirmed)
        {
            return;
        }

        _deleteHotkeyActionUseCase.Execute(_action.Id);
        _notifyHotkeysChanged();
        _showStatus("핫키를 삭제했습니다.");
        _reload();
    }

}
