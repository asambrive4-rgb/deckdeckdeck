using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class TextInputFocusDetector : ITextInputFocusDetector
{
    public bool IsTextInputFocused()
    {
        if (IsWpfTextInputFocused())
        {
            return true;
        }

        var focusedElement = AutomationElement.FocusedElement;
        return focusedElement is not null && IsAutomationTextInputElement(focusedElement);
    }

    private static bool IsWpfTextInputFocused()
    {
        var application = Application.Current;
        if (application is null || !application.Windows.OfType<Window>().Any(IsActiveWindowWithKeyboardFocus))
        {
            return false;
        }

        return IsWpfTextInputElement(Keyboard.FocusedElement);
    }

    internal static bool IsWpfTextInputElement(object? focusedElement)
    {
        return focusedElement is TextBoxBase
            or PasswordBox
            or ComboBox { IsEditable: true };
    }

    internal static bool IsAutomationTextInputElement(AutomationElement focusedElement)
    {
        var controlType = focusedElement.Current.ControlType;
        var hasEditableValuePattern = focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern)
            && valuePattern is ValuePattern value
            && !value.Current.IsReadOnly;

        return IsAutomationTextInputElement(controlType, hasEditableValuePattern);
    }

    internal static bool IsAutomationTextInputElement(
        ControlType controlType,
        bool hasEditableValuePattern)
    {
        return controlType == ControlType.Edit || hasEditableValuePattern;
    }

    private static bool IsActiveWindowWithKeyboardFocus(Window window)
    {
        return window.IsActive && window.IsKeyboardFocusWithin;
    }
}
