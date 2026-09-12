// 역할: 타이머 시간 설정 대화상자에서 분과 초를 선택하고 확인/취소하는 동작을 처리하는 화면 모델입니다.
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DeckDeckDeck.App.ViewModels;

public sealed partial class TimerTimePickerViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalDuration))]
    [NotifyPropertyChangedFor(nameof(IsValidDuration))]
    private int _minutes = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalDuration))]
    [NotifyPropertyChangedFor(nameof(IsValidDuration))]
    private int _seconds = 0;

    [ObservableProperty]
    private bool? _dialogResult;

    public TimeSpan TotalDuration => TimeSpan.FromMinutes(Minutes) + TimeSpan.FromSeconds(Seconds);
    public bool IsValidDuration => TotalDuration > TimeSpan.Zero;

    public Action? RequestClose { get; set; }

    public IRelayCommand<string> SelectPresetCommand { get; }
    public IRelayCommand ConfirmCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public TimerTimePickerViewModel()
    {
        SelectPresetCommand = new RelayCommand<string>(presetStr =>
        {
            if (int.TryParse(presetStr, out int presetSeconds))
            {
                Minutes = presetSeconds / 60;
                Seconds = presetSeconds % 60;
            }
        });

        ConfirmCommand = new RelayCommand(() =>
        {
            if (IsValidDuration)
            {
                DialogResult = true;
                RequestClose?.Invoke();
            }
        });

        CancelCommand = new RelayCommand(() =>
        {
            DialogResult = false;
            RequestClose?.Invoke();
        });
    }
}
