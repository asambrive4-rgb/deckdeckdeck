using System.Windows;
using Microsoft.Win32;

namespace DeckDeckDeck.App.Services;

public class DialogService
{
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
}
