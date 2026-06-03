using System.Windows;
using System.Windows.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!TryGetSlotKey(e.Key, out var slotKey))
        {
            return;
        }

        viewModel.SelectSlot(slotKey);
        e.Handled = true;
    }

    private static bool TryGetSlotKey(Key key, out SlotKey slotKey)
    {
        slotKey = key switch
        {
            Key.NumPad0 => SlotKey.Numpad0,
            Key.NumPad1 => SlotKey.Numpad1,
            Key.NumPad2 => SlotKey.Numpad2,
            Key.NumPad3 => SlotKey.Numpad3,
            Key.NumPad4 => SlotKey.Numpad4,
            Key.NumPad5 => SlotKey.Numpad5,
            Key.NumPad6 => SlotKey.Numpad6,
            Key.NumPad7 => SlotKey.Numpad7,
            Key.NumPad8 => SlotKey.Numpad8,
            Key.NumPad9 => SlotKey.Numpad9,
            Key.Divide => SlotKey.NumpadDivide,
            Key.Multiply => SlotKey.NumpadMultiply,
            Key.Subtract => SlotKey.NumpadSubtract,
            Key.Add => SlotKey.NumpadAdd,
            Key.Decimal => SlotKey.NumpadDecimal,
            _ => default
        };

        return key is Key.NumPad0
            or Key.NumPad1
            or Key.NumPad2
            or Key.NumPad3
            or Key.NumPad4
            or Key.NumPad5
            or Key.NumPad6
            or Key.NumPad7
            or Key.NumPad8
            or Key.NumPad9
            or Key.Divide
            or Key.Multiply
            or Key.Subtract
            or Key.Add
            or Key.Decimal;
    }
}
