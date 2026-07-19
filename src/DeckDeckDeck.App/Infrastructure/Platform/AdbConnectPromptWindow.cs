using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeckDeckDeck.App.Domain;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal sealed class AdbConnectPromptWindow : Window
{
    private readonly TextBox _portBox;
    private readonly TextBlock _errorText;

    public AdbConnectPromptWindow(string title, string fixedIp)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "ADB 연결" : title;
        Width = 380;
        MinWidth = 320;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        Topmost = true;
        Background = TryFindBrush("Deck.Brush.Background.App")
            ?? new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xD1));

        var root = new DockPanel { Margin = new Thickness(20) };

        var buttons = CreateButtonRow();
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "포트 숫자만 입력하면 설정에 저장한 IP로 adb connect 합니다.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14),
            Foreground = TryFindBrush("Deck.Brush.Text.Secondary")
                ?? new SolidColorBrush(Color.FromRgb(0x7D, 0x5E, 0x3E))
        });

        content.Children.Add(CreateFieldLabel("IP (설정값)"));
        content.Children.Add(new TextBlock
        {
            Text = fixedIp,
            Margin = new Thickness(0, 0, 0, 12),
            FontFamily = new FontFamily("Cascadia Code, Consolas, Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            Foreground = TryFindBrush("Deck.Brush.Text.Primary")
                ?? new SolidColorBrush(Color.FromRgb(0x3B, 0x2A, 0x1D))
        });

        content.Children.Add(CreateFieldLabel("포트"));
        _portBox = CreateInputBox("예: 12345");
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
        content.Children.Add(_portBox);

        _errorText = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = TryFindBrush("Deck.Brush.Text.Danger")
                ?? new SolidColorBrush(Color.FromRgb(0xB4, 0x23, 0x18)),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        content.Children.Add(_errorText);

        root.Children.Add(content);
        Content = root;

        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) =>
        {
            Activate();
            _portBox.Focus();
        };
    }

    public string Port => _portBox.Text ?? string.Empty;

    private StackPanel CreateButtonRow()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var cancel = new Button
        {
            Content = "취소",
            MinWidth = 88,
            Height = 36,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true,
            Padding = new Thickness(12, 6, 12, 6)
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        var ok = new Button
        {
            Content = "연결",
            MinWidth = 88,
            Height = 36,
            IsDefault = true,
            Padding = new Thickness(12, 6, 12, 6)
        };
        ok.Click += (_, _) => Confirm();

        row.Children.Add(cancel);
        row.Children.Add(ok);
        return row;
    }

    private void Confirm()
    {
        if (!TerminalCommandParameterRules.TryNormalizeAdbPort(
                Port,
                out _,
                out var errorMessage))
        {
            _errorText.Text = errorMessage ?? "포트를 확인해 주세요.";
            _errorText.Visibility = Visibility.Visible;
            return;
        }

        DialogResult = true;
        Close();
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

    private static TextBlock CreateFieldLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Margin = new Thickness(0, 0, 0, 6),
            FontWeight = FontWeights.SemiBold,
            Foreground = TryFindBrush("Deck.Brush.Text.Primary")
                ?? new SolidColorBrush(Color.FromRgb(0x3B, 0x2A, 0x1D))
        };
    }

    private static TextBox CreateInputBox(string toolTip)
    {
        return new TextBox
        {
            Height = 36,
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 14,
            FontFamily = new FontFamily("Cascadia Code, Consolas, Segoe UI"),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = toolTip
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

    private static Brush? TryFindBrush(string key)
    {
        return Application.Current?.TryFindResource(key) as Brush;
    }
}
