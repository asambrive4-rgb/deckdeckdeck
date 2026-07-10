using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Views.Navigation;

/// <summary>
/// Keeps Home/Category view instances alive across navigation so the slot grid
/// is not torn down and rebuilt on every transition.
/// Transient screens (editors, settings, hotkey flows) are not cached.
/// </summary>
public sealed class CachingContentControl : Grid
{
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content),
        typeof(object),
        typeof(CachingContentControl),
        new PropertyMetadata(null, OnContentChanged));

    private const int MaxCachedHomeViews = 1;
    private const int MaxCachedCategoryViews = 3;
    private static readonly Duration FirstShowFadeDuration = TimeSpan.FromMilliseconds(120);

    private readonly Dictionary<object, FrameworkElement> _cache = new(ReferenceEqualityComparer.Instance);
    private readonly LinkedList<object> _lru = new();
    private FrameworkElement? _transientView;
    private object? _currentContent;
    private object? _pendingContent;

    public CachingContentControl()
    {
        Loaded += OnHostLoaded;
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CachingContentControl)d).Present(e.NewValue);
    }

    private void OnHostLoaded(object sender, RoutedEventArgs e)
    {
        if (_pendingContent is null)
        {
            return;
        }

        var pending = _pendingContent;
        _pendingContent = null;
        // Force re-present even if Content DP already equals pending.
        _currentContent = null;
        Present(pending);
    }

    private void Present(object? content)
    {
        if (ReferenceEquals(_currentContent, content) && content is not null && HasVisiblePresentation(content))
        {
            return;
        }

        HideAllCachedViews();
        DetachTransientView();

        _currentContent = content;
        _pendingContent = null;

        if (content is null)
        {
            return;
        }

        if (IsCacheable(content))
        {
            PresentCached(content);
            return;
        }

        PresentTransient(content);
    }

    private bool HasVisiblePresentation(object content)
    {
        if (IsCacheable(content)
            && _cache.TryGetValue(content, out var cached)
            && cached.Visibility == Visibility.Visible
            && Children.Contains(cached))
        {
            return true;
        }

        return _transientView is not null
            && ReferenceEquals(_transientView.DataContext, content)
            && Children.Contains(_transientView);
    }

    private void PresentCached(object content)
    {
        if (_cache.TryGetValue(content, out var existing))
        {
            if (!Children.Contains(existing))
            {
                Children.Add(existing);
            }

            TouchLru(content);
            ShowReady(existing);
            return;
        }

        if (!TryCreateView(content, animateFirstShow: true, out var view))
        {
            // Templates often live on the Window; retry after we are in the tree.
            _pendingContent = content;
            return;
        }

        _cache[content] = view;
        TouchLru(content);
        EvictOverflow(content);
        if (!Children.Contains(view))
        {
            Children.Add(view);
        }
    }

    private void PresentTransient(object content)
    {
        if (!TryCreateView(content, animateFirstShow: true, out var view))
        {
            _pendingContent = content;
            return;
        }

        _transientView = view;
        Children.Add(view);
    }

    private bool TryCreateView(object content, bool animateFirstShow, out FrameworkElement view)
    {
        view = null!;
        var template = FindTemplate(content.GetType());
        if (template is null)
        {
            return false;
        }

        FrameworkElement? root;
        try
        {
            root = template.LoadContent() as FrameworkElement;
        }
        catch
        {
            return false;
        }

        if (root is null)
        {
            return false;
        }

        root.DataContext = content;
        root.Visibility = Visibility.Visible;

        if (animateFirstShow)
        {
            PrepareFirstShowFade(root);
        }
        else
        {
            ClearOpacityAnimation(root);
            root.Opacity = 1;
        }

        view = root;
        return true;
    }

    private static void PrepareFirstShowFade(FrameworkElement element)
    {
        ClearOpacityAnimation(element);
        element.Opacity = 0;

        if (element.IsLoaded)
        {
            StartFirstShowFade(element);
            return;
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            element.Loaded -= OnLoaded;
            StartFirstShowFade(element);
        }

        element.Loaded += OnLoaded;
    }

    private static void StartFirstShowFade(FrameworkElement element)
    {
        // If the view was hidden/recycled before fade finished, skip animating a collapsed tree.
        if (element.Visibility != Visibility.Visible)
        {
            ClearOpacityAnimation(element);
            element.Opacity = 1;
            return;
        }

        var animation = new DoubleAnimation(0, 1, FirstShowFadeDuration)
        {
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            ClearOpacityAnimation(element);
            if (element.Visibility == Visibility.Visible)
            {
                element.Opacity = 1;
            }
        };
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static void ShowReady(FrameworkElement element)
    {
        ClearOpacityAnimation(element);
        element.Opacity = 1;
        element.Visibility = Visibility.Visible;
    }

    private static void ClearOpacityAnimation(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
    }

    private DataTemplate? FindTemplate(Type contentType)
    {
        for (var type = contentType; type is not null && type != typeof(object); type = type.BaseType)
        {
            var key = new DataTemplateKey(type);
            if (TryFindResource(key) is DataTemplate local)
            {
                return local;
            }

            if (Application.Current?.TryFindResource(key) is DataTemplate app)
            {
                return app;
            }
        }

        return null;
    }

    private void HideAllCachedViews()
    {
        foreach (var view in _cache.Values)
        {
            ClearOpacityAnimation(view);
            view.Visibility = Visibility.Collapsed;
        }
    }

    private void DetachTransientView()
    {
        if (_transientView is null)
        {
            return;
        }

        ClearOpacityAnimation(_transientView);
        Children.Remove(_transientView);
        _transientView.DataContext = null;
        _transientView = null;
    }

    private void TouchLru(object key)
    {
        _lru.Remove(key);
        _lru.AddLast(key);
    }

    private void EvictOverflow(object justAdded)
    {
        EvictTypeOverflow(typeof(HomeViewModel), MaxCachedHomeViews, justAdded);
        EvictTypeOverflow(typeof(CategoryViewModel), MaxCachedCategoryViews, justAdded);
    }

    private void EvictTypeOverflow(Type viewModelType, int maxCount, object justAdded)
    {
        while (CountCachedOfType(viewModelType) > maxCount)
        {
            var evicted = false;
            for (var node = _lru.First; node is not null; node = node.Next)
            {
                var key = node.Value;
                if (ReferenceEquals(key, justAdded)
                    || ReferenceEquals(key, _currentContent)
                    || !viewModelType.IsInstanceOfType(key)
                    || !_cache.ContainsKey(key))
                {
                    continue;
                }

                RemoveCached(key);
                evicted = true;
                break;
            }

            if (!evicted)
            {
                break;
            }
        }
    }

    private int CountCachedOfType(Type viewModelType)
    {
        var count = 0;
        foreach (var key in _cache.Keys)
        {
            if (viewModelType.IsInstanceOfType(key))
            {
                count++;
            }
        }

        return count;
    }

    private void RemoveCached(object key)
    {
        _lru.Remove(key);
        if (!_cache.Remove(key, out var view))
        {
            return;
        }

        ClearOpacityAnimation(view);
        Children.Remove(view);
        view.DataContext = null;
    }

    private static bool IsCacheable(object content)
    {
        return content is HomeViewModel or CategoryViewModel;
    }

    /// <summary>
    /// Reference equality so two different HomeViewModel instances never share a cache slot.
    /// </summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
