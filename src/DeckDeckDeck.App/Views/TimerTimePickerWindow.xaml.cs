// 역할: 슬롯 타이머 시간을 선택하고 마우스 드래그로 창을 이동할 수 있는 팝업 창의 코드 비하인드입니다.
using System.Windows;
using System.Windows.Input;
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

    private void OnBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
