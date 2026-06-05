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
