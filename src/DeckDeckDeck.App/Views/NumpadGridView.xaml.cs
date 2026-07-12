using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Views;

public partial class NumpadGridView : UserControl
{
    private static readonly TimeSpan LongPressDuration = TimeSpan.FromMilliseconds(400);
    private const double MoveCancelThresholdDip = 12;
    private const string SlotDragFormat = "DeckDeckDeck.SlotKey";

    private readonly DispatcherTimer _longPressTimer;
    private Point _pressPoint;
    private SlotViewModel? _pressSlot;
    private SlotViewModel? _highlightedDropTarget;
    private Popup? _ghostPopup;
    private bool _isDragInProgress;

    public NumpadGridView()
    {
        InitializeComponent();
        _longPressTimer = new DispatcherTimer { Interval = LongPressDuration };
        _longPressTimer.Tick += OnLongPressElapsed;
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CancelPendingLongPress();

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (IsOverEditButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (FindSlotViewModel(e.OriginalSource as DependencyObject) is not { } slot
            || !slot.CanStartDrag)
        {
            return;
        }

        _pressSlot = slot;
        _pressPoint = e.GetPosition(this);
        _longPressTimer.Start();
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressSlot is null || !_longPressTimer.IsEnabled)
        {
            return;
        }

        var delta = e.GetPosition(this) - _pressPoint;
        if (Math.Abs(delta.X) > MoveCancelThresholdDip
            || Math.Abs(delta.Y) > MoveCancelThresholdDip)
        {
            CancelPendingLongPress();
        }
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CancelPendingLongPress();
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_isDragInProgress)
        {
            CancelPendingLongPress();
        }
    }

    private void OnLongPressElapsed(object? sender, EventArgs e)
    {
        _longPressTimer.Stop();

        var slot = _pressSlot;
        _pressSlot = null;
        if (slot is null || !slot.CanStartDrag || DataContext is not NumpadGridViewModel)
        {
            return;
        }

        BeginDrag(slot);
    }

    private void BeginDrag(SlotViewModel slot)
    {
        _isDragInProgress = true;
        slot.IsDragging = true;
        slot.SuppressNextSelect();
        ShowGhost(slot);

        try
        {
            GiveFeedback += OnGiveFeedback;
            var data = new DataObject(SlotDragFormat, slot.SlotKey);
            DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
        }
        finally
        {
            GiveFeedback -= OnGiveFeedback;
            slot.IsDragging = false;
            HideGhost();
            ClearDropHighlight();
            _isDragInProgress = false;
            // A second suppress covers Click that may fire after DoDragDrop returns.
            slot.SuppressNextSelect();
        }
    }

    private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        UpdateGhostPosition();
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void Slot_DragOver(object sender, DragEventArgs e)
    {
        if (!TryGetDropTarget(sender, e, out var target, out var sourceKey))
        {
            e.Effects = DragDropEffects.None;
            ClearDropHighlight();
            e.Handled = true;
            return;
        }

        if (!target.CanAcceptDrop || target.SlotKey == sourceKey)
        {
            e.Effects = DragDropEffects.None;
            ClearDropHighlight();
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        SetDropHighlight(target);
        e.Handled = true;
    }

    private void Slot_DragLeave(object sender, DragEventArgs e)
    {
        if (FindSlotViewModel(sender as DependencyObject) is { } slot
            && ReferenceEquals(_highlightedDropTarget, slot))
        {
            ClearDropHighlight();
        }
    }

    private void Slot_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetDropTarget(sender, e, out var target, out var sourceKey)
            || !target.CanAcceptDrop
            || target.SlotKey == sourceKey)
        {
            e.Effects = DragDropEffects.None;
            ClearDropHighlight();
            e.Handled = true;
            return;
        }

        ClearDropHighlight();
        if (DataContext is NumpadGridViewModel grid)
        {
            grid.RequestReorder(sourceKey, target.SlotKey);
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private bool TryGetDropTarget(
        object sender,
        DragEventArgs e,
        out SlotViewModel target,
        out SlotKey sourceKey)
    {
        target = null!;
        sourceKey = default;

        if (!e.Data.GetDataPresent(SlotDragFormat)
            || e.Data.GetData(SlotDragFormat) is not SlotKey key)
        {
            return false;
        }

        sourceKey = key;
        var slot = FindSlotViewModel(sender as DependencyObject)
            ?? FindSlotViewModel(e.OriginalSource as DependencyObject);
        if (slot is null)
        {
            return false;
        }

        target = slot;
        return true;
    }

    private void SetDropHighlight(SlotViewModel target)
    {
        if (ReferenceEquals(_highlightedDropTarget, target))
        {
            return;
        }

        ClearDropHighlight();
        _highlightedDropTarget = target;
        target.IsDropHighlight = true;
    }

    private void ClearDropHighlight()
    {
        if (_highlightedDropTarget is null)
        {
            return;
        }

        _highlightedDropTarget.IsDropHighlight = false;
        _highlightedDropTarget = null;
    }

    private void CancelPendingLongPress()
    {
        _longPressTimer.Stop();
        _pressSlot = null;
    }

    private void ShowGhost(SlotViewModel slot)
    {
        HideGhost();

        var content = new Border
        {
            Width = 118,
            Height = 88,
            Opacity = 0.72,
            CornerRadius = new CornerRadius(10),
            Background = TryFindResource("Deck.Brush.Background.Surface") as Brush
                ?? new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xD1)),
            BorderBrush = TryFindResource("Deck.Brush.Brand.Accent") as Brush
                ?? new SolidColorBrush(Color.FromRgb(0xFF, 0xB1, 0x5E)),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(8),
            Effect = TryFindResource("Deck.Effect.CardShadow") as System.Windows.Media.Effects.Effect
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = slot.KeyText,
            FontSize = 11,
            Opacity = 0.75,
            Foreground = TryFindResource("Deck.Brush.Text.Primary") as Brush
                ?? Brushes.SaddleBrown
        });
        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(slot.Title) ? slot.DisplayText : slot.Title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 40,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = TryFindResource("Deck.Brush.Text.Primary") as Brush
                ?? Brushes.SaddleBrown
        });
        content.Child = stack;

        _ghostPopup = new Popup
        {
            AllowsTransparency = true,
            IsHitTestVisible = false,
            Placement = PlacementMode.Absolute,
            Child = content,
            IsOpen = true
        };

        UpdateGhostPosition();
    }

    private void HideGhost()
    {
        if (_ghostPopup is null)
        {
            return;
        }

        _ghostPopup.IsOpen = false;
        _ghostPopup.Child = null;
        _ghostPopup = null;
    }

    private void UpdateGhostPosition()
    {
        if (_ghostPopup is null)
        {
            return;
        }

        var screen = GetMouseScreenPosition();
        _ghostPopup.HorizontalOffset = screen.X + 14;
        _ghostPopup.VerticalOffset = screen.Y + 14;
    }

    private static Point GetMouseScreenPosition()
    {
        var point = System.Windows.Forms.Control.MousePosition;
        return new Point(point.X, point.Y);
    }

    private static bool IsOverEditButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button button
                && button.DataContext is SlotViewModel slot
                && ReferenceEquals(button.Command, slot.EditCommand))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static SlotViewModel? FindSlotViewModel(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement element && element.DataContext is SlotViewModel slot)
            {
                return slot;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
