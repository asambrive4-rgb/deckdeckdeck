using System.Windows.Controls;
using System.Windows.Automation;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
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
    public void HotkeyGestureOnlyMatchesUnmodifiedArrowKeys(uint virtualKey)
    {
        Assert.True(new HotkeyGesture(virtualKey, HotkeyModifiers.None).IsUnmodifiedArrowKey);
        Assert.False(new HotkeyGesture(virtualKey, HotkeyModifiers.Control).IsUnmodifiedArrowKey);
    }

    [Fact]
    public void ShouldPassThroughDirectHotkeyUseCasePassesOnlyUnmodifiedArrowInTextInput()
    {
        var useCase = new ShouldPassThroughDirectHotkeyUseCase(new StubTextInputFocusDetector(true));

        Assert.True(useCase.Execute(new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.None)));
        Assert.False(useCase.Execute(new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.Control)));
        Assert.False(useCase.Execute(new HotkeyGesture(0x67, HotkeyModifiers.None)));
    }

    [Fact]
    public void ShouldPassThroughDirectHotkeyUseCaseKeepsExistingBehaviorWhenDetectionFails()
    {
        var useCase = new ShouldPassThroughDirectHotkeyUseCase(new ThrowingTextInputFocusDetector());

        Assert.False(useCase.Execute(new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.None)));
    }

    private sealed class StubTextInputFocusDetector : ITextInputFocusDetector
    {
        private readonly bool _isTextInputFocused;

        public StubTextInputFocusDetector(bool isTextInputFocused)
        {
            _isTextInputFocused = isTextInputFocused;
        }

        public bool IsTextInputFocused()
        {
            return _isTextInputFocused;
        }
    }

    private sealed class ThrowingTextInputFocusDetector : ITextInputFocusDetector
    {
        public bool IsTextInputFocused()
        {
            throw new InvalidOperationException("focus detection failed");
        }
    }
}
