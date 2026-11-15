// 역할: 파일 열기, 앱 선택, 블루투스 기기 선택, 저장 확인, 알림 메시지 등 윈도우 대화상자를 화면에 띄웁니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases.Ports;
using System.Windows;
using Microsoft.Win32;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public class DialogAdapter : IDialogAdapter
{
    private readonly Dictionary<string, string> _lastTextInputValues = new(StringComparer.Ordinal);

    public virtual bool Confirm(string title, string message)
    {
        return MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public virtual void ShowInformation(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public virtual bool TryPromptTextInputs(
        string title,
        string message,
        IReadOnlyList<string> fieldNames,
        out IReadOnlyDictionary<string, string> values)
    {
        if (fieldNames.Count == 0)
        {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            return true;
        }

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            IReadOnlyDictionary<string, string>? captured = null;
            var confirmed = false;
            dispatcher.Invoke(() =>
            {
                confirmed = TryPromptTextInputs(title, message, fieldNames, out captured!);
            });
            values = captured ?? new Dictionary<string, string>(StringComparer.Ordinal);
            return confirmed;
        }

        var initialValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in fieldNames)
        {
            if (_lastTextInputValues.TryGetValue(name, out var previous))
            {
                initialValues[name] = previous;
            }
        }

        var window = new TextInputPromptWindow(title, message, fieldNames, initialValues);
        var owner = Application.Current?.MainWindow;
        if (owner is { IsLoaded: true, IsVisible: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        var confirmedResult = window.ShowDialog() == true;
        if (!confirmedResult)
        {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            return false;
        }

        var collected = window.CollectValues();
        foreach (var (name, value) in collected)
        {
            _lastTextInputValues[name] = value;
        }

        values = collected;
        return true;
    }

    public virtual bool TryPromptAdbAction(
        string title,
        string fixedIp,
        out AdbPromptResult result)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            AdbPromptResult? capturedResult = null;
            var confirmed = false;
            dispatcher.Invoke(() =>
            {
                confirmed = TryPromptAdbAction(title, fixedIp, out capturedResult!);
            });
            result = capturedResult ?? new AdbPromptResult(AdbPromptMode.Connect, string.Empty);
            return confirmed;
        }

        var window = new AdbConnectPromptWindow(title, fixedIp);
        var owner = Application.Current?.MainWindow;
        if (owner is { IsLoaded: true, IsVisible: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        var dialogResult = window.ShowDialog();
        result = window.Result;
        return dialogResult == true;
    }

    public virtual bool TryPromptAdbPort(
        string title,
        string fixedIp,
        out string port)
    {
        var confirmed = TryPromptAdbAction(title, fixedIp, out var result);
        port = confirmed ? result.Port : string.Empty;
        return confirmed;
    }

    public virtual string? SelectImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "이미지 선택",
            Filter = "이미지 파일 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public virtual string? SelectLaunchFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "실행할 파일 또는 바로 가기 선택",
            Filter = "모든 파일 (*.*)|*.*|실행 파일 및 바로 가기 (*.exe;*.lnk)|*.exe;*.lnk",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true
            ? LaunchPathRules.ToPortablePath(dialog.FileName)
            : null;
    }

    public virtual string? SelectPasteFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "붙여넣을 파일 선택",
            Filter = "모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public virtual string? SelectLaunchFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "실행할 폴더 선택",
            UseDescriptionForTitle = true
        };

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    public virtual string? SelectBackupFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "백업 폴더 선택",
            UseDescriptionForTitle = true
        };

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    public virtual string? SelectBackupZipFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "백업 ZIP 선택",
            Filter = "DeckDeckDeck 백업 ZIP (*.zip)|*.zip|모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public virtual InstalledAppSelection? SelectInstalledApp()
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            InstalledAppSelection? result = null;
            dispatcher.Invoke(() =>
            {
                result = SelectInstalledApp();
            });
            return result;
        }

        var window = new InstalledAppPickerWindow();
        var owner = Application.Current?.MainWindow;
        if (owner is { IsLoaded: true, IsVisible: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        if (window.ShowDialog() == true && window.SelectedApp is { } app)
        {
            return new InstalledAppSelection(app.DisplayName, app.LaunchPath);
        }

        return null;
    }

    public virtual BluetoothDeviceSelection? SelectPairedBluetoothDevice()
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            BluetoothDeviceSelection? result = null;
            dispatcher.Invoke(() =>
            {
                result = SelectPairedBluetoothDevice();
            });
            return result;
        }

        var devices = WindowsBluetoothDeviceControlGateway.GetPairedDevices();
        var entries = devices
            .Select(d => new InstalledAppEntry(
                d.DisplayName,
                d.MacAddress,
                d.IsConnected ? $"{d.MacAddress} · 현재 연결됨" : d.MacAddress))
            .ToList();

        var window = new InstalledAppPickerWindow(
            entries,
            "블루투스 기기 선택",
            "페어링된 블루투스 오디오 기기를 선택하세요.",
            "기기 이름 또는 MAC 주소 검색...",
            "페어링된 블루투스 기기를 찾을 수 없습니다.");

        var owner = Application.Current?.MainWindow;
        if (owner is { IsLoaded: true, IsVisible: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        if (window.ShowDialog() == true && window.SelectedApp is { } app)
        {
            return new BluetoothDeviceSelection(app.DisplayName, app.LaunchPath);
        }

        return null;
    }

    public virtual RemoteInstallProjectSelection? SelectRemoteInstallProject(
        RemoteInstallProjectPromptRequest request)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            RemoteInstallProjectSelection? result = null;
            dispatcher.Invoke(() =>
            {
                result = SelectRemoteInstallProject(request);
            });
            return result;
        }

        var window = new RemoteInstallProjectPickerWindow(request);
        var owner = Application.Current?.MainWindow;
        if (owner is { IsLoaded: true, IsVisible: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.Topmost = true;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return window.ShowDialog() == true ? window.Selection : null;
    }
}
