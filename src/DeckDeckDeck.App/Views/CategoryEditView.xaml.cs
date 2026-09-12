// 역할: 카테고리 이름과 색상, 아이콘을 편집하는 팝업 대화상자 화면의 코드 비하인드입니다.
using System.Windows;
using System.Windows.Controls;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Views;

public partial class CategoryEditView : UserControl
{
    public CategoryEditView()
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
        if (DataContext is CategoryEditViewModel viewModel
            && e.Data.GetData(DataFormats.FileDrop) is string[] sourcePaths)
        {
            viewModel.DropImageFiles(sourcePaths);
        }

        e.Handled = true;
    }
}
