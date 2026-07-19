namespace DeckDeckDeck.App.Models;

public sealed class HotkeyAction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public int? HotkeyVirtualKey { get; set; }

    public HotkeyModifiers HotkeyModifiers { get; set; } = HotkeyModifiers.None;

    public bool IsEnabled { get; set; } = true;

    public string Content { get; set; } = string.Empty;

    public SnippetActionType ActionType { get; set; } = SnippetActionType.PasteText;

    public PasteShortcutMode PasteShortcutMode { get; set; } = PasteShortcutMode.CtrlV;

    public string? LaunchPath { get; set; }

    public FileActionMode FileActionMode { get; set; } = FileActionMode.Launch;

    public string? LaunchUrl { get; set; }

    public SnippetMediaProvider? MediaProvider { get; set; }

    public SnippetMediaCommand? MediaCommand { get; set; }

    public string? TerminalCommand { get; set; }

    public SnippetTerminalShell? TerminalShell { get; set; }

    public bool OpenTerminalWindow { get; set; }

    public string? TerminalWorkingDirectory { get; set; }

    /// <summary>
    /// Per-action fixed device IP for ADB wireless connect. Port is entered at run time.
    /// </summary>
    public string? AdbDeviceIp { get; set; }

    public bool RunAsAdministrator { get; set; } = true;

    public SlotImageMode SlotImageMode { get; set; } = SlotImageMode.Auto;

    public string? Description { get; set; }

    public string? ImagePath { get; set; }

    public string? ThumbnailPath { get; set; }

    public string? AutoIconPath { get; set; }

    public string? AutoIconSourcePath { get; set; }

    public DateTime? AutoIconSourceLastWriteTimeUtc { get; set; }

    public long? AutoIconSourceLength { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public HotkeyGesture? Gesture => HotkeyVirtualKey.HasValue
        ? new HotkeyGesture((uint)HotkeyVirtualKey.Value, HotkeyModifiers)
        : null;

    public string HotkeyDisplayText => Gesture?.DisplayText ?? "미지정";
}
