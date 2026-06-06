using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class ViewRenderingTests
{
    [Fact]
    public void SettingsViewLoads()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var services = CreateServices();
                var viewModel = new SettingsViewModel(
                    services.SettingsService,
                    () => { },
                    () => { },
                    _ => { },
                    services.LoggingService);
                var view = new SettingsView { DataContext = viewModel };

                view.Measure(new Size(560, 680));
                view.Arrange(new Rect(0, 0, 560, 680));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void SnippetEditViewLoadsForEmptySlot()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var services = CreateServices();
                var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
                var viewModel = new SnippetEditViewModel(
                    category,
                    SlotKey.Numpad3,
                    snippet: null,
                    services.SnippetService,
                    new DialogService(),
                    () => { },
                    _ => { },
                    () => { },
                    _ => { });
                var view = new SnippetEditView { DataContext = viewModel };

                view.Measure(new Size(560, 680));
                view.Arrange(new Rect(0, 0, 560, 680));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void NumpadGridViewLoadsForThumbnailSlot()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var slots = SlotKeyCatalog.All.Select(slotKey => new SlotViewModel(
                    slotKey,
                    slotKey == SlotKey.Numpad1 ? "GitHub" : null,
                    slotKey == SlotKey.Numpad1 ? "thumbnail.png" : null,
                    true,
                    _ => { },
                    _ => { }));
                var viewModel = new NumpadGridViewModel(slots);
                var view = new NumpadGridView { DataContext = viewModel };

                view.Measure(new Size(560, 680));
                view.Arrange(new Rect(0, 0, 560, 680));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void NumpadGridViewWrapsLongTitleForTextSlot()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var slots = SlotKeyCatalog.All.Select(slotKey => new SlotViewModel(
                    slotKey,
                    "이미지 생성 프롬프트",
                    thumbnailPath: null,
                    true,
                    _ => { },
                    _ => { }));
                var viewModel = new NumpadGridViewModel(slots);
                var view = new NumpadGridView { DataContext = viewModel };

                view.Measure(new Size(560, 680));
                view.Arrange(new Rect(0, 0, 560, 680));
                view.UpdateLayout();

                var titleBlocks = FindVisualChildren<TextBlock>(view)
                    .Where(textBlock => textBlock.Name == "DisplayTextBlock")
                    .ToList();

                Assert.NotEmpty(titleBlocks);
                Assert.All(titleBlocks, textBlock =>
                {
                    Assert.Equal(TextWrapping.Wrap, textBlock.TextWrapping);
                    Assert.Equal(20, textBlock.LineHeight);
                    Assert.Equal(LineStackingStrategy.BlockLineHeight, textBlock.LineStackingStrategy);
                    Assert.Equal(40, textBlock.MaxHeight);
                });
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
