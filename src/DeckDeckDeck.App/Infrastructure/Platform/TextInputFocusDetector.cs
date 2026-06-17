using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Platform;

/// <summary>
/// 현재 사용자가 텍스트 입력창(텍스트 박스, 비밀번호 입력창 등)에 포커스를 두고 있는지 감지하는 클래스입니다.
/// 단축키(Hotkey)가 텍스트 입력 중에 오작동하거나 겹치는 것을 방지하기 위해 사용됩니다.
/// WPF 내부 컨트롤 감지와 외부 애플리케이션(UI Automation) 감지를 모두 지원합니다.
/// </summary>
public sealed class TextInputFocusDetector : ITextInputFocusDetector
{
    /// <summary>
    /// 현재 활성화된 화면에서 텍스트 입력 관련 요소가 포커스를 가지고 있는지 확인합니다.
    /// </summary>
    /// <returns>텍스트 입력창이 포커스되어 있다면 true, 그렇지 않다면 false</returns>
    public bool IsTextInputFocused()
    {
        // 1단계: WPF 애플리케이션 내부(본 앱)의 입력창 포커스 여부를 먼저 검사합니다.
        if (IsWpfTextInputFocused())
        {
            return true;
        }

        // 2단계: 본 앱이 아닌 외부 앱(메모장, 웹 브라우저 등)이 활성화되어 있는 경우,
        // Windows OS의 UI Automation을 통해 포커스된 외부 요소를 가져와 검사합니다.
        var focusedElement = AutomationElement.FocusedElement;
        return focusedElement is not null && IsAutomationTextInputElement(focusedElement);
    }

    /// <summary>
    /// 현재 DeckDeckDeck 앱의 활성화된 창 내에서 텍스트 입력창에 키보드 포커스가 있는지 검사합니다.
    /// </summary>
    private static bool IsWpfTextInputFocused()
    {
        var application = Application.Current;
        if (application is null || !application.Windows.OfType<Window>().Any(IsActiveWindowWithKeyboardFocus))
        {
            return false;
        }

        return IsWpfTextInputElement(Keyboard.FocusedElement);
    }

    /// <summary>
    /// 지정된 WPF 요소가 텍스트 입력 컨트롤인지 판별합니다.
    /// </summary>
    /// <param name="focusedElement">검사할 WPF 요소</param>
    /// <returns>텍스트 입력 컨트롤(TextBox, PasswordBox, 편집 가능한 ComboBox)인 경우 true</returns>
    internal static bool IsWpfTextInputElement(object? focusedElement)
    {
        return focusedElement is TextBoxBase // TextBox, RichTextBox 등
            or PasswordBox // 비밀번호 입력 상자
            or ComboBox { IsEditable: true }; // 직접 텍스트를 타이핑할 수 있는 드롭다운(콤보박스)
    }

    /// <summary>
    /// UI Automation 요소를 분석하여 텍스트 입력창인지 판별합니다.
    /// </summary>
    /// <param name="focusedElement">UI Automation으로 감지된 포커스 요소</param>
    /// <returns>텍스트 입력 요소인 경우 true</returns>
    internal static bool IsAutomationTextInputElement(AutomationElement focusedElement)
    {
        var controlType = focusedElement.Current.ControlType;
        
        // ValuePattern이 존재하고 읽기 전용이 아닌 경우, 
        // 컨트롤 타입이 Edit가 아니더라도(예: Document, Custom 등) 텍스트 입력이 가능한 영역으로 판단합니다.
        var hasEditableValuePattern = focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern)
            && valuePattern is ValuePattern value
            && !value.Current.IsReadOnly;

        return IsAutomationTextInputElement(controlType, hasEditableValuePattern);
    }

    /// <summary>
    /// UI Automation의 컨트롤 타입 및 값 편집 패턴 정보를 기준으로 텍스트 입력창 여부를 결정합니다.
    /// </summary>
    internal static bool IsAutomationTextInputElement(
        ControlType controlType,
        bool hasEditableValuePattern)
    {
        // OS 표준 에디터 컨트롤(Edit)이거나, 쓰기 권한이 허용된 ValuePattern 속성을 가지고 있다면 입력창으로 인정합니다.
        return controlType == ControlType.Edit || hasEditableValuePattern;
    }

    /// <summary>
    /// 검사 대상 윈도우가 활성화되어 있고, 내부에 키보드 포커스가 도달해 있는지 확인합니다.
    /// </summary>
    private static bool IsActiveWindowWithKeyboardFocus(Window window)
    {
        return window.IsActive && window.IsKeyboardFocusWithin;
    }
}
