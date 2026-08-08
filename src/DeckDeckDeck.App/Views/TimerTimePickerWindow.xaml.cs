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
