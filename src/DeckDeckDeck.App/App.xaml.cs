using System.Windows;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace DeckDeckDeck.App;

public partial class App : Application
{
    private FormsNotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CreateTrayIcon();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeTrayIcon();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        var menu = new FormsContextMenuStrip();
        menu.Items.Add(new FormsToolStripMenuItem("열기", null, (_, _) => ShowMainWindow()));
        menu.Items.Add(new FormsToolStripMenuItem("종료", null, (_, _) => Shutdown()));

        _trayIcon = new FormsNotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = (System.Drawing.Icon)DrawingSystemIcons.Application.Clone(),
            Text = "DeckDeckDeck 실행 중",
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        if (MainWindow.WindowState == WindowState.Minimized)
        {
            MainWindow.WindowState = WindowState.Normal;
        }

        if (!MainWindow.IsVisible)
        {
            MainWindow.Show();
        }

        MainWindow.Activate();
        MainWindow.Focus();
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.Visible = false;
        _trayIcon.Icon?.Dispose();
        _trayIcon.Dispose();
        _trayIcon = null;
    }
}
