using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using System.Runtime.InteropServices;
using System.Windows;
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
}
