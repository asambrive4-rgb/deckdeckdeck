// 역할: Android 원격설치 실행 시 최근 사용 프로젝트(최대 5개) 버튼, 전체 목록(더보기), 직접 폴더 선택 및 모듈 선택을 제공하는 모달 창입니다.
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases.Ports;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal sealed class RemoteInstallProjectPickerWindow : Window
{
    private readonly RemoteInstallProjectPromptRequest _request;
    private readonly CancellationTokenSource _adbScanCts = new();
    private readonly List<Button> _recentButtons = [];
    private readonly List<Button> _moduleButtons = [];

    private readonly Brush _appBgBrush;
    private readonly Brush _surfaceBrush;
    private readonly Brush _surfaceSoftBrush;
    private readonly Brush _accentBrush;
    private readonly Brush _inkBrush;
    private readonly Brush _secondaryTextBrush;
    private readonly Brush _mutedTextBrush;
    private readonly Brush _warningTextBrush;
    private readonly Brush _dangerBgBrush;
    private readonly Brush _dangerTextBrush;
    private readonly FontFamily _appFont;
    private readonly FontFamily _monoFont = new("Cascadia Code, Consolas");

    private readonly TextBlock _adbStatusText;
    private readonly Button _refreshAdbButton;
    private readonly Button _toggleMoreButton;
    private readonly Border _morePanel;
    private readonly TextBox _searchBox;
    private readonly TextBlock _searchPlaceholder;
    private readonly ListBox _allProjectsList;
    private readonly TextBlock _emptyAllProjectsText;
    private readonly Border _selectionDetailCard;
    private readonly TextBlock _selectedProjectTitleText;
    private readonly TextBlock _selectedProjectPathText;
    private readonly TextBlock _moduleHeaderText;
    private readonly WrapPanel _moduleChipsPanel;
    private readonly Border _warningBanner;
    private readonly TextBlock _warningBannerText;
    private readonly Button _confirmButton;

    private IReadOnlyList<RemoteInstallProjectCandidate> _filteredProjects;
    private string? _selectedProjectPath;
    private string? _selectedModuleName;
    private bool _isMoreExpanded;

    public RemoteInstallProjectPickerWindow(RemoteInstallProjectPromptRequest request)
    {
        _request = request;
        _filteredProjects = request.AllProjects;

        _appBgBrush = ResolveBrush("Deck.Brush.Background.App", "#F2EEE7");
        _surfaceBrush = ResolveBrush("Deck.Brush.Background.Surface", "#FCFAF6");
        _surfaceSoftBrush = ResolveBrush("Deck.Brush.Background.SurfaceSoft", "#E7E2D9");
        _accentBrush = ResolveBrush("Deck.Brush.Brand.Accent", "#C7D7E8");
        _inkBrush = ResolveBrush("Deck.Brush.Text.Primary", "#1E1B18");
        _secondaryTextBrush = ResolveBrush("Deck.Brush.Text.Secondary", "#5C554E");
        _mutedTextBrush = ResolveBrush("Deck.Brush.Text.Muted", "#8C8278");
        _warningTextBrush = ResolveBrush("Deck.Brush.Text.Warning", "#8C4E03");
        _dangerBgBrush = ResolveBrush("Deck.Brush.Background.DangerSoft", "#FADED7");
        _dangerTextBrush = ResolveBrush("Deck.Brush.Text.Danger", "#C2410C");
        _appFont = ResolveFont("Deck.Font.App");

        Title = "원격설치 프로젝트 선택";
        Width = 510;
        MinWidth = 420;
        MaxHeight = Math.Max(480, SystemParameters.WorkArea.Height - 48);
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var rootStack = new StackPanel();

        // Header
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerInfo = new StackPanel();
        headerInfo.Children.Add(new TextBlock
        {
            Text = "원격설치 프로젝트 선택",
            FontFamily = _appFont,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = _inkBrush
        });

        var kindLabel = RemoteInstallRules.GetDeviceKindKoreanLabel(request.TargetDeviceKind);
        var targetBadgeText = string.IsNullOrWhiteSpace(request.TargetDeviceFilter)
            ? $"{request.SlotTitle} · 대상: {kindLabel}"
            : $"{request.SlotTitle} · 대상: {kindLabel} ({request.TargetDeviceFilter})";
        headerInfo.Children.Add(new TextBlock
        {
            Text = targetBadgeText,
            FontFamily = _appFont,
            FontSize = 11.5,
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = _secondaryTextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        headerGrid.Children.Add(headerInfo);

        var closeButton = CreateIconCloseButton();
        Grid.SetColumn(closeButton, 1);
        headerGrid.Children.Add(closeButton);
        headerGrid.MouseLeftButtonDown += (_, _) => DragMove();
        rootStack.Children.Add(headerGrid);

        // Live ADB Status Banner
        var adbGrid = new Grid();
        adbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        adbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _adbStatusText = new TextBlock
        {
            Text = "연결된 ADB 기기 확인 중...",
            FontFamily = _appFont,
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = _secondaryTextBrush,
            Margin = new Thickness(0, 0, 8, 0)
        };
        adbGrid.Children.Add(_adbStatusText);

        _refreshAdbButton = CreateCompactSecondaryButton("새로고침 ↻", 26);
        _refreshAdbButton.Click += (_, _) => _ = RefreshConnectedDevicesAsync();
        Grid.SetColumn(_refreshAdbButton, 1);
        adbGrid.Children.Add(_refreshAdbButton);

        rootStack.Children.Add(new Border
        {
            Margin = new Thickness(0, 12, 0, 14),
            Padding = new Thickness(12, 8, 10, 8),
            CornerRadius = new CornerRadius(12),
            Background = _surfaceBrush,
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(1.5),
            Child = adbGrid
        });

        // Section 1: Recent 5 Projects Header + More Toggle
        var recentHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        recentHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        recentHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var recentTitlePanel = new StackPanel();
        recentTitlePanel.Children.Add(new TextBlock
        {
            Text = "최근 사용 프로젝트",
            FontFamily = _appFont,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = _inkBrush
        });
        recentTitlePanel.Children.Add(new TextBlock
        {
            Text = request.RecentProjects.Count > 0
                ? "숫자 1~5 또는 클릭으로 빠르게 선택 (단일 모듈이면 즉시 설치)"
                : "최근 실행 기록이 없습니다. 아래 전체 목록 또는 직접 찾기를 이용해 주세요.",
            FontFamily = _appFont,
            FontSize = 11,
            Margin = new Thickness(0, 2, 8, 0),
            Foreground = _secondaryTextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        recentHeaderGrid.Children.Add(recentTitlePanel);

        _toggleMoreButton = CreateCompactSecondaryButton(BuildMoreButtonLabel(expanded: false), 28);
        _toggleMoreButton.VerticalAlignment = VerticalAlignment.Center;
        _toggleMoreButton.Click += (_, _) => SetMoreExpanded(!_isMoreExpanded);
        Grid.SetColumn(_toggleMoreButton, 1);
        recentHeaderGrid.Children.Add(_toggleMoreButton);
        rootStack.Children.Add(recentHeaderGrid);

        // Recent 5 Buttons List
        if (request.RecentProjects.Count > 0)
        {
            var recentPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            for (var i = 0; i < request.RecentProjects.Count; i++)
            {
                var candidate = request.RecentProjects[i];
                var button = CreateRecentProjectButton(i + 1, candidate);
                _recentButtons.Add(button);
                recentPanel.Children.Add(button);
            }
            rootStack.Children.Add(recentPanel);
        }

        // Section 2: Collapsible Full Project List ("더보기")
        var moreStack = new StackPanel();

        var searchContainer = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        _searchBox = new TextBox
        {
            MinHeight = 36,
            Padding = new Thickness(10, 7, 10, 7),
            FontFamily = _appFont,
            FontSize = 12.5,
            Foreground = _inkBrush,
            Background = _appBgBrush,
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(1.5),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _searchBox.TextChanged += (_, _) => ApplyProjectSearchFilter();
        _searchPlaceholder = new TextBlock
        {
            Text = "프로젝트 이름 또는 경로 검색...",
            FontFamily = _appFont,
            FontSize = 12,
            Foreground = _mutedTextBrush,
            Margin = new Thickness(12, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        searchContainer.Children.Add(_searchBox);
        searchContainer.Children.Add(_searchPlaceholder);
        moreStack.Children.Add(searchContainer);

        _allProjectsList = new ListBox
        {
            MaxHeight = 205,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = CreateAllProjectDataTemplate()
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_allProjectsList, ScrollBarVisibility.Disabled);
        _allProjectsList.ItemContainerStyle = CreateCleanListBoxItemStyle();
        _allProjectsList.SelectionChanged += (_, _) =>
        {
            if (_allProjectsList.SelectedItem is RemoteInstallProjectCandidate candidate)
            {
                SelectProject(candidate.ProjectPath, autoConfirmIfSingleModule: false);
            }
        };
        _allProjectsList.MouseDoubleClick += (_, _) =>
        {
            if (_allProjectsList.SelectedItem is RemoteInstallProjectCandidate candidate)
            {
                SelectProject(candidate.ProjectPath, autoConfirmIfSingleModule: true);
            }
        };
        moreStack.Children.Add(_allProjectsList);

        _emptyAllProjectsText = new TextBlock
        {
            Visibility = Visibility.Collapsed,
            FontFamily = _appFont,
            FontSize = 11.5,
            Foreground = _secondaryTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(6, 10, 6, 10)
        };
        moreStack.Children.Add(_emptyAllProjectsText);

        _morePanel = new Border
        {
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(14),
            Background = _surfaceBrush,
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(1.5),
            Child = moreStack
        };
        rootStack.Children.Add(_morePanel);

        // Section 3: Browse custom folder
        var browseButton = CreateCompactSecondaryButton("📁 다른 프로젝트 폴더 직접 찾기...", 36);
        browseButton.Margin = new Thickness(0, 0, 0, 12);
        browseButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        browseButton.FontWeight = FontWeights.SemiBold;
        browseButton.Click += (_, _) => BrowseManualProjectFolder();
        rootStack.Children.Add(browseButton);

        // Section 4: Selected Project + Module Picker (shown once a project is selected or warning appears)
        var detailStack = new StackPanel();
        _selectedProjectTitleText = new TextBlock
        {
            FontFamily = _appFont,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = _inkBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _selectedProjectPathText = new TextBlock
        {
            FontFamily = _monoFont,
            FontSize = 10.5,
            Margin = new Thickness(0, 2, 0, 8),
            Foreground = _secondaryTextBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _moduleHeaderText = new TextBlock
        {
            Text = "빌드할 Android 앱 모듈 선택",
            FontFamily = _appFont,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
            Foreground = _inkBrush
        };
        _moduleChipsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };

        _warningBannerText = new TextBlock
        {
            FontFamily = _appFont,
            FontSize = 11.5,
            Foreground = _dangerTextBrush,
            TextWrapping = TextWrapping.Wrap
        };
        _warningBanner = new Border
        {
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(10, 7, 10, 7),
            CornerRadius = new CornerRadius(10),
            Background = _dangerBgBrush,
            BorderBrush = _dangerTextBrush,
            BorderThickness = new Thickness(1),
            Child = _warningBannerText
        };

        detailStack.Children.Add(_selectedProjectTitleText);
        detailStack.Children.Add(_selectedProjectPathText);
        detailStack.Children.Add(_moduleHeaderText);
        detailStack.Children.Add(_moduleChipsPanel);
        detailStack.Children.Add(_warningBanner);

        _selectionDetailCard = new Border
        {
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(14),
            Background = _surfaceBrush,
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(2),
            Child = detailStack
        };
        rootStack.Children.Add(_selectionDetailCard);

        // Footer CTA Row
        var footerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = CreateCompactSecondaryButton("취소 (Esc)", 36);
        cancelButton.Padding = new Thickness(16, 6, 16, 6);
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        cancelButton.Click += (_, _) => CancelDialog();

        _confirmButton = new Button
        {
            Content = "원격설치 시작",
            MinHeight = 36,
            Padding = new Thickness(18, 6, 18, 6),
            FontFamily = _appFont,
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = _inkBrush,
            Background = _accentBrush,
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(2.5),
            Cursor = System.Windows.Input.Cursors.Hand,
            IsEnabled = false
        };
        _confirmButton.Resources[typeof(Border)] = new Style(typeof(Border))
        {
            Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(12)) }
        };
        _confirmButton.Click += (_, _) => ConfirmSelection();

        footerPanel.Children.Add(cancelButton);
        footerPanel.Children.Add(_confirmButton);
        rootStack.Children.Add(footerPanel);

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = rootStack
        };

        Content = new Border
        {
            Background = _appBgBrush,
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(20),
            Child = scrollViewer
        };

        PopulateAllProjectsList();
        SetMoreExpanded(request.RecentProjects.Count == 0);

        Loaded += (_, _) =>
        {
            Activate();
            _ = RefreshConnectedDevicesAsync();
        };
        Closed += (_, _) => _adbScanCts.Cancel();
        PreviewKeyDown += HandlePreviewKeyDown;
    }

    public RemoteInstallProjectSelection? Selection { get; private set; }

    private void SetMoreExpanded(bool expanded)
    {
        _isMoreExpanded = expanded;
        _morePanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        _toggleMoreButton.Content = BuildMoreButtonLabel(expanded);
        if (expanded)
        {
            Dispatcher.BeginInvoke(() => _searchBox.Focus());
        }
    }

    private string BuildMoreButtonLabel(bool expanded)
    {
        var total = _request.AllProjects.Count;
        return expanded
            ? "접기 ▲"
            : total > 0
                ? $"더보기 (전체 {total}) ▼"
                : "전체 목록 ▼";
    }

    private void ApplyProjectSearchFilter()
    {
        var query = _searchBox.Text.Trim();
        _searchPlaceholder.Visibility = string.IsNullOrEmpty(_searchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        _filteredProjects = string.IsNullOrEmpty(query)
            ? _request.AllProjects
            : _request.AllProjects
                .Where(c => c.ProjectName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || c.ProjectPath.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        PopulateAllProjectsList();
    }

    private void PopulateAllProjectsList()
    {
        _allProjectsList.Items.Clear();

        if (_filteredProjects.Count == 0)
        {
            _allProjectsList.Visibility = Visibility.Collapsed;
            _emptyAllProjectsText.Text = _request.AllProjects.Count == 0
                ? "검색된 로컬 Android 프로젝트가 없습니다. '다른 프로젝트 폴더 직접 찾기'를 이용해 주세요."
                : "검색 조건과 일치하는 프로젝트가 없습니다.";
            _emptyAllProjectsText.Visibility = Visibility.Visible;
            return;
        }

        _emptyAllProjectsText.Visibility = Visibility.Collapsed;
        _allProjectsList.Visibility = Visibility.Visible;
        foreach (var candidate in _filteredProjects)
        {
            _allProjectsList.Items.Add(candidate);
        }
    }

    private DataTemplate CreateAllProjectDataTemplate()
    {
        var template = new DataTemplate(typeof(RemoteInstallProjectCandidate));
        var grid = new FrameworkElementFactory(typeof(Grid));
        grid.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 2, 2, 2));

        var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        grid.AppendChild(col0);
        grid.AppendChild(col1);

        var textStack = new FrameworkElementFactory(typeof(StackPanel));
        var nameBlock = new FrameworkElementFactory(typeof(TextBlock));
        nameBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(RemoteInstallProjectCandidate.ProjectName)));
        nameBlock.SetValue(TextBlock.FontFamilyProperty, _appFont);
        nameBlock.SetValue(TextBlock.FontSizeProperty, 12.5);
        nameBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        nameBlock.SetValue(TextBlock.ForegroundProperty, _inkBrush);
        nameBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

        var pathBlock = new FrameworkElementFactory(typeof(TextBlock));
        pathBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(RemoteInstallProjectCandidate.ProjectPath)));
        pathBlock.SetValue(TextBlock.FontFamilyProperty, _monoFont);
        pathBlock.SetValue(TextBlock.FontSizeProperty, 10.5);
        pathBlock.SetValue(TextBlock.ForegroundProperty, _secondaryTextBrush);
        pathBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        pathBlock.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 8, 0));

        textStack.AppendChild(nameBlock);
        textStack.AppendChild(pathBlock);
        grid.AppendChild(textStack);

        var badgeText = new FrameworkElementFactory(typeof(TextBlock));
        badgeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(RemoteInstallProjectCandidate.SourceBadge)));
        badgeText.SetValue(TextBlock.FontFamilyProperty, _appFont);
        badgeText.SetValue(TextBlock.FontSizeProperty, 10.0);
        badgeText.SetValue(TextBlock.ForegroundProperty, _secondaryTextBrush);

        var badgeBorder = new FrameworkElementFactory(typeof(Border));
        badgeBorder.SetValue(Border.BackgroundProperty, _surfaceSoftBrush);
        badgeBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        badgeBorder.SetValue(Border.PaddingProperty, new Thickness(7, 2, 7, 2));
        badgeBorder.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        badgeBorder.SetValue(Grid.ColumnProperty, 1);
        badgeBorder.AppendChild(badgeText);
        grid.AppendChild(badgeBorder);

        template.VisualTree = grid;
        return template;
    }

    private Button CreateRecentProjectButton(int number, RemoteInstallProjectCandidate candidate)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var numberBadge = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(8),
            Background = _surfaceSoftBrush,
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = number.ToString(),
                FontFamily = _appFont,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = _inkBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        grid.Children.Add(numberBadge);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock
        {
            Text = candidate.ProjectName,
            FontFamily = _appFont,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = _inkBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        textStack.Children.Add(new TextBlock
        {
            Text = candidate.ProjectPath,
            FontFamily = _monoFont,
            FontSize = 10.5,
            Margin = new Thickness(0, 1, 8, 0),
            Foreground = _secondaryTextBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);

        var arrowText = new TextBlock
        {
            Text = "선택 →",
            FontFamily = _appFont,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = _secondaryTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(arrowText, 2);
        grid.Children.Add(arrowText);

        var button = new Button
        {
            Tag = candidate.ProjectPath,
            MinHeight = 52,
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = _surfaceBrush,
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(1.5),
            Cursor = System.Windows.Input.Cursors.Hand,
            Content = grid
        };
        button.Resources[typeof(Border)] = new Style(typeof(Border))
        {
            Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(12)) }
        };
        button.Click += (_, _) => SelectProject(candidate.ProjectPath, autoConfirmIfSingleModule: true);
        return button;
    }

    private void BrowseManualProjectFolder()
    {
        var initialDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AndroidStudioProjects");

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "원격설치할 Android 프로젝트 폴더 선택",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(initialDir) ? initialDir : string.Empty
        };

        var wasTopmost = Topmost;
        Topmost = false;
        try
        {
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK
                || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                return;
            }

            SelectProject(dialog.SelectedPath, autoConfirmIfSingleModule: false);
        }
        finally
        {
            Topmost = wasTopmost;
        }
    }

    private void SelectProject(string projectPath, bool autoConfirmIfSingleModule)
    {
        var normalizedPath = RemoteInstallRules.NormalizeProjectPath(projectPath) ?? projectPath.Trim();
        _selectedProjectPath = normalizedPath;
        _selectedModuleName = null;

        HighlightRecentButton(normalizedPath);

        var displayName = Path.GetFileName(normalizedPath.TrimEnd('\\', '/'));
        _selectedProjectTitleText.Text = string.IsNullOrWhiteSpace(displayName) ? normalizedPath : displayName;
        _selectedProjectPathText.Text = normalizedPath;
        _selectionDetailCard.Visibility = Visibility.Visible;

        IReadOnlyList<AndroidAppModuleInfo> modules;
        try
        {
            modules = _request.ScanModules(normalizedPath);
        }
        catch (Exception ex)
        {
            ShowModuleError($"프로젝트 모듈 스캔 중 오류가 발생했습니다: {ex.Message}");
            return;
        }

        if (modules.Count == 0)
        {
            ShowModuleError("선택한 폴더에서 Android 앱 모듈(settings.gradle)을 찾지 못했습니다.");
            return;
        }

        if (modules.Count == 1 && autoConfirmIfSingleModule)
        {
            Selection = new RemoteInstallProjectSelection(normalizedPath, modules[0].ModuleName);
            DialogResult = true;
            Close();
            return;
        }

        _warningBanner.Visibility = Visibility.Collapsed;
        var moduleNames = modules.Select(static m => m.ModuleName).ToList();
        var recommended = RemoteInstallRules.RecommendModuleForDeviceKind(moduleNames, _request.TargetDeviceKind)
            ?? modules[0].ModuleName;

        _selectedModuleName = recommended;
        RenderModuleChips(modules, recommended);
        _confirmButton.IsEnabled = true;
        _confirmButton.Content = modules.Count == 1
            ? "원격설치 시작 (Enter)"
            : $"{_selectedModuleName} 설치 시작 (Enter)";
    }

    private void ShowModuleError(string message)
    {
        _moduleHeaderText.Visibility = Visibility.Collapsed;
        _moduleChipsPanel.Children.Clear();
        _moduleButtons.Clear();
        _selectedModuleName = null;
        _warningBannerText.Text = message;
        _warningBanner.Visibility = Visibility.Visible;
        _confirmButton.IsEnabled = false;
        _confirmButton.Content = "원격설치 시작";
    }

    private void RenderModuleChips(IReadOnlyList<AndroidAppModuleInfo> modules, string recommended)
    {
        _moduleHeaderText.Visibility = Visibility.Visible;
        _moduleHeaderText.Text = modules.Count == 1
            ? "감지된 Android 앱 모듈"
            : $"빌드할 Android 앱 모듈 선택 ({modules.Count}개 감지됨 · 추천 자동 선택)";

        _moduleChipsPanel.Children.Clear();
        _moduleButtons.Clear();

        foreach (var module in modules)
        {
            var isRecommended = string.Equals(module.ModuleName, recommended, StringComparison.OrdinalIgnoreCase);
            var label = isRecommended && modules.Count > 1
                ? $"{module.ModuleName} (추천)"
                : module.ModuleName;
            if (!string.IsNullOrWhiteSpace(module.ApplicationId))
            {
                label = $"{label} · {module.ApplicationId}";
            }

            var chip = new Button
            {
                Tag = module.ModuleName,
                Content = label,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(12, 4, 12, 4),
                FontFamily = _appFont,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _inkBrush,
                BorderBrush = _inkBrush,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            chip.Resources[typeof(Border)] = new Style(typeof(Border))
            {
                Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(10)) }
            };
            chip.Click += (_, _) =>
            {
                _selectedModuleName = module.ModuleName;
                UpdateModuleChipVisuals();
                _confirmButton.Content = $"{module.ModuleName} 설치 시작 (Enter)";
            };

            _moduleButtons.Add(chip);
            _moduleChipsPanel.Children.Add(chip);
        }

        UpdateModuleChipVisuals();
    }

    private void UpdateModuleChipVisuals()
    {
        foreach (var chip in _moduleButtons)
        {
            var isSelected = chip.Tag is string name
                && string.Equals(name, _selectedModuleName, StringComparison.OrdinalIgnoreCase);
            chip.Background = isSelected ? _accentBrush : _surfaceSoftBrush;
            chip.BorderThickness = new Thickness(isSelected ? 2 : 1);
        }
    }

    private void HighlightRecentButton(string selectedPath)
    {
        foreach (var button in _recentButtons)
        {
            var isSelected = button.Tag is string path
                && string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase);
            button.Background = isSelected ? _accentBrush : _surfaceBrush;
            button.BorderThickness = new Thickness(isSelected ? 2.5 : 1.5);
        }
    }

    private async Task RefreshConnectedDevicesAsync()
    {
        if (_request.ScanConnectedDevicesAsync is null)
        {
            _adbStatusText.Text = $"대상: {RemoteInstallRules.GetDeviceKindKoreanLabel(_request.TargetDeviceKind)}";
            _refreshAdbButton.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            _adbStatusText.Foreground = _secondaryTextBrush;
            _adbStatusText.Text = "연결된 ADB 기기 확인 중...";
            var devices = await _request.ScanConnectedDevicesAsync(_adbScanCts.Token);
            if (_adbScanCts.IsCancellationRequested)
            {
                return;
            }

            if (devices.Count == 0)
            {
                _adbStatusText.Foreground = _warningTextBrush;
                _adbStatusText.Text = "⚠ 현재 연결된 ADB 기기가 없습니다. (1·2·3 슬롯으로 무선 ADB 먼저 연결 권장)";
                return;
            }

            var matching = devices
                .Where(d => RemoteInstallRules.MatchesDeviceKind(_request.TargetDeviceKind, d.DeviceKind)
                    && RemoteInstallRules.MatchesTargetFilter(d.Serial, d.Model, d.IpAddress, _request.TargetDeviceFilter))
                .ToList();

            if (matching.Count > 0)
            {
                _adbStatusText.Foreground = _inkBrush;
                var summary = string.Join(", ", matching.Select(static d =>
                    $"{d.Model}({RemoteInstallRules.GetDeviceKindKoreanLabel(d.DeviceKind)})"));
                _adbStatusText.Text = $"✓ 설치 준비 완료 ({matching.Count}대): {summary}";
            }
            else
            {
                _adbStatusText.Foreground = _warningTextBrush;
                var targetLabel = RemoteInstallRules.GetDeviceKindKoreanLabel(_request.TargetDeviceKind);
                _adbStatusText.Text = $"⚠ 대상 기기({targetLabel}) 미연결 · 현재 연결됨: {string.Join(", ", devices.Select(static d => d.Model))}";
            }
        }
        catch (OperationCanceledException)
        {
            // Window closed
        }
        catch (Exception ex)
        {
            _adbStatusText.Foreground = _secondaryTextBrush;
            _adbStatusText.Text = $"ADB 상태 확인 실패: {ex.Message}";
        }
    }

    private void ConfirmSelection()
    {
        if (string.IsNullOrWhiteSpace(_selectedProjectPath) || string.IsNullOrWhiteSpace(_selectedModuleName))
        {
            return;
        }

        Selection = new RemoteInstallProjectSelection(_selectedProjectPath, _selectedModuleName);
        DialogResult = true;
        Close();
    }

    private void CancelDialog()
    {
        Selection = null;
        DialogResult = false;
        Close();
    }

    private void HandlePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelDialog();
            return;
        }

        if (e.Key == Key.Enter && _confirmButton.IsEnabled && !_searchBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            ConfirmSelection();
            return;
        }

        if (_searchBox.IsKeyboardFocusWithin)
        {
            return;
        }

        var numericIndex = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            _ => -1
        };

        if (numericIndex >= 0 && numericIndex < _request.RecentProjects.Count)
        {
            e.Handled = true;
            SelectProject(_request.RecentProjects[numericIndex].ProjectPath, autoConfirmIfSingleModule: true);
        }
    }

    private Button CreateCompactSecondaryButton(string text, double minHeight)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = minHeight,
            Padding = new Thickness(10, 4, 10, 4),
            FontFamily = _appFont,
            FontSize = 11.5,
            Foreground = _inkBrush,
            Background = _surfaceBrush,
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(1.5),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Resources[typeof(Border)] = new Style(typeof(Border))
        {
            Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(10)) }
        };
        return button;
    }

    private Button CreateIconCloseButton()
    {
        var button = new Button
        {
            Content = "✕",
            Width = 30,
            Height = 30,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = _secondaryTextBrush,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "닫기 (Esc)"
        };
        button.Click += (_, _) => CancelDialog();
        return button;
    }

    private Style CreateCleanListBoxItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 7, 8, 7)));
        style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 0, 4)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, _appBgBrush));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, _inkBrush));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
        style.Triggers.Add(new Trigger
        {
            Property = ListBoxItem.IsSelectedProperty,
            Value = true,
            Setters =
            {
                new Setter(Control.BackgroundProperty, _accentBrush),
                new Setter(Control.BorderThicknessProperty, new Thickness(2))
            }
        });
        return style;
    }

    private static Brush ResolveBrush(string key, string fallbackHex)
    {
        if (System.Windows.Application.Current?.TryFindResource(key) is Brush found)
        {
            return found;
        }

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
    }

    private static FontFamily ResolveFont(string key)
    {
        if (System.Windows.Application.Current?.TryFindResource(key) is FontFamily found)
        {
            return found;
        }

        return new FontFamily("Segoe UI");
    }
}
