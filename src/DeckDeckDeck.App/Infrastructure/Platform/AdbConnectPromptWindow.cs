// 역할: 안드로이드 무선 디버깅(ADB) 일상 연결 및 6자리 OTP 페어링 입력을 받는 현대적인 모달 대화상자 창을 띄웁니다.
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal sealed class AdbConnectPromptWindow : Window
{
    private readonly TextBox _portBox;
    private readonly TextBox[] _codeBoxes = new TextBox[6];
    private readonly Border[] _codeBoxBorders = new Border[6];
    private readonly Border _connectTabBorder;
    private readonly Border _pairTabBorder;
    private readonly TextBlock _connectTabText;
    private readonly TextBlock _pairTabText;
    private readonly TextBlock _portLabel;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _errorText;
    private readonly Button _confirmButton;
    private readonly StackPanel _otpSection;
    private AdbPromptMode _currentMode;

    public AdbPromptResult Result { get; private set; }

    public string Port => Result?.Port ?? _portBox.Text?.Trim() ?? string.Empty;

    public AdbConnectPromptWindow(
        string title,
        string fixedIp,
        AdbPromptMode initialMode = AdbPromptMode.Connect)
    {
        _currentMode = initialMode;
        Result = new AdbPromptResult(initialMode, string.Empty);

        Title = string.IsNullOrWhiteSpace(title) ? "ADB 무선 연결 / 페어링" : title;
        Width = 420;
        MinWidth = 360;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        Topmost = true;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        var surfaceBrush = ResolveBrush("Deck.Brush.Background.Surface", Color.FromRgb(0xFC, 0xFA, 0xF6));
        var appBgBrush = ResolveBrush("Deck.Brush.Background.App", Color.FromRgb(0xF2, 0xEE, 0xE7));
        var lineSoftBrush = ResolveBrush("Deck.Brush.Line.Soft", Color.FromRgb(0x1E, 0x1B, 0x18));
        var textPrimaryBrush = ResolveBrush("Deck.Brush.Text.Primary", Color.FromRgb(0x1E, 0x1B, 0x18));
        var textSecondaryBrush = ResolveBrush("Deck.Brush.Text.Secondary", Color.FromRgb(0x5C, 0x55, 0x4E));
        var codeFont = new FontFamily("Cascadia Code, Consolas, Segoe UI");

        var outerBorder = new Border
        {
            Margin = new Thickness(12),
            Background = surfaceBrush,
            BorderBrush = lineSoftBrush,
            BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            SnapsToDevicePixels = true,
            Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 4,
                Opacity = 0.16,
                Color = Color.FromRgb(0x1E, 0x1B, 0x18)
            }
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Tab Switcher
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Description
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Fixed IP
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 6-Digit OTP (Pair mode)
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Port Input
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Error Text
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

        // 1. Header (Title + Close button)
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };

        var titleText = new TextBlock
        {
            Text = "⚡ 무선 디버깅 (ADB)",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = textPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleText, 0);
        headerGrid.Children.Add(titleText);

        var closeButton = new Button
        {
            Content = "✕",
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = textSecondaryBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "닫기"
        };
        closeButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        Grid.SetColumn(closeButton, 1);
        headerGrid.Children.Add(closeButton);
        Grid.SetRow(headerGrid, 0);
        rootGrid.Children.Add(headerGrid);

        // 2. Mode Switcher (Segmented Tab)
        var tabContainer = new Border
        {
            Background = appBgBrush,
            BorderBrush = lineSoftBrush,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(3),
            Margin = new Thickness(0, 0, 0, 14),
            Height = 40
        };

        var tabGrid = new Grid();
        tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _connectTabBorder = new Border
        {
            CornerRadius = new CornerRadius(9),
            Cursor = Cursors.Hand
        };
        _connectTabText = new TextBlock
        {
            Text = "⚡ 일상 연결",
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _connectTabBorder.Child = _connectTabText;
        _connectTabBorder.MouseLeftButtonDown += (_, _) => SwitchMode(AdbPromptMode.Connect);
        Grid.SetColumn(_connectTabBorder, 0);
        tabGrid.Children.Add(_connectTabBorder);

        _pairTabBorder = new Border
        {
            CornerRadius = new CornerRadius(9),
            Cursor = Cursors.Hand
        };
        _pairTabText = new TextBlock
        {
            Text = "🔗 기기 처음 페어링",
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _pairTabBorder.Child = _pairTabText;
        _pairTabBorder.MouseLeftButtonDown += (_, _) => SwitchMode(AdbPromptMode.Pair);
        Grid.SetColumn(_pairTabBorder, 1);
        tabGrid.Children.Add(_pairTabBorder);

        tabContainer.Child = tabGrid;
        Grid.SetRow(tabContainer, 1);
        rootGrid.Children.Add(tabContainer);

        // 3. Description text
        _descriptionText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            FontSize = 12.5,
            Foreground = textSecondaryBrush
        };
        Grid.SetRow(_descriptionText, 2);
        rootGrid.Children.Add(_descriptionText);

        // 4. Fixed IP row
        var ipPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        ipPanel.Children.Add(CreateFieldLabel("IP (설정값)", textPrimaryBrush));

        var ipBorder = new Border
        {
            Background = appBgBrush,
            BorderBrush = lineSoftBrush,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7)
        };
        ipBorder.Child = new TextBlock
        {
            Text = fixedIp,
            FontFamily = codeFont,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13.5,
            Foreground = textPrimaryBrush
        };
        ipPanel.Children.Add(ipBorder);
        Grid.SetRow(ipPanel, 3);
        rootGrid.Children.Add(ipPanel);

        // 5. 6-Digit OTP Pairing Code Component (Pair Mode)
        _otpSection = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        _otpSection.Children.Add(CreateFieldLabel("6자리 페어링 코드", textPrimaryBrush));

        var otpRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 4)
        };

        for (var i = 0; i < 6; i++)
        {
            var index = i;
            var box = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                MaxLength = 1,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                FontFamily = codeFont,
                Foreground = textPrimaryBrush,
                CaretBrush = textPrimaryBrush
            };

            var boxBorder = new Border
            {
                Width = 38,
                Height = 44,
                Margin = new Thickness(2, 0, 2, 0),
                BorderThickness = new Thickness(2),
                BorderBrush = lineSoftBrush,
                CornerRadius = new CornerRadius(8),
                Background = surfaceBrush,
                Child = box
            };

            box.PreviewTextInput += (_, e) => OnOtpPreviewTextInput(index, e);
            box.PreviewKeyDown += (_, e) => OnOtpPreviewKeyDown(index, e);
            box.GotFocus += (_, _) =>
            {
                boxBorder.BorderBrush = ResolveBrush("Deck.Brush.Brand.Accent", Color.FromRgb(0x1E, 0x1B, 0x18));
                box.SelectAll();
            };
            box.LostFocus += (_, _) =>
            {
                boxBorder.BorderBrush = lineSoftBrush;
            };

            DataObject.AddPastingHandler(box, OnOtpPaste);

            _codeBoxes[i] = box;
            _codeBoxBorders[i] = boxBorder;
            otpRow.Children.Add(boxBorder);
        }

        _otpSection.Children.Add(otpRow);
        Grid.SetRow(_otpSection, 4);
        rootGrid.Children.Add(_otpSection);

        // 6. Port Input
        var portPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        _portLabel = CreateFieldLabel("포트", textPrimaryBrush);
        portPanel.Children.Add(_portLabel);

        var portBorder = new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = lineSoftBrush,
            CornerRadius = new CornerRadius(8),
            Background = surfaceBrush,
            Height = 40
        };

        _portBox = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(10, 6, 10, 6),
            FontSize = 14,
            FontFamily = codeFont,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = textPrimaryBrush,
            CaretBrush = textPrimaryBrush
        };
        _portBox.PreviewTextInput += OnPortPreviewTextInput;
        DataObject.AddPastingHandler(_portBox, OnPortPaste);
        _portBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Confirm();
                e.Handled = true;
            }
        };
        _portBox.GotFocus += (_, _) =>
        {
            portBorder.BorderBrush = ResolveBrush("Deck.Brush.Brand.Accent", Color.FromRgb(0x1E, 0x1B, 0x18));
        };
        _portBox.LostFocus += (_, _) =>
        {
            portBorder.BorderBrush = lineSoftBrush;
        };

        portBorder.Child = _portBox;
        portPanel.Children.Add(portBorder);
        Grid.SetRow(portPanel, 5);
        rootGrid.Children.Add(portPanel);

        // 7. Error Text
        _errorText = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = ResolveBrush("Deck.Brush.Text.Danger", Color.FromRgb(0xC2, 0x41, 0x0C)),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        Grid.SetRow(_errorText, 6);
        rootGrid.Children.Add(_errorText);

        // 8. Buttons
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var cancelButton = new Button
        {
            Content = "취소",
            MinWidth = 88,
            MinHeight = 36,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true,
            Cursor = Cursors.Hand
        };
        var secondaryStyle = ResolveStyle("Deck.SecondaryButtonStyle", "SecondaryButtonStyle");
        if (secondaryStyle is not null)
        {
            cancelButton.Style = secondaryStyle;
        }
        else
        {
            cancelButton.Padding = new Thickness(14, 6, 14, 6);
            cancelButton.Background = surfaceBrush;
            cancelButton.BorderBrush = lineSoftBrush;
        }
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        _confirmButton = new Button
        {
            MinWidth = 140,
            MinHeight = 36,
            IsDefault = true,
            Cursor = Cursors.Hand
        };
        var primaryStyle = ResolveStyle("Deck.PrimaryButtonStyle", "PrimaryButtonStyle");
        if (primaryStyle is not null)
        {
            _confirmButton.Style = primaryStyle;
        }
        else
        {
            _confirmButton.Padding = new Thickness(14, 6, 14, 6);
            _confirmButton.Background = ResolveBrush("Deck.Brush.Brand.Accent", Color.FromRgb(0xC7, 0xD7, 0xE8));
            _confirmButton.BorderBrush = lineSoftBrush;
        }
        _confirmButton.Click += (_, _) => Confirm();

        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(_confirmButton);
        Grid.SetRow(buttonRow, 7);
        rootGrid.Children.Add(buttonRow);

        outerBorder.Child = rootGrid;
        Content = outerBorder;

        PreviewKeyDown += OnPreviewKeyDown;

        SwitchMode(initialMode);

        Loaded += (_, _) =>
        {
            Activate();
            if (_currentMode == AdbPromptMode.Pair)
            {
                _codeBoxes[0].Focus();
            }
            else
            {
                _portBox.Focus();
            }
        };
    }

    private void SwitchMode(AdbPromptMode mode)
    {
        _currentMode = mode;
        _errorText.Visibility = Visibility.Collapsed;

        var isPair = mode == AdbPromptMode.Pair;
        _otpSection.Visibility = isPair ? Visibility.Visible : Visibility.Collapsed;

        _descriptionText.Text = isPair
            ? "기기의 '페어링 코드로 기기 페어링' 팝업에 표시된 6자리 코드와 포트를 입력하세요."
            : "포트 숫자만 입력하면 설정에 저장한 IP로 adb connect 합니다.";

        _portLabel.Text = isPair
            ? "페어링 포트 (페어링 팝업창의 포트)"
            : "접속 포트 (무선 디버깅 메인 화면의 포트)";

        _portBox.ToolTip = isPair ? "예: 38421" : "예: 41827";

        _confirmButton.Content = isPair
            ? "기기 페어링 (adb pair)"
            : "디버깅 연결 (adb connect)";

        UpdateTabVisuals();

        if (isPair)
        {
            _codeBoxes[0].Focus();
            _codeBoxes[0].SelectAll();
        }
        else
        {
            _portBox.Focus();
            _portBox.SelectAll();
        }
    }

    private void UpdateTabVisuals()
    {
        var accentBrush = ResolveBrush("Deck.Brush.Brand.Accent", Color.FromRgb(0xC7, 0xD7, 0xE8));
        var textPrimaryBrush = ResolveBrush("Deck.Brush.Text.Primary", Color.FromRgb(0x1E, 0x1B, 0x18));
        var textSecondaryBrush = ResolveBrush("Deck.Brush.Text.Secondary", Color.FromRgb(0x5C, 0x55, 0x4E));
        var lineBrush = ResolveBrush("Deck.Brush.Line.Soft", Color.FromRgb(0x1E, 0x1B, 0x18));

        if (_currentMode == AdbPromptMode.Connect)
        {
            _connectTabBorder.Background = accentBrush;
            _connectTabBorder.BorderThickness = new Thickness(1.5);
            _connectTabBorder.BorderBrush = lineBrush;
            _connectTabText.FontWeight = FontWeights.Bold;
            _connectTabText.Foreground = textPrimaryBrush;

            _pairTabBorder.Background = Brushes.Transparent;
            _pairTabBorder.BorderThickness = new Thickness(0);
            _pairTabBorder.BorderBrush = Brushes.Transparent;
            _pairTabText.FontWeight = FontWeights.SemiBold;
            _pairTabText.Foreground = textSecondaryBrush;
        }
        else
        {
            _pairTabBorder.Background = accentBrush;
            _pairTabBorder.BorderThickness = new Thickness(1.5);
            _pairTabBorder.BorderBrush = lineBrush;
            _pairTabText.FontWeight = FontWeights.Bold;
            _pairTabText.Foreground = textPrimaryBrush;

            _connectTabBorder.Background = Brushes.Transparent;
            _connectTabBorder.BorderThickness = new Thickness(0);
            _connectTabBorder.BorderBrush = Brushes.Transparent;
            _connectTabText.FontWeight = FontWeights.SemiBold;
            _connectTabText.Foreground = textSecondaryBrush;
        }
    }

    private void OnOtpPreviewTextInput(int index, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || !char.IsDigit(e.Text[0]))
        {
            e.Handled = true;
            return;
        }

        _codeBoxes[index].Text = e.Text[0].ToString();
        _codeBoxes[index].CaretIndex = 1;
        e.Handled = true;

        if (index < 5)
        {
            _codeBoxes[index + 1].Focus();
            _codeBoxes[index + 1].SelectAll();
        }
        else
        {
            _portBox.Focus();
            _portBox.SelectAll();
        }
    }

    private void OnOtpPreviewKeyDown(int index, KeyEventArgs e)
    {
        if (e.Key == Key.Back)
        {
            if (string.IsNullOrEmpty(_codeBoxes[index].Text))
            {
                if (index > 0)
                {
                    _codeBoxes[index - 1].Focus();
                    _codeBoxes[index - 1].Text = string.Empty;
                    e.Handled = true;
                }
            }
            else
            {
                _codeBoxes[index].Text = string.Empty;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Left)
        {
            if (index > 0)
            {
                _codeBoxes[index - 1].Focus();
                _codeBoxes[index - 1].SelectAll();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Right)
        {
            if (index < 5)
            {
                _codeBoxes[index + 1].Focus();
                _codeBoxes[index + 1].SelectAll();
                e.Handled = true;
            }
            else
            {
                _portBox.Focus();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            Confirm();
            e.Handled = true;
        }
        else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (Clipboard.ContainsText())
            {
                ApplyPastedCode(Clipboard.GetText());
                e.Handled = true;
            }
        }
    }

    private void OnOtpPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            return;
        }

        var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        ApplyPastedCode(text);
    }

    private void ApplyPastedCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var digits = new string(text.Where(char.IsDigit).Take(6).ToArray());
        if (digits.Length == 0)
        {
            return;
        }

        for (var i = 0; i < 6; i++)
        {
            _codeBoxes[i].Text = i < digits.Length ? digits[i].ToString() : string.Empty;
        }

        if (digits.Length == 6)
        {
            if (string.IsNullOrWhiteSpace(_portBox.Text))
            {
                _portBox.Focus();
                _portBox.SelectAll();
            }
            else
            {
                _confirmButton.Focus();
            }
        }
        else
        {
            var nextIndex = Math.Min(digits.Length, 5);
            _codeBoxes[nextIndex].Focus();
            _codeBoxes[nextIndex].SelectAll();
        }
    }

    private void Confirm()
    {
        var port = _portBox.Text?.Trim() ?? string.Empty;

        if (_currentMode == AdbPromptMode.Pair)
        {
            var pairCode = string.Concat(_codeBoxes.Select(b => b.Text?.Trim() ?? string.Empty));
            if (!TerminalCommandParameterRules.TryNormalizeAdbPairCode(pairCode, out var normalizedCode, out var codeError))
            {
                _errorText.Text = codeError ?? "6자리 페어링 코드를 입력해 주세요.";
                _errorText.Visibility = Visibility.Visible;
                for (var i = 0; i < 6; i++)
                {
                    if (string.IsNullOrWhiteSpace(_codeBoxes[i].Text))
                    {
                        _codeBoxes[i].Focus();
                        return;
                    }
                }
                _codeBoxes[0].Focus();
                _codeBoxes[0].SelectAll();
                return;
            }

            if (!TerminalCommandParameterRules.TryNormalizeAdbPort(port, out var normalizedPort, out var portError))
            {
                _errorText.Text = portError ?? "포트를 확인해 주세요.";
                _errorText.Visibility = Visibility.Visible;
                _portBox.Focus();
                _portBox.SelectAll();
                return;
            }

            Result = new AdbPromptResult(AdbPromptMode.Pair, normalizedPort, normalizedCode);
            DialogResult = true;
            Close();
        }
        else
        {
            if (!TerminalCommandParameterRules.TryNormalizeAdbPort(port, out var normalizedPort, out var portError))
            {
                _errorText.Text = portError ?? "포트를 확인해 주세요.";
                _errorText.Visibility = Visibility.Visible;
                _portBox.Focus();
                _portBox.SelectAll();
                return;
            }

            Result = new AdbPromptResult(AdbPromptMode.Connect, normalizedPort);
            DialogResult = true;
            Close();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }

    private static TextBlock CreateFieldLabel(string text, Brush foreground)
    {
        return new TextBlock
        {
            Text = text,
            Margin = new Thickness(0, 0, 0, 6),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = foreground
        };
    }

    private static void OnPortPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
    }

    private static void OnPortPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!Regex.IsMatch(text, @"^\d+$"))
        {
            e.CancelCommand();
        }
    }

    private Brush ResolveBrush(string key, Color fallbackColor)
    {
        if (TryFindResource(key) is Brush b1) return b1;
        if (Application.Current?.TryFindResource(key) is Brush b2) return b2;
        return new SolidColorBrush(fallbackColor);
    }

    private Style? ResolveStyle(string primaryKey, string alternativeKey)
    {
        if (TryFindResource(primaryKey) is Style s1) return s1;
        if (TryFindResource(alternativeKey) is Style s2) return s2;
        if (Application.Current?.TryFindResource(primaryKey) is Style s3) return s3;
        if (Application.Current?.TryFindResource(alternativeKey) is Style s4) return s4;
        return null;
    }
}
