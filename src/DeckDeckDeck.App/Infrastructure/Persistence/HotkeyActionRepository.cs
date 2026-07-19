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

        data = data.NormalizeForStorage();
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

        data = data.NormalizeForStorage();
        var action = dbContext.HotkeyActions.First(item => item.Id == id);
        ApplySaveData(action, data);
        action.UpdatedAt = DateTime.UtcNow;
        dbContext.SaveChanges();

        return action;
    }

    public HotkeyAction SetEnabled(Guid id, bool isEnabled)
    {
        using var dbContext = _dbContextFactory.Create();

        var action = dbContext.HotkeyActions.First(item => item.Id == id);
        action.IsEnabled = isEnabled;
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
        action.Title = data.Title;
        action.HotkeyVirtualKey = data.Gesture is null ? null : (int)data.Gesture.VirtualKey;
        action.HotkeyModifiers = data.Gesture?.Modifiers ?? HotkeyModifiers.None;
        action.IsEnabled = data.IsEnabled;
        action.Content = data.Content;
        action.ActionType = data.ActionType;
        action.PasteShortcutMode = data.PasteShortcutMode;
        action.LaunchPath = data.LaunchPath;
        action.FileActionMode = data.FileActionMode;
        action.LaunchUrl = data.LaunchUrl;
        action.MediaProvider = data.MediaProvider;
        action.MediaCommand = data.MediaCommand;
        action.TerminalCommand = data.TerminalCommand;
        action.TerminalShell = data.TerminalShell;
        action.OpenTerminalWindow = data.OpenTerminalWindow;
        action.TerminalWorkingDirectory = data.TerminalWorkingDirectory;
        action.AdbDeviceIp = data.AdbDeviceIp;
        action.RunAsAdministrator = data.RunAsAdministrator;
        action.SlotImageMode = data.SlotImageMode;
        action.Description = data.Description;
        action.ImagePath = data.ImagePath;
        action.ThumbnailPath = data.ThumbnailPath;
        SetAutoIcon(action, data.AutoIcon);
    }

    private static void SetAutoIcon(HotkeyAction action, AutoIconCacheEntry? autoIcon)
    {
        action.AutoIconPath = autoIcon?.IconPath;
        action.AutoIconSourcePath = autoIcon?.SourcePath;
        action.AutoIconSourceLastWriteTimeUtc = autoIcon?.SourceLastWriteTimeUtc;
        action.AutoIconSourceLength = autoIcon?.SourceLength;
    }
}
