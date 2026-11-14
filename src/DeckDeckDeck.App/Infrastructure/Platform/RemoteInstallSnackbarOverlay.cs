// 역할: Android 원격설치(스캔·빌드·설치·실행) 진행 상황과 경과 시간, 취소·재설치·로그 확인 버튼을 포커스를 빼앗지 않는 플로팅 스낵바로 표시합니다.
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal interface IRemoteInstallFeedbackNotifier
{
    void NotifyProgress(
        string badgeLabel,
        string title,
        string subtitle,
        Action? onCancel = null);

    void NotifyCompleted(
        string title,
        string subtitle,
        TimeSpan elapsed);

    void NotifyFailed(
        string title,
        string subtitle,
        string fullLog,
        Action? onRetry = null,
        Action? onUninstallAndReinstall = null);

    AndroidConnectedDeviceInfo? PromptSelectDevice(
        string slotTitle,
        RemoteInstallDeviceKind targetKind,
        IReadOnlyList<AndroidConnectedDeviceInfo> candidates);
}

internal sealed class WpfRemoteInstallSnackbarNotifier : IRemoteInstallFeedbackNotifier
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private static readonly SolidColorBrush SurfaceBrush = CreateFrozenBrush("#FCFAF6");
    private static readonly SolidColorBrush SurfaceSoftBrush = CreateFrozenBrush("#F4EFE6");
    private static readonly SolidColorBrush BorderDarkBrush = CreateFrozenBrush("#1E1B18");
    private static readonly SolidColorBrush SubTextBrush = CreateFrozenBrush("#5C554E");
    private static readonly SolidColorBrush SkyAccentBrush = CreateFrozenBrush("#C7D7E8");
    private static readonly SolidColorBrush MintSuccessBrush = CreateFrozenBrush("#BEEAD8");
    private static readonly SolidColorBrush PeachAlertBrush = CreateFrozenBrush("#FFC6A8");

    private readonly Dispatcher? _dispatcher;
    private Window? _window;
    private Border? _badgeBorder;
    private TextBlock? _badgeText;
    private TextBlock? _titleText;
    private TextBlock? _subtitleText;
    private StackPanel? _actionPanel;
    private DispatcherTimer? _closeTimer;
    private DispatcherTimer? _elapsedTimer;
    private DateTime _progressStartedUtc;
    private string _currentBadgePrefix = "빌드";
    private int _operationId;

    public WpfRemoteInstallSnackbarNotifier(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher;
    }

    public void NotifyProgress(
        string badgeLabel,
        string title,
        string subtitle,
        Action? onCancel = null)
    {
        if (_dispatcher == null || _dispatcher.HasShutdownStarted)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                EnsureWindow();
                if (_window == null)
                {
                    return;
                }

                _closeTimer?.Stop();
                if (_elapsedTimer == null || !_elapsedTimer.IsEnabled)
                {
                    _progressStartedUtc = DateTime.UtcNow;
                    StartElapsedTimer();
                }

                _currentBadgePrefix = badgeLabel;
                if (_badgeBorder != null)
                {
                    _badgeBorder.Background = SkyAccentBrush;
                }

                UpdateElapsedBadgeText();

                if (_titleText != null)
                {
                    _titleText.Text = title;
                }

                if (_subtitleText != null)
                {
                    _subtitleText.Text = subtitle;
                }

                RebuildActionButtons(
                    onCancel != null
                        ? [("취소", () =>
                        {
                            onCancel();
                            _window?.Hide();
                        }, false)]
                        : []);

                UpdatePosition();
                if (!_window.IsVisible)
                {
                    _window.Show();
                }
            }
            catch
            {
                // Suppress UI exceptions during unexpected WPF teardown
            }
        });
    }

    public void NotifyCompleted(
        string title,
        string subtitle,
        TimeSpan elapsed)
    {
        if (_dispatcher == null || _dispatcher.HasShutdownStarted)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                EnsureWindow();
                if (_window == null)
                {
                    return;
                }

                var currentOp = ++_operationId;
                _elapsedTimer?.Stop();
                _closeTimer?.Stop();

                if (_badgeBorder != null)
                {
                    _badgeBorder.Background = MintSuccessBrush;
                }

                if (_badgeText != null)
                {
                    _badgeText.Text = $"완료 {FormatElapsed(elapsed)}";
                }

                if (_titleText != null)
                {
                    _titleText.Text = title;
                }

                if (_subtitleText != null)
                {
                    _subtitleText.Text = subtitle;
                }

                RebuildActionButtons([]);
                UpdatePosition();

                if (!_window.IsVisible)
                {
                    _window.Show();
                }

                _closeTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(3500)
                };
                _closeTimer.Tick += (_, _) =>
                {
                    _closeTimer?.Stop();
                    if (_operationId == currentOp)
                    {
                        _window?.Hide();
                    }
                };
                _closeTimer.Start();
            }
            catch
            {
                // Suppress UI exceptions during unexpected WPF teardown
            }
        });
    }

    public void NotifyFailed(
        string title,
        string subtitle,
        string fullLog,
        Action? onRetry = null,
        Action? onUninstallAndReinstall = null)
    {
        if (_dispatcher == null || _dispatcher.HasShutdownStarted)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                EnsureWindow();
                if (_window == null)
                {
                    return;
                }

                var currentOp = ++_operationId;
                _elapsedTimer?.Stop();
                _closeTimer?.Stop();

                if (_badgeBorder != null)
                {
                    _badgeBorder.Background = PeachAlertBrush;
                }

                if (_badgeText != null)
                {
                    _badgeText.Text = "오류 !";
                }

                if (_titleText != null)
                {
                    _titleText.Text = title;
                }

                if (_subtitleText != null)
                {
                    _subtitleText.Text = subtitle;
                }

                var actions = new List<(string Label, Action Callback, bool IsAccent)>();
                if (onUninstallAndReinstall != null)
                {
                    actions.Add(("기존 앱 삭제 후 재설치", onUninstallAndReinstall, true));
                }
                else if (onRetry != null)
                {
                    actions.Add(("재시도", onRetry, true));
                }

                if (!string.IsNullOrWhiteSpace(fullLog))
                {
                    actions.Add(("로그 보기", () => ShowLogWindow(title, fullLog), false));
                }

                actions.Add(("닫기", () => _window?.Hide(), false));
                RebuildActionButtons(actions);

                UpdatePosition();
                if (!_window.IsVisible)
                {
                    _window.Show();
                }

                // Auto-hide after 18 seconds if untouched so error banner doesn't stay forever
                _closeTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
                {
                    Interval = TimeSpan.FromSeconds(18)
                };
                _closeTimer.Tick += (_, _) =>
                {
                    _closeTimer?.Stop();
                    if (_operationId == currentOp)
                    {
                        _window?.Hide();
                    }
                };
                _closeTimer.Start();
            }
            catch
            {
                // Suppress UI exceptions during unexpected WPF teardown
            }
        });
    }

    public AndroidConnectedDeviceInfo? PromptSelectDevice(
        string slotTitle,
        RemoteInstallDeviceKind targetKind,
        IReadOnlyList<AndroidConnectedDeviceInfo> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1 || _dispatcher == null || _dispatcher.HasShutdownStarted)
        {
            return candidates[0];
        }

        return _dispatcher.Invoke(() => ShowDevicePickerModal(slotTitle, targetKind, candidates));
    }

    private void StartElapsedTimer()
    {
        if (_dispatcher == null)
        {
            return;
        }

        _elapsedTimer?.Stop();
        _elapsedTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _elapsedTimer.Tick += (_, _) => UpdateElapsedBadgeText();
        _elapsedTimer.Start();
    }

    private void UpdateElapsedBadgeText()
    {
        if (_badgeText == null)
        {
            return;
        }

        var elapsed = DateTime.UtcNow - _progressStartedUtc;
        _badgeText.Text = $"{_currentBadgePrefix} {FormatElapsed(elapsed)}";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        var totalSeconds = Math.Max(0, (int)Math.Round(elapsed.TotalSeconds));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:D2}";
    }

    private void RebuildActionButtons(
        IReadOnlyList<(string Label, Action Callback, bool IsAccent)> actions)
    {
        if (_actionPanel == null)
        {
            return;
        }

        _actionPanel.Children.Clear();
        if (actions.Count == 0)
        {
            _actionPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _actionPanel.Visibility = Visibility.Visible;
        foreach (var (label, callback, isAccent) in actions)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = BorderDarkBrush,
                Background = isAccent ? SkyAccentBrush : SurfaceSoftBrush,
                BorderBrush = BorderDarkBrush,
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(9, 3, 9, 3),
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                Focusable = false
            };

            btn.Click += (_, e) =>
            {
                e.Handled = true;
                callback();
            };

            _actionPanel.Children.Add(btn);
        }
    }

    private void EnsureWindow()
    {
        if (_window != null)
        {
            return;
        }

        _badgeText = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BorderDarkBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        _badgeBorder = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            BorderBrush = BorderDarkBrush,
            BorderThickness = new Thickness(1.5),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _badgeText
        };

        _titleText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = BorderDarkBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _subtitleText = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.Medium,
            Foreground = SubTextBrush,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        textStack.Children.Add(_titleText);
        textStack.Children.Add(_subtitleText);

        _actionPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Visibility = Visibility.Collapsed
        };

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(_badgeBorder, 0);
        Grid.SetColumn(textStack, 2);
        Grid.SetColumn(_actionPanel, 3);

        contentGrid.Children.Add(_badgeBorder);
        contentGrid.Children.Add(textStack);
        contentGrid.Children.Add(_actionPanel);

        var outerBorder = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = BorderDarkBrush,
            BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(16, 10, 16, 10),
            MinWidth = 290,
            MaxWidth = 600,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Child = contentGrid
        };

        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = outerBorder
        };

        _window.SizeChanged += (_, _) => UpdatePosition();
        _window.Closed += (_, _) =>
        {
            _elapsedTimer?.Stop();
            _closeTimer?.Stop();
            _window = null;
        };

        var helper = new WindowInteropHelper(_window);
        var hwnd = helper.EnsureHandle();
        if (hwnd != IntPtr.Zero)
        {
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }
    }

    private void UpdatePosition()
    {
        if (_window == null)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var width = _window.ActualWidth;
        var height = _window.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            _window.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            width = _window.DesiredSize.Width;
            height = _window.DesiredSize.Height;
        }

        if (width <= 0)
        {
            width = 320;
        }

        if (height <= 0)
        {
            height = 60;
        }

        _window.Left = workArea.Left + (workArea.Width - width) / 2;
        _window.Top = workArea.Bottom - height - 28;
    }

    private static void ShowLogWindow(string title, string fullLog)
    {
        var window = new Window
        {
            Title = $"{title} - 원격설치 상세 로그",
            Width = 560,
            Height = 400,
            MinWidth = 420,
            MinHeight = 280,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = SurfaceBrush,
            Topmost = true
        };

        var root = new Grid
        {
            Margin = new Thickness(16)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = $"{title} · 상세 빌드/설치 로그",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = BorderDarkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var logBox = new TextBox
        {
            Text = fullLog,
            IsReadOnly = true,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = SurfaceSoftBrush,
            Foreground = BorderDarkBrush,
            BorderBrush = BorderDarkBrush,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(10)
        };
        Grid.SetRow(logBox, 1);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        Grid.SetRow(buttonRow, 2);

        var copyBtn = new Button
        {
            Content = "로그 복사",
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 0, 8, 0),
            Background = SkyAccentBrush,
            BorderBrush = BorderDarkBrush,
            BorderThickness = new Thickness(1.5),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        copyBtn.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(fullLog);
                copyBtn.Content = "복사됨 ✓";
            }
            catch
            {
                // Ignore clipboard lock errors
            }
        };

        var closeBtn = new Button
        {
            Content = "닫기",
            Padding = new Thickness(12, 5, 12, 5),
            Background = SurfaceSoftBrush,
            BorderBrush = BorderDarkBrush,
            BorderThickness = new Thickness(1.5),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (_, _) => window.Close();

        buttonRow.Children.Add(copyBtn);
        buttonRow.Children.Add(closeBtn);

        root.Children.Add(header);
        root.Children.Add(logBox);
        root.Children.Add(buttonRow);

        window.Content = root;
        window.Show();
    }

    private static AndroidConnectedDeviceInfo? ShowDevicePickerModal(
        string slotTitle,
        RemoteInstallDeviceKind targetKind,
        IReadOnlyList<AndroidConnectedDeviceInfo> candidates)
    {
        AndroidConnectedDeviceInfo? selected = null;
        var kindLabel = RemoteInstallRules.GetDeviceKindKoreanLabel(targetKind);

        var window = new Window
        {
            Title = $"{slotTitle} - 대상 {kindLabel} 기기 선택",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = $"연결된 {kindLabel} 기기가 {candidates.Count}대 감지되었습니다",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = BorderDarkBrush
        });
        stack.Children.Add(new TextBlock
        {
            Text = "설치할 기기를 선택하세요 (숫자키 1~9 또는 클릭)",
            FontSize = 11.5,
            Foreground = SubTextBrush,
            Margin = new Thickness(0, 4, 0, 12)
        });

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var indexLabel = i + 1;
            var itemButton = new Button
            {
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Background = SurfaceSoftBrush,
                BorderBrush = BorderDarkBrush,
                BorderThickness = new Thickness(1.5),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Content = $"{indexLabel}. {candidate.DisplaySummary}"
            };
            itemButton.Click += (_, _) =>
            {
                selected = candidate;
                window.DialogResult = true;
                window.Close();
            };
            stack.Children.Add(itemButton);
        }

        var cancelBtn = new Button
        {
            Content = "취소 (Esc)",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 6, 0, 0),
            Background = SurfaceBrush,
            BorderBrush = BorderDarkBrush,
            BorderThickness = new Thickness(1.5),
            Cursor = Cursors.Hand
        };
        cancelBtn.Click += (_, _) =>
        {
            window.DialogResult = false;
            window.Close();
        };
        stack.Children.Add(cancelBtn);

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                window.DialogResult = false;
                window.Close();
                e.Handled = true;
                return;
            }

            var digit = e.Key switch
            {
                >= Key.D1 and <= Key.D9 => e.Key - Key.D1,
                >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad1,
                _ => -1
            };

            if (digit >= 0 && digit < candidates.Count)
            {
                selected = candidates[digit];
                window.DialogResult = true;
                window.Close();
                e.Handled = true;
            }
        };

        var outerBorder = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = BorderDarkBrush,
            BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18, 16, 18, 16),
            Child = stack
        };

        window.Content = outerBorder;
        window.ShowDialog();
        return selected;
    }

    private static SolidColorBrush CreateFrozenBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
