using System.Windows.Controls;
using System.Windows.Automation;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class TextInputFocusDetectorTests
{
    [Fact]
    public void WpfTextInputElementsAreDetected()
    {
        var result = RunInSta(() => new
        {
            TextBox = TextInputFocusDetector.IsWpfTextInputElement(new TextBox()),
            PasswordBox = TextInputFocusDetector.IsWpfTextInputElement(new PasswordBox()),
            EditableComboBox = TextInputFocusDetector.IsWpfTextInputElement(new ComboBox { IsEditable = true })
        });

        Assert.True(result.TextBox);
        Assert.True(result.PasswordBox);
        Assert.True(result.EditableComboBox);
    }

    [Fact]
    public void NonTextInputWpfElementsAreNotDetected()
    {
        var result = RunInSta(() => new
        {
            Button = TextInputFocusDetector.IsWpfTextInputElement(new Button()),
            ComboBox = TextInputFocusDetector.IsWpfTextInputElement(new ComboBox { IsEditable = false })
        });

        Assert.False(result.Button);
        Assert.False(result.ComboBox);
    }

    [Fact]
    public void AutomationEditControlIsDetectedAsTextInput()
    {
        Assert.True(TextInputFocusDetector.IsAutomationTextInputElement(
            ControlType.Edit,
            hasEditableValuePattern: false));
    }

    [Fact]
    public void AutomationEditableValuePatternIsDetectedAsTextInput()
    {
        Assert.True(TextInputFocusDetector.IsAutomationTextInputElement(
            ControlType.Custom,
            hasEditableValuePattern: true));
    }

    [Fact]
    public void AutomationDocumentIsNotTreatedAsTextInputByItself()
    {
        Assert.False(TextInputFocusDetector.IsAutomationTextInputElement(
            ControlType.Document,
            hasEditableValuePattern: false));
    }

    [Theory]
    [InlineData(Win32Constants.VkLeft)]
    [InlineData(Win32Constants.VkUp)]
    [InlineData(Win32Constants.VkRight)]
    [InlineData(Win32Constants.VkDown)]
    public void DirectHotkeyPassthroughPolicyOnlyMatchesUnmodifiedArrowKeys(uint virtualKey)
    {
        Assert.True(DirectHotkeyPassthroughPolicy.IsUnmodifiedArrowKey(
            new HotkeyGesture(virtualKey, HotkeyModifiers.None)));
        Assert.False(DirectHotkeyPassthroughPolicy.IsUnmodifiedArrowKey(
            new HotkeyGesture(virtualKey, HotkeyModifiers.Control)));
    }
}
