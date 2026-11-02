// 역할: 사용자가 윈도우에 설치된 앱이나 페어링된 블루투스 기기 등을 검색하고 선택할 수 있는 모달 대화상자 창입니다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal sealed class InstalledAppPickerWindow : Window
{
    private readonly TextBox _searchBox;
    private readonly ListBox _listBox;
    private readonly TextBlock _statusTextBlock;
    private readonly Button _selectButton;
    private readonly string _emptyMessage;
    private IReadOnlyList<InstalledAppEntry> _allApps = Array.Empty<InstalledAppEntry>();
    private bool _isLoading;

    public InstalledAppEntry? SelectedApp { get; private set; }

    public InstalledAppPickerWindow(
        IReadOnlyList<InstalledAppEntry>? preloadedApps = null,
        string windowTitle = "설치된 앱 선택",
        string windowSubtitle = "Microsoft Store 앱이나 윈도우에 설치된 프로그램을 바로 선택하세요.",
        string searchPlaceholder = "앱 이름 또는 키워드 검색...",
        string emptyMessage = "설치된 앱을 찾을 수 없습니다.")
    {
        _emptyMessage = emptyMessage;
        Title = windowTitle;
        Width = 460;
        Height = 540;
        MinWidth = 380;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = true;

        var rootBorder = new Border
        {
            CornerRadius = new CornerRadius(24),
            BorderThickness = new Thickness(2.5),
            Background = ResolveBrush("Deck.Brush.Background.App", "BackgroundBrush", Color.FromRgb(0xF2, 0xEE, 0xE7)),
            BorderBrush = ResolveBrush("Deck.Brush.Line.Soft", "BorderBrush", Color.FromRgb(0x1E, 0x1B, 0x18)),
            Padding = new Thickness(22),
            SnapsToDevicePixels = true,
            Effect = new DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 4,
                Opacity = 0.18,
                Color = Color.FromRgb(0x1E, 0x1B, 0x18)
            }
        };

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Row 3에서 사용될 선택/취소 버튼 미리 인스턴스화
        var cancelButton = new Button
        {
            Content = "취소",
            MinWidth = 88,
            MinHeight = 36,
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
            cancelButton.Background = ResolveBrush("Deck.Brush.Background.Surface", "SurfaceBrush", Color.FromRgb(0xFC, 0xFA, 0xF6));
            cancelButton.BorderBrush = ResolveBrush("Deck.Brush.Line.Soft", "BorderBrush", Color.FromRgb(0x1E, 0x1B, 0x18));
        }
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        _selectButton = new Button
        {
            Content = "선택",
            MinWidth = 88,
            MinHeight = 36,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true,
            IsEnabled = false,
            Cursor = Cursors.Hand
        };
        var primaryStyle = ResolveStyle("Deck.PrimaryButtonStyle", "PrimaryButtonStyle");
        if (primaryStyle is not null)
        {
            _selectButton.Style = primaryStyle;
        }
        else
        {
            _selectButton.Padding = new Thickness(14, 6, 14, 6);
            _selectButton.Background = ResolveBrush("Deck.Brush.Brand.Accent", "AccentBrush", Color.FromRgb(0xC7, 0xD7, 0xE8));
            _selectButton.BorderBrush = ResolveBrush("Deck.Brush.Line.Soft", "BorderBrush", Color.FromRgb(0x1E, 0x1B, 0x18));
        }
        _selectButton.Click += (_, _) => Confirm();

        // Row 0: 헤더
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        headerGrid.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleText = new TextBlock
        {
            Text = windowTitle,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = ResolveBrush("Deck.Brush.Text.Primary", "TextPrimaryBrush", Color.FromRgb(0x1E, 0x1B, 0x18))
        };
        var subtitleText = new TextBlock
        {
            Text = windowSubtitle,
            FontSize = 12.5,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = ResolveBrush("Deck.Brush.Text.Secondary", "TextSecondaryBrush", Color.FromRgb(0x5C, 0x55, 0x4E)),
            TextWrapping = TextWrapping.Wrap
        };
        titleStack.Children.Add(titleText);
        titleStack.Children.Add(subtitleText);

        var closeButton = new Button
        {
            Content = "✕",
            Width = 28,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ResolveBrush("Deck.Brush.Text.Secondary", "TextSecondaryBrush", Color.FromRgb(0x5C, 0x55, 0x4E)),
            Focusable = false,
            ToolTip = "닫기"
        };
        closeButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        headerGrid.Children.Add(titleStack);
        headerGrid.Children.Add(closeButton);
        Grid.SetRow(headerGrid, 0);
        mainGrid.Children.Add(headerGrid);

        // Row 1: 검색 입력창
        var searchContainer = new Grid { Margin = new Thickness(0, 0, 0, 12) };

        _searchBox = new TextBox
        {
            MinHeight = 38,
            FontSize = 13.5,
            Padding = new Thickness(10, 8, 10, 8),
            Background = ResolveBrush("Deck.Brush.Background.Surface", "SurfaceBrush", Color.FromRgb(0xFC, 0xFA, 0xF6)),
            BorderBrush = ResolveBrush("Deck.Brush.Line.Soft", "BorderBrush", Color.FromRgb(0x1E, 0x1B, 0x18)),
            BorderThickness = new Thickness(2),
            Foreground = ResolveBrush("Deck.Brush.Text.Primary", "TextPrimaryBrush", Color.FromRgb(0x1E, 0x1B, 0x18)),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var placeholder = new TextBlock
        {
            Text = searchPlaceholder,
            FontSize = 13.5,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Foreground = ResolveBrush("Deck.Brush.Text.Muted", "TextSecondaryBrush", Color.FromRgb(0x8C, 0x82, 0x78))
        };

        _searchBox.TextChanged += (_, _) =>
        {
            placeholder.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ApplyFilter(_searchBox.Text);
        };

        _searchBox.PreviewKeyDown += OnSearchBoxPreviewKeyDown;

        searchContainer.Children.Add(_searchBox);
        searchContainer.Children.Add(placeholder);
        Grid.SetRow(searchContainer, 1);
        mainGrid.Children.Add(searchContainer);

        // Row 2: 앱 목록
        var listContainer = new Grid();

        var listOuterBorder = new Border
        {
            Background = ResolveBrush("Deck.Brush.Background.Surface", "SurfaceBrush", Color.FromRgb(0xFC, 0xFA, 0xF6)),
            BorderBrush = ResolveBrush("Deck.Brush.Line.Soft", "BorderBrush", Color.FromRgb(0x1E, 0x1B, 0x18)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(3)
        };

        _listBox = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            SelectionMode = SelectionMode.Single
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_listBox, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(_listBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetCanContentScroll(_listBox, true);
        VirtualizingStackPanel.SetIsVirtualizing(_listBox, true);
        VirtualizingStackPanel.SetVirtualizationMode(_listBox, VirtualizationMode.Recycling);

        // DataTemplate
        var itemTemplate = new DataTemplate(typeof(InstalledAppEntry));
        var itemFactory = new FrameworkElementFactory(typeof(StackPanel));
        itemFactory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);

        var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
        nameFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(InstalledAppEntry.DisplayName)));
        nameFactory.SetValue(TextBlock.FontSizeProperty, 13.5);
        nameFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        nameFactory.SetValue(TextBlock.ForegroundProperty, ResolveBrush("Deck.Brush.Text.Primary", "TextPrimaryBrush", Color.FromRgb(0x1E, 0x1B, 0x18)));
        nameFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

        var subFactory = new FrameworkElementFactory(typeof(TextBlock));
        subFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(InstalledAppEntry.Subtitle)));
        subFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        subFactory.SetValue(TextBlock.ForegroundProperty, ResolveBrush("Deck.Brush.Text.Secondary", "TextSecondaryBrush", Color.FromRgb(0x5C, 0x55, 0x4E)));
        subFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        subFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 0));

        itemFactory.AppendChild(nameFactory);
        itemFactory.AppendChild(subFactory);
        itemTemplate.VisualTree = itemFactory;
        _listBox.ItemTemplate = itemTemplate;

        // ItemContainerStyle
        var containerStyle = new Style(typeof(ListBoxItem));
        containerStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(2)));
        containerStyle.Setters.Add(new Setter(ListBoxItem.CursorProperty, Cursors.Hand));

        var itemControlTemplate = new ControlTemplate(typeof(ListBoxItem));
        var itemBorderFactory = new FrameworkElementFactory(typeof(Border));
        itemBorderFactory.Name = "ItemBorder";
        itemBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        itemBorderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
        itemBorderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        itemBorderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);

        var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        itemBorderFactory.AppendChild(cpFactory);
        itemControlTemplate.VisualTree = itemBorderFactory;

        var mouseOverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ResolveBrush("Deck.Brush.Background.SurfaceSoft", "SurfaceSoftBrush", Color.FromRgb(0xE7, 0xE2, 0xD9)), "ItemBorder"));

        var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ResolveBrush("Deck.Brush.Brand.Accent", "AccentBrush", Color.FromRgb(0xC7, 0xD7, 0xE8)), "ItemBorder"));

        itemControlTemplate.Triggers.Add(mouseOverTrigger);
        itemControlTemplate.Triggers.Add(selectedTrigger);
        containerStyle.Setters.Add(new Setter(ListBoxItem.TemplateProperty, itemControlTemplate));
        _listBox.ItemContainerStyle = containerStyle;

        _listBox.SelectionChanged += (_, _) =>
        {
            _selectButton.IsEnabled = _listBox.SelectedItem is not null;
        };

        _listBox.MouseDoubleClick += (_, _) =>
        {
            if (_listBox.SelectedItem is not null)
            {
                Confirm();
            }
        };

        _listBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Confirm();
                e.Handled = true;
            }
        };

        listOuterBorder.Child = _listBox;
        listContainer.Children.Add(listOuterBorder);

        _statusTextBlock = new TextBlock
        {
            Text = "설치된 앱 목록을 불러오는 중...",
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("Deck.Brush.Text.Muted", "TextSecondaryBrush", Color.FromRgb(0x8C, 0x82, 0x78)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        listContainer.Children.Add(_statusTextBlock);

        Grid.SetRow(listContainer, 2);
        mainGrid.Children.Add(listContainer);

        // Row 3: 하단 버튼 영역
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(_selectButton);
        Grid.SetRow(buttonRow, 3);
        mainGrid.Children.Add(buttonRow);

        rootBorder.Child = mainGrid;
        Content = rootBorder;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        };

        Loaded += (_, _) =>
        {
            Activate();
            _searchBox.Focus();

            if (preloadedApps is not null)
            {
                _allApps = preloadedApps;
                _isLoading = false;
                ApplyFilter(_searchBox.Text);
            }
            else
            {
                _isLoading = true;
                _statusTextBlock.Text = "설치된 앱 목록을 불러오는 중...";
                _statusTextBlock.Visibility = Visibility.Visible;

                Task.Run(() => WindowsInstalledAppCatalog.GetInstalledApps())
                    .ContinueWith(task =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _isLoading = false;
                            _allApps = task.IsCompletedSuccessfully ? task.Result : Array.Empty<InstalledAppEntry>();
                            ApplyFilter(_searchBox.Text);
                        });
                    });
            }
        };
    }

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (_listBox.Items.Count > 0)
            {
                if (_listBox.SelectedIndex < 0)
                {
                    _listBox.SelectedIndex = 0;
                }

                _listBox.Focus();
                if (_listBox.SelectedItem is not null)
                {
                    _listBox.ScrollIntoView(_listBox.SelectedItem);
                }

                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            Confirm();
            e.Handled = true;
        }
    }

    private void ApplyFilter(string? query)
    {
        if (_isLoading)
        {
            _statusTextBlock.Text = "설치된 앱 목록을 불러오는 중...";
            _statusTextBlock.Visibility = Visibility.Visible;
            _listBox.ItemsSource = null;
            return;
        }

        var trimmed = query?.Trim();
        IReadOnlyList<InstalledAppEntry> filtered;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            filtered = _allApps;
        }
        else
        {
            filtered = _allApps
                .Where(app =>
                    app.DisplayName.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase) ||
                    app.Subtitle.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        }

        _listBox.ItemsSource = filtered;

        if (filtered.Count == 0)
        {
            _statusTextBlock.Text = _allApps.Count == 0 ? _emptyMessage : "검색 결과가 없습니다.";
            _statusTextBlock.Visibility = Visibility.Visible;
            _selectButton.IsEnabled = false;
        }
        else
        {
            _statusTextBlock.Visibility = Visibility.Collapsed;
            _listBox.SelectedIndex = 0;
            _selectButton.IsEnabled = true;
        }
    }

    private void Confirm()
    {
        if (_listBox.SelectedItem is InstalledAppEntry entry)
        {
            SelectedApp = entry;
            DialogResult = true;
            Close();
        }
        else if (_listBox.Items.Count > 0 && _listBox.Items[0] is InstalledAppEntry first)
        {
            SelectedApp = first;
            DialogResult = true;
            Close();
        }
    }

    private Brush ResolveBrush(string primaryKey, string alternativeKey, Color fallbackColor)
    {
        if (TryFindResource(primaryKey) is Brush b1) return b1;
        if (TryFindResource(alternativeKey) is Brush b2) return b2;
        if (Application.Current?.TryFindResource(primaryKey) is Brush b3) return b3;
        if (Application.Current?.TryFindResource(alternativeKey) is Brush b4) return b4;
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
