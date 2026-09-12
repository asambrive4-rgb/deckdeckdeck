// 역할: 슬롯의 텍스트 스니펫 내용과 실행 동작을 설정하는 편집 팝업 화면의 코드 비하인드입니다.
using System.Windows;
using System.Windows.Controls;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Views;

public partial class SnippetEditView : UserControl
{
    public SnippetEditView()
    {
        InitializeComponent();
    }

    private void ImageDropTarget_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ImageDropTarget_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is SnippetEditViewModel viewModel
            && e.Data.GetData(DataFormats.FileDrop) is string[] sourcePaths)
        {
            viewModel.DropImageFiles(sourcePaths);
        }

        e.Handled = true;
    }
}
