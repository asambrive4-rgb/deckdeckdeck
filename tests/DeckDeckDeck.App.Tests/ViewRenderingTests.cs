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
using DeckDeckDeck.App.Views.Navigation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

[Collection(WpfViewRenderingCollection.Name)]
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
                EnsureApplicationResources();
                var services = CreateServices();
                var viewModel = TestAppFactory.CreateSettingsViewModel(
                    services,
                    spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
                    clipboardTextWriter: new FakeClipboardAdapter(null));
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
                ShutdownCurrentThreadApplication();
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
                EnsureApplicationResources();
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
                ShutdownCurrentThreadApplication();
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
                EnsureApplicationResources();
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
                ShutdownCurrentThreadApplication();
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
                EnsureApplicationResources();
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
                ShutdownCurrentThreadApplication();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void HotkeyListViewLoadsForEmptyState()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                var services = CreateServices();
                var viewModel = new HotkeyListViewModel(
                    [],
                    new SetHotkeyActionEnabledUseCase(services.HotkeyActionRepository),
                    new DeleteHotkeyActionUseCase(services.HotkeyActionRepository, services.ImageFileRepository),
                    new DialogAdapter(),
                    () => { },
                    _ => { },
                    () => { },
                    () => { },
                    () => { },
                    _ => { });
                var view = new HotkeyListView { DataContext = viewModel };

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
                ShutdownCurrentThreadApplication();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void HotkeyEditViewLoadsForNewAction()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                var services = CreateServices();
                var viewModel = new HotkeyEditViewModel(
                    action: null,
                    new HotkeyActionEditorState(SpotifyConnectionState.FromSettings(services.SettingsRepository.Load())),
                    new SaveHotkeyActionUseCase(services.HotkeyActionRepository),
                    new DeleteHotkeyActionUseCase(services.HotkeyActionRepository, services.ImageFileRepository),
                    new DialogAdapter(),
                    () => { },
                    _ => { },
                    () => { },
                    () => { },
                    _ => { },
                    services.ImageFileRepository,
                    services.FileLogger,
                    services.SnippetImageResolver,
                    services.StoredImagePathResolver);
                var view = new HotkeyEditView { DataContext = viewModel };

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
                ShutdownCurrentThreadApplication();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void CachingContentControlReusesHomeAndCategoryVisuals()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                var services = CreateServices();
                services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
                var mainViewModel = CreateMainViewModel(services);
                var home = Assert.IsType<HomeViewModel>(mainViewModel.CurrentViewModel);

                var host = new CachingContentControl();
                host.Resources.Add(
                    new DataTemplateKey(typeof(HomeViewModel)),
                    CreateViewTemplate("HomeView"));
                host.Resources.Add(
                    new DataTemplateKey(typeof(CategoryViewModel)),
                    CreateViewTemplate("CategoryView"));

                host.Content = home;
                host.Measure(new Size(400, 500));
                host.Arrange(new Rect(0, 0, 400, 500));
                host.UpdateLayout();
                var homeVisual = Assert.Single(
                    host.Children.OfType<FrameworkElement>(),
                    child => child.Visibility == Visibility.Visible);

                mainViewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
                var category = Assert.IsType<CategoryViewModel>(mainViewModel.CurrentViewModel);
                host.Content = category;
                host.UpdateLayout();
                var categoryVisual = Assert.Single(
                    host.Children.OfType<FrameworkElement>(),
                    child => child.Visibility == Visibility.Visible);
                Assert.NotSame(homeVisual, categoryVisual);

                host.Content = home;
                host.UpdateLayout();
                var homeVisualAgain = Assert.Single(
                    host.Children.OfType<FrameworkElement>(),
                    child => child.Visibility == Visibility.Visible);
                Assert.Same(homeVisual, homeVisualAgain);

                host.Content = category;
                host.UpdateLayout();
                var categoryVisualAgain = Assert.Single(
                    host.Children.OfType<FrameworkElement>(),
                    child => child.Visibility == Visibility.Visible);
                Assert.Same(categoryVisual, categoryVisualAgain);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                ShutdownCurrentThreadApplication();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void CachingContentControlSurvivesMissingTemplateWithoutThrowing()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                var services = CreateServices();
                var mainViewModel = CreateMainViewModel(services);
                var home = Assert.IsType<HomeViewModel>(mainViewModel.CurrentViewModel);

                // No DataTemplate registered for HomeViewModel on this host.
                var host = new CachingContentControl { Content = home };
                host.Measure(new Size(400, 500));
                host.Arrange(new Rect(0, 0, 400, 500));
                host.UpdateLayout();

                Assert.Empty(host.Children);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                ShutdownCurrentThreadApplication();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void CachingContentControlKeepsOnlyOneHomeVisualWhenHomeIsRebuilt()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                var services = CreateServices();
                services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
                var mainViewModel = CreateMainViewModel(services);
                var home1 = Assert.IsType<HomeViewModel>(mainViewModel.CurrentViewModel);

                var host = new CachingContentControl();
                host.Resources.Add(
                    new DataTemplateKey(typeof(HomeViewModel)),
                    CreateViewTemplate("HomeView"));
                host.Resources.Add(
                    new DataTemplateKey(typeof(CategoryViewModel)),
                    CreateViewTemplate("CategoryView"));
                host.Resources.Add(
                    new DataTemplateKey(typeof(CategoryEditViewModel)),
                    CreateViewTemplate("CategoryEditView"));

                host.Content = home1;
                host.Measure(new Size(400, 500));
                host.Arrange(new Rect(0, 0, 400, 500));
                host.UpdateLayout();

                home1.NumpadGrid.Numpad1.EditCommand.Execute(null);
                var editor = Assert.IsType<CategoryEditViewModel>(mainViewModel.CurrentViewModel);
                host.Content = editor;
                host.UpdateLayout();

                editor.Name = "Writing Renamed";
                editor.SaveCommand.Execute(null);
                var home2 = Assert.IsType<HomeViewModel>(mainViewModel.CurrentViewModel);
                Assert.NotSame(home1, home2);

                host.Content = home2;
                host.UpdateLayout();

                // Old home visual must be evicted (max 1 home); only the new home remains cached.
                var homeChildren = host.Children
                    .OfType<FrameworkElement>()
                    .Where(child => child.DataContext is HomeViewModel)
                    .ToList();
                Assert.Single(homeChildren);
                Assert.Same(home2, homeChildren[0].DataContext);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                ShutdownCurrentThreadApplication();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    private static DataTemplate CreateViewTemplate(string viewTypeName)
    {
        var xaml =
            $"""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:views="clr-namespace:DeckDeckDeck.App.Views;assembly=DeckDeckDeck.App">
              <views:{viewTypeName} />
            </DataTemplate>
            """;
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
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

    private static void EnsureApplicationResources()
    {
        var application = Application.Current ?? new Application();
        var themeSource = new Uri(
            "/DeckDeckDeck.App;component/Resources/Theme.xaml",
            UriKind.Relative);

        if (application.Resources.MergedDictionaries.Any(dictionary => dictionary.Source == themeSource))
        {
            return;
        }

        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = themeSource
        });
    }

    private static void ShutdownCurrentThreadApplication()
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            Application.Current.Shutdown();
        }
    }

    private static ISpotifyConnectionUseCase CreateSpotifyConnectionUseCase(TestServices services)
    {
        var urlLaunchService = new RecordingUrlLaunchGatewayAdapter();
        var spotifyConnectionService = new SpotifyConnectionGatewayAdapter(urlLaunchService);
        return new SpotifyConnectionUseCase(
            services.SettingsRepository,
            spotifyConnectionService,
            urlLaunchService);
    }
}



