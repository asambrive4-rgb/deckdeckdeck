using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.UseCases;
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
                    services.SettingsRepository,
                    () => { },
                    () => { },
                    _ => { },
                    services.FileLogger,
                    spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
                    clipboardService: new FakeClipboardAdapter(null));
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
                var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
                var viewModel = new SnippetEditViewModel(
                    category,
                    SlotKey.Numpad3,
                    snippet: null,
                    new LoadSnippetEditorStateUseCase(
                        services.SnippetRepository,
                        services.SettingsRepository)
                        .Execute(new LoadSnippetEditorStateRequest(category.Id, SlotKey.Numpad3, SnippetId: null)),
                    new SaveSnippetUseCase(
                        services.SnippetRepository,
                        services.SettingsRepository,
                        autoBackupRequester: null),
                    new DeleteSnippetUseCase(
                        services.SnippetRepository,
                        services.ImageFileRepository),
                    new TransferSnippetUseCase(
                        services.SnippetRepository,
                        services.SettingsRepository,
                        new SaveSnippetUseCase(services.SnippetRepository, services.SettingsRepository),
                        services.ImageFileRepository),
                    new DialogAdapter(),
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

    private static ISpotifyConnectionUseCase CreateSpotifyConnectionUseCase(TestServices services)
    {
        var urlLaunchService = new RecordingUrlLaunchGatewayAdapter();
        var spotifyConnectionService = new SpotifyConnectionGatewayAdapter(services.SettingsRepository, urlLaunchService);
        return new SpotifyConnectionUseCase(
            services.SettingsRepository,
            spotifyConnectionService,
            urlLaunchService);
    }
}



