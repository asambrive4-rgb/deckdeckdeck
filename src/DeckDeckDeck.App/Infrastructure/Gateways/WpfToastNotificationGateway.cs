// 역할: 윈도우 화면 모서리에 작업 완료나 알림 메시지를 담은 토스트 팝업을 표시합니다.
using System.Media;
using System.Windows;
using System.Windows.Threading;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class WpfToastNotificationGateway : IToastNotificationPort
{
    private readonly Dispatcher _dispatcher;

    public WpfToastNotificationGateway(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public void ShowToast(string title, string message)
    {
        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (Application.Current?.MainWindow is Window mainWindow)
                {
                    ShowCustomToastWindow(title, message, mainWindow);
                }
            }
            catch
            {
            }
        });
    }

    public void PlayCompletionSound()
    {
        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch
        {
        }
    }

    private static void ShowCustomToastWindow(string title, string message, Window owner)
    {
        var toast = new Window
        {
            Title = title,
            Width = 300,
            Height = 70,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        var border = new System.Windows.Controls.Border
        {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#2B2523")!,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 10, 14, 10),
            BorderThickness = new Thickness(1.5),
            BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#D97448")!
        };

        var stack = new System.Windows.Controls.StackPanel();
        var titleBlock = new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13
        };
        var msgBlock = new System.Windows.Controls.TextBlock
        {
            Text = message,
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#E5DFD5")!,
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0)
        };

        stack.Children.Add(titleBlock);
        stack.Children.Add(msgBlock);
        border.Child = stack;
        toast.Content = border;

        var workArea = SystemParameters.WorkArea;
        toast.Left = workArea.Right - 320;
        toast.Top = workArea.Bottom - 85;

        toast.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            toast.Close();
        };
        timer.Start();
    }
}
