using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Views;

public partial class HotkeyEditView : UserControl
{
    public HotkeyEditView()
    {
        InitializeComponent();
    }

    private void HotkeyInputButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not HotkeyEditViewModel viewModel || !viewModel.IsCapturingHotkey)
        {
            return;
        }

        var key = GetRealKey(e);
        if (key == Key.Escape)
        {
            viewModel.CancelHotkeyCapture();
            e.Handled = true;
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            e.Handled = true;
            return;
        }

        viewModel.CaptureHotkey(new HotkeyGesture(
            (uint)virtualKey,
            GetCurrentModifiers()));
        e.Handled = true;
    }

    private void ImageDropTarget_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ImageDropTarget_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is HotkeyEditViewModel viewModel
            && e.Data.GetData(DataFormats.FileDrop) is string[] sourcePaths)
        {
            viewModel.DropImageFiles(sourcePaths);
        }

        e.Handled = true;
    }

    private static Key GetRealKey(KeyEventArgs e)
    {
        if (e.Key == Key.System)
        {
            return e.SystemKey;
        }

        if (e.Key == Key.ImeProcessed)
        {
            return e.ImeProcessedKey;
        }

        return e.Key;
    }

    private static HotkeyModifiers GetCurrentModifiers()
    {
        var modifiers = HotkeyModifiers.None;
        var keyboardModifiers = Keyboard.Modifiers;
        if (keyboardModifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (keyboardModifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (keyboardModifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (keyboardModifiers.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return modifiers;
    }
}
