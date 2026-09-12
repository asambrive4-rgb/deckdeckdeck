// 역할: 특정 슬롯에 등록된 텍스트 스니펫의 이름, 본문 내용, 표시 설정을 담는 데이터 모델입니다.
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
}
