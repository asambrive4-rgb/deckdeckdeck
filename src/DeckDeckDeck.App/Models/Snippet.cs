namespace DeckDeckDeck.App.Models;

public sealed class Snippet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CategoryId { get; set; }

    public Category? Category { get; set; }

    public SlotKey SlotKey { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public SnippetActionType ActionType { get; set; } = SnippetActionType.PasteText;

    public PasteShortcutMode PasteShortcutMode { get; set; } = PasteShortcutMode.CtrlV;

    public string? LaunchPath { get; set; }

    public string? LaunchUrl { get; set; }

    public SnippetMediaProvider? MediaProvider { get; set; }

    public SnippetMediaCommand? MediaCommand { get; set; }

    public string? TerminalCommand { get; set; }

    public SnippetTerminalShell? TerminalShell { get; set; }

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
}
