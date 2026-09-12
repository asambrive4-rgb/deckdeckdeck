// 역할: 슬롯 타이머 시간을 시/분/초 단위로 선택할 수 있는 팝업 창의 코드 비하인드입니다.
using System.Windows;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Views;

public partial class TimerTimePickerWindow : Window
{
    public TimerTimePickerWindow(TimerTimePickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose = () =>
        {
            DialogResult = viewModel.DialogResult;
            Close();
        };
    }
}
