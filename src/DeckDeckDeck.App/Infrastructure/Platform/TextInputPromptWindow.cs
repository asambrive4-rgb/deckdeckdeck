using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal sealed class TextInputPromptWindow : Window
{
    private readonly Dictionary<string, TextBox> _fields = new(StringComparer.Ordinal);

    public TextInputPromptWindow(
        string title,
        string message,
        IReadOnlyList<string> fieldNames,
        IReadOnlyDictionary<string, string>? initialValues)
    {
        Title = title;
        Width = 420;
        MinWidth = 360;
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
        if (!string.IsNullOrWhiteSpace(message))
        {
            content.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14),
                Foreground = TryFindBrush("Deck.Brush.Text.Secondary")
                    ?? new SolidColorBrush(Color.FromRgb(0x7D, 0x5E, 0x3E))
            });
        }

        TextBox? firstBox = null;
        TextBox? lastBox = null;
        foreach (var fieldName in fieldNames)
        {
            content.Children.Add(new TextBlock
            {
                Text = fieldName,
                Margin = new Thickness(0, 0, 0, 6),
                FontWeight = FontWeights.SemiBold,
                Foreground = TryFindBrush("Deck.Brush.Text.Primary")
                    ?? new SolidColorBrush(Color.FromRgb(0x3B, 0x2A, 0x1D))
            });

            var box = new TextBox
            {
                Height = 36,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 14,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            if (initialValues is not null
                && initialValues.TryGetValue(fieldName, out var initial)
                && !string.IsNullOrEmpty(initial))
            {
                box.Text = initial;
                box.SelectAll();
            }

            _fields[fieldName] = box;
            content.Children.Add(box);
            firstBox ??= box;
            lastBox = box;
        }

        root.Children.Add(content);
        Content = root;

        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) =>
        {
            Activate();
            firstBox?.Focus();
            if (firstBox is not null && !string.IsNullOrEmpty(firstBox.Text))
            {
                firstBox.SelectAll();
            }
        };

        // Last field Enter confirms; intermediate fields move focus downward via default Tab.
        if (lastBox is not null)
        {
            lastBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Confirm();
                    e.Handled = true;
                }
            };
        }
    }

    public IReadOnlyDictionary<string, string> CollectValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, box) in _fields)
        {
            values[name] = box.Text ?? string.Empty;
        }

        return values;
    }

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
            Content = "실행",
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

    private static Brush? TryFindBrush(string key)
    {
        return Application.Current?.TryFindResource(key) as Brush;
    }
}
