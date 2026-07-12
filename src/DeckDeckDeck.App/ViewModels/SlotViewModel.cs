using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SlotViewModel : ObservableObject
{
    private readonly Action<SlotKey> _onSelected;
    private ImageSource? _thumbnailSource;
    private int _thumbnailLoadGeneration;
    private bool _isDropHighlight;
    private bool _isDragging;
    private int _suppressSelectCount;

    public SlotViewModel(
        SlotKey slotKey,
        string? title,
        bool isEnabledSlot,
        Action<SlotKey> onSelected)
        : this(slotKey, title, thumbnailPath: null, isEnabledSlot, onSelected, _ => { })
    {
    }

    public SlotViewModel(
        SlotKey slotKey,
        string? title,
        string? thumbnailPath,
        bool isEnabledSlot,
        Action<SlotKey> onSelected,
        Action<SlotKey> onEdit)
    {
        _onSelected = onSelected;
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        Row = slotKey.GetGridRow();
        Column = slotKey.GetGridColumn();
        RowSpan = slotKey.GetGridRowSpan();
        ColumnSpan = slotKey.GetGridColumnSpan();
        Title = title ?? string.Empty;
        ThumbnailPath = thumbnailPath;
        IsEmpty = string.IsNullOrWhiteSpace(title);
        IsEnabledSlot = isEnabledSlot;
        SelectCommand = new RelayCommand(ExecuteSelect, () => IsEnabledSlot);
        EditCommand = new RelayCommand(() => onEdit(SlotKey));
    }

    public SlotKey SlotKey { get; }

    public string KeyText { get; }

    public int Row { get; }

    public int Column { get; }

    public int RowSpan { get; }

    public int ColumnSpan { get; }

    public string Title { get; }

    public string? ThumbnailPath { get; }

    /// <summary>
    /// Decoded thumbnail for binding. Filled asynchronously by the image load scheduler.
    /// </summary>
    public ImageSource? ThumbnailSource
    {
        get => _thumbnailSource;
        private set => SetProperty(ref _thumbnailSource, value);
    }

    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath);

    public bool IsEmpty { get; }

    public bool IsEnabledSlot { get; }

    /// <summary>
    /// Filled and enabled slots can start a long-press drag reorder.
    /// </summary>
    public bool CanStartDrag => IsEnabledSlot && !IsEmpty;

    /// <summary>
    /// Enabled slots (empty or filled) can accept a drag-and-drop reorder.
    /// </summary>
    public bool CanAcceptDrop => IsEnabledSlot;

    public bool IsDropHighlight
    {
        get => _isDropHighlight;
        set => SetProperty(ref _isDropHighlight, value);
    }

    public bool IsDragging
    {
        get => _isDragging;
        set => SetProperty(ref _isDragging, value);
    }

    public string DisplayText => IsEmpty ? "+" : Title;

    public ICommand SelectCommand { get; }

    public ICommand EditCommand { get; }

    public void SuppressNextSelect()
    {
        _suppressSelectCount++;
    }

    private void ExecuteSelect()
    {
        if (_suppressSelectCount > 0)
        {
            _suppressSelectCount--;
            return;
        }

        _onSelected(SlotKey);
    }

    /// <summary>
    /// Starts a new load generation so in-flight decodes for this slot can be ignored.
    /// </summary>
    public int BeginThumbnailLoadGeneration()
    {
        return Interlocked.Increment(ref _thumbnailLoadGeneration);
    }

    public bool IsCurrentThumbnailLoad(int generation)
    {
        return Volatile.Read(ref _thumbnailLoadGeneration) == generation;
    }

    public void ApplyThumbnailSource(ImageSource? imageSource)
    {
        ThumbnailSource = imageSource;
    }
}
