// 역할: 블루투스 기기 연결 및 해제 진행 상태와 완료 결과를 포커스를 빼앗지 않는 모바일 스낵바 형태의 플로팅 팝업으로 화면에 표시합니다.
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeckDeckDeck.App.Domain;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal interface IBluetoothActionFeedbackNotifier
{
    void NotifyStarting(BluetoothAudioActionKind action, string deviceDisplayName);
    void NotifyCompleted(BluetoothAudioActionKind action, string deviceDisplayName, bool succeeded);
}

internal sealed class WpfBluetoothActionSnackbarNotifier : IBluetoothActionFeedbackNotifier
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private static readonly SolidColorBrush SurfaceBrush = CreateFrozenBrush("#FCFAF6");
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
    private DispatcherTimer? _closeTimer;
    private int _operationId;

    public WpfBluetoothActionSnackbarNotifier(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher;
    }

    public void NotifyStarting(BluetoothAudioActionKind action, string deviceDisplayName)
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
                _closeTimer?.Stop();

                // Safety timeout: automatically hide after 10 seconds if operation hangs
                _closeTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                _closeTimer.Tick += (s, e) =>
                {
                    _closeTimer?.Stop();
                    if (_operationId == currentOp)
                    {
                        _window?.Hide();
                    }
                };
                _closeTimer.Start();

                if (_badgeBorder != null)
                {
                    _badgeBorder.Background = SkyAccentBrush;
                }

                if (_badgeText != null)
                {
                    _badgeText.Text = "BT •••";
                }

                var actionText = action == BluetoothAudioActionKind.Connect ? "연결 중..." : "연결 해제 중...";
                if (_titleText != null)
                {
                    _titleText.Text = $"{deviceDisplayName} {actionText}";
                }

                if (_subtitleText != null)
                {
                    _subtitleText.Text = "블루투스 오디오 서비스 전환 중";
                }

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

    public void NotifyCompleted(BluetoothAudioActionKind action, string deviceDisplayName, bool succeeded)
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
                _closeTimer?.Stop();

                if (succeeded)
                {
                    if (_badgeBorder != null)
                    {
                        _badgeBorder.Background = MintSuccessBrush;
                    }

                    if (_badgeText != null)
                    {
                        _badgeText.Text = "BT ✓";
                    }

                    var actionText = action == BluetoothAudioActionKind.Connect ? "연결됨" : "연결 해제됨";
                    if (_titleText != null)
                    {
                        _titleText.Text = $"{deviceDisplayName} {actionText}";
                    }

                    if (_subtitleText != null)
                    {
                        _subtitleText.Text = action == BluetoothAudioActionKind.Connect
                            ? "오디오 기기가 연결되었습니다"
                            : "오디오 기기 연결이 해제되었습니다";
                    }
                }
                else
                {
                    if (_badgeBorder != null)
                    {
                        _badgeBorder.Background = PeachAlertBrush;
                    }

                    if (_badgeText != null)
                    {
                        _badgeText.Text = "BT !";
                    }

                    var actionText = action == BluetoothAudioActionKind.Connect ? "연결 실패" : "연결 해제 실패";
                    if (_titleText != null)
                    {
                        _titleText.Text = $"{deviceDisplayName} {actionText}";
                    }

                    if (_subtitleText != null)
                    {
                        _subtitleText.Text = "기기 전원 및 페어링 상태를 확인해 주세요";
                    }
                }

                UpdatePosition();

                if (!_window.IsVisible)
                {
                    _window.Show();
                }

                _closeTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(succeeded ? 1600 : 2600)
                };
                _closeTimer.Tick += (s, e) =>
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
            Padding = new Thickness(9, 4, 9, 4),
            BorderBrush = BorderDarkBrush,
            BorderThickness = new Thickness(1.5),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _badgeText
        };

        _titleText = new TextBlock
        {
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = BorderDarkBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _subtitleText = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = SubTextBrush,
            Margin = new Thickness(0, 1, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        textStack.Children.Add(_titleText);
        textStack.Children.Add(_subtitleText);

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(_badgeBorder, 0);
        Grid.SetColumn(textStack, 2);

        contentGrid.Children.Add(_badgeBorder);
        contentGrid.Children.Add(textStack);

        var outerBorder = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = BorderDarkBrush,
            BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(16, 10, 18, 10),
            MinWidth = 240,
            MaxWidth = 420,
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

        _window.MouseLeftButtonUp += (s, e) => _window?.Hide();
        _window.SizeChanged += (s, e) => UpdatePosition();
        _window.Closed += (s, e) => _window = null;

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
            width = 240;
        }

        if (height <= 0)
        {
            height = 56;
        }

        _window.Left = workArea.Left + (workArea.Width - width) / 2;
        _window.Top = workArea.Bottom - height - 28;
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
