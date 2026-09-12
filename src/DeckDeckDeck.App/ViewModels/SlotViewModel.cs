// 역할: 3x3 그리드에 배치되는 개별 슬롯 타일의 텍스트, 아이콘, 타이머 상태 및 클릭 동작을 관리하는 화면 모델입니다.
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
    private bool _isTimerRunning;
    private string? _formattedTimerRemaining;
    private bool _isCategoryView;
    private bool _isTimerSlot;

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

    public bool CanStartDrag => IsEnabledSlot && !IsEmpty;

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

    public bool IsTimerRunning
    {
        get => _isTimerRunning;
        set
        {
            if (SetProperty(ref _isTimerRunning, value))
            {
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    public string? FormattedTimerRemaining
    {
        get => _formattedTimerRemaining;
        set
        {
            if (SetProperty(ref _formattedTimerRemaining, value))
            {
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    public bool IsCategoryView
    {
        get => _isCategoryView;
        set
        {
            if (SetProperty(ref _isCategoryView, value))
            {
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    public bool IsTimerSlot
    {
        get => _isTimerSlot;
        set => SetProperty(ref _isTimerSlot, value);
    }

    public string DisplayText =>
        IsCategoryView && IsTimerRunning && !string.IsNullOrEmpty(FormattedTimerRemaining)
            ? FormattedTimerRemaining
            : IsEmpty ? "+" : Title;

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
