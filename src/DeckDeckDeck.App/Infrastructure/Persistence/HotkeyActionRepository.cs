using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;
using Microsoft.EntityFrameworkCore;
using UseCaseImageFileReference = DeckDeckDeck.App.UseCases.Ports.ImageFileReference;

namespace DeckDeckDeck.App.Infrastructure.Persistence;

public sealed class HotkeyActionRepository : IHotkeyActionRepository
{
    private readonly AppDbContextFactory _dbContextFactory;

    public HotkeyActionRepository(AppDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyList<HotkeyAction> GetAll()
    {
        using var dbContext = _dbContextFactory.Create();

        return dbContext.HotkeyActions
            .AsNoTracking()
            .OrderBy(action => action.CreatedAt)
            .ThenBy(action => action.Title)
            .ToList();
    }

    public HotkeyAction? GetById(Guid id)
    {
        using var dbContext = _dbContextFactory.Create();

        return dbContext.HotkeyActions
            .AsNoTracking()
            .FirstOrDefault(action => action.Id == id);
    }

    public HotkeyAction Create(HotkeyActionSaveData data)
    {
        using var dbContext = _dbContextFactory.Create();

        var now = DateTime.UtcNow;
        var action = new HotkeyAction
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplySaveData(action, data);

        dbContext.HotkeyActions.Add(action);
        dbContext.SaveChanges();

        return action;
    }

    public HotkeyAction Update(Guid id, HotkeyActionSaveData data)
    {
        using var dbContext = _dbContextFactory.Create();

        var action = dbContext.HotkeyActions.First(item => item.Id == id);
        ApplySaveData(action, data);
        action.UpdatedAt = DateTime.UtcNow;
        dbContext.SaveChanges();

        return action;
    }

    public UseCaseImageFileReference Delete(Guid id)
    {
        using var dbContext = _dbContextFactory.Create();

        var action = dbContext.HotkeyActions.First(item => item.Id == id);
        var imageFiles = new UseCaseImageFileReference(action.ImagePath, action.ThumbnailPath);
        dbContext.HotkeyActions.Remove(action);
        dbContext.SaveChanges();

        return imageFiles;
    }

    private static void ApplySaveData(HotkeyAction action, HotkeyActionSaveData data)
    {
        var storedImageMode = GetStoredSlotImageMode(data.SlotImageMode, data.ImagePath);
        var storedAutoIcon = GetStoredAutoIcon(data.ActionType, storedImageMode, data.AutoIcon);

        action.Title = data.Title.Trim();
        action.HotkeyVirtualKey = data.Gesture is null ? null : (int)data.Gesture.VirtualKey;
        action.HotkeyModifiers = data.Gesture?.Modifiers ?? HotkeyModifiers.None;
        action.IsEnabled = data.IsEnabled;
        action.Content = GetStoredContent(data.ActionType, data.Content);
        action.ActionType = data.ActionType;
        action.PasteShortcutMode = GetStoredPasteShortcutMode(data.ActionType, data.PasteShortcutMode);
        action.LaunchPath = GetStoredLaunchPath(data.ActionType, data.LaunchPath);
        action.LaunchUrl = GetStoredLaunchUrl(data.ActionType, data.LaunchUrl);
        action.MediaProvider = GetStoredMediaProvider(data.ActionType, data.MediaProvider);
        action.MediaCommand = GetStoredMediaCommand(data.ActionType, data.MediaCommand);
        action.TerminalCommand = GetStoredTerminalCommand(data.ActionType, data.TerminalCommand);
        action.TerminalShell = GetStoredTerminalShell(data.ActionType, data.TerminalShell);
        action.RunAsAdministrator = GetStoredRunAsAdministrator(data.ActionType, data.RunAsAdministrator);
        action.SlotImageMode = storedImageMode;
        action.Description = NormalizeOptionalText(data.Description);
        action.ImagePath = data.ImagePath;
        action.ThumbnailPath = data.ThumbnailPath;
        SetAutoIcon(action, storedAutoIcon);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetStoredContent(SnippetActionType actionType, string content)
    {
        return actionType == SnippetActionType.PasteText ? content : string.Empty;
    }

    private static string? GetStoredLaunchPath(SnippetActionType actionType, string? launchPath)
    {
        return actionType == SnippetActionType.LaunchFile ? NormalizeOptionalText(launchPath) : null;
    }

    private static PasteShortcutMode GetStoredPasteShortcutMode(
        SnippetActionType actionType,
        PasteShortcutMode pasteShortcutMode)
    {
        return actionType == SnippetActionType.PasteText
            ? pasteShortcutMode
            : PasteShortcutMode.CtrlV;
    }

    private static string? GetStoredLaunchUrl(SnippetActionType actionType, string? launchUrl)
    {
        return actionType == SnippetActionType.LaunchUrl ? NormalizeOptionalText(launchUrl) : null;
    }

    private static SnippetMediaProvider? GetStoredMediaProvider(
        SnippetActionType actionType,
        SnippetMediaProvider? mediaProvider)
    {
        return actionType == SnippetActionType.MediaAction
            ? mediaProvider ?? SnippetMediaProvider.System
            : null;
    }

    private static SnippetMediaCommand? GetStoredMediaCommand(
        SnippetActionType actionType,
        SnippetMediaCommand? mediaCommand)
    {
        return actionType == SnippetActionType.MediaAction
            ? mediaCommand ?? SnippetMediaCommand.PlayPause
            : null;
    }

    private static string? GetStoredTerminalCommand(SnippetActionType actionType, string? terminalCommand)
    {
        return actionType == SnippetActionType.TerminalCommand
            ? NormalizeOptionalText(terminalCommand)
            : null;
    }

    private static SnippetTerminalShell? GetStoredTerminalShell(
        SnippetActionType actionType,
        SnippetTerminalShell? terminalShell)
    {
        return actionType == SnippetActionType.TerminalCommand
            ? terminalShell ?? SnippetTerminalShell.Cmd
            : null;
    }

    private static bool GetStoredRunAsAdministrator(
        SnippetActionType actionType,
        bool runAsAdministrator)
    {
        return actionType == SnippetActionType.TerminalCommand && runAsAdministrator;
    }

    private static SlotImageMode GetStoredSlotImageMode(SlotImageMode slotImageMode, string? imagePath)
    {
        return slotImageMode == SlotImageMode.Auto && !string.IsNullOrWhiteSpace(imagePath)
            ? SlotImageMode.Custom
            : slotImageMode;
    }

    private static AutoIconCacheEntry? GetStoredAutoIcon(
        SnippetActionType actionType,
        SlotImageMode slotImageMode,
        AutoIconCacheEntry? autoIcon)
    {
        return actionType == SnippetActionType.LaunchFile && slotImageMode != SlotImageMode.None
            ? autoIcon
            : null;
    }

    private static void SetAutoIcon(HotkeyAction action, AutoIconCacheEntry? autoIcon)
    {
        action.AutoIconPath = autoIcon?.IconPath;
        action.AutoIconSourcePath = autoIcon?.SourcePath;
        action.AutoIconSourceLastWriteTimeUtc = autoIcon?.SourceLastWriteTimeUtc;
        action.AutoIconSourceLength = autoIcon?.SourceLength;
    }
}
