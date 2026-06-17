using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using Microsoft.EntityFrameworkCore;
using UseCaseImageFileReference = DeckDeckDeck.App.UseCases.Ports.ImageFileReference;

namespace DeckDeckDeck.App.Infrastructure.Persistence;

public sealed record SnippetTransferResult(Snippet Snippet, IReadOnlyList<ImageFileSet> OverwrittenImageFiles);

public sealed class SnippetRepository : ISnippetRepository
{
    private readonly AppDbContextFactory _dbContextFactory;

    public SnippetRepository(AppDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyList<Snippet> GetByCategoryId(Guid categoryId)
    {
        using var dbContext = _dbContextFactory.Create();

        return dbContext.Snippets
            .AsNoTracking()
            .Where(snippet => snippet.CategoryId == categoryId)
            .ToList()
            .OrderBy(snippet => snippet.SlotKey.GetSortOrder())
            .ToList();
    }

    public Snippet? GetById(Guid id)
    {
        using var dbContext = _dbContextFactory.Create();

        return dbContext.Snippets
            .AsNoTracking()
            .FirstOrDefault(snippet => snippet.Id == id);
    }

    public Snippet Create(
        Guid categoryId,
        SlotKey slotKey,
        SnippetSaveData data)
    {
        using var dbContext = _dbContextFactory.Create();

        data = data.NormalizeForStorage();
        var now = DateTime.UtcNow;
        var snippet = new Snippet
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            SlotKey = slotKey,
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplySaveData(snippet, data);

        dbContext.Snippets.Add(snippet);
        dbContext.SaveChanges();

        return snippet;
    }

    public Snippet Create(
        Guid categoryId,
        SlotKey slotKey,
        string title,
        string content,
        string? description,
        string? imagePath = null,
        string? thumbnailPath = null,
        SnippetActionType actionType = SnippetActionType.PasteText,
        string? launchPath = null,
        SlotImageMode slotImageMode = SlotImageMode.Auto,
        AutoIconCacheEntry? autoIcon = null,
        string? launchUrl = null,
        SnippetMediaProvider? mediaProvider = null,
        SnippetMediaCommand? mediaCommand = null,
        PasteShortcutMode pasteShortcutMode = PasteShortcutMode.CtrlV,
        string? terminalCommand = null,
        SnippetTerminalShell? terminalShell = null,
        bool runAsAdministrator = true)
    {
        return Create(
            categoryId,
            slotKey,
            new SnippetSaveData(
                title,
                content,
                description,
                imagePath,
                thumbnailPath,
                actionType,
                launchPath,
                slotImageMode,
                autoIcon,
                launchUrl,
                mediaProvider,
                mediaCommand,
                pasteShortcutMode,
                terminalCommand,
                terminalShell,
                runAsAdministrator));
    }

    public Snippet Update(Guid id, string title, string content, string? description)
    {
        using var dbContext = _dbContextFactory.Create();

        var snippet = dbContext.Snippets.First(item => item.Id == id);
        var data = new SnippetSaveData(
                title,
                content,
                description,
                snippet.ImagePath,
                snippet.ThumbnailPath,
                SnippetActionType.PasteText,
                SlotImageMode: snippet.SlotImageMode,
                AutoIcon: AutoIconCacheEntry.FromSnippet(snippet))
            .NormalizeForStorage();
        ApplySaveData(snippet, data);
        snippet.UpdatedAt = DateTime.UtcNow;

        dbContext.SaveChanges();

        return snippet;
    }

    public Snippet Update(
        Guid id,
        SnippetSaveData data)
    {
        using var dbContext = _dbContextFactory.Create();

        data = data.NormalizeForStorage();
        var snippet = dbContext.Snippets.First(item => item.Id == id);
        ApplySaveData(snippet, data);
        snippet.UpdatedAt = DateTime.UtcNow;

        dbContext.SaveChanges();

        return snippet;
    }

    public Snippet Update(
        Guid id,
        string title,
        string content,
        string? description,
        string? imagePath,
        string? thumbnailPath,
        SnippetActionType actionType = SnippetActionType.PasteText,
        string? launchPath = null,
        SlotImageMode slotImageMode = SlotImageMode.Auto,
        AutoIconCacheEntry? autoIcon = null,
        string? launchUrl = null,
        SnippetMediaProvider? mediaProvider = null,
        SnippetMediaCommand? mediaCommand = null,
        PasteShortcutMode pasteShortcutMode = PasteShortcutMode.CtrlV,
        string? terminalCommand = null,
        SnippetTerminalShell? terminalShell = null,
        bool runAsAdministrator = true)
    {
        return Update(
            id,
            new SnippetSaveData(
                title,
                content,
                description,
                imagePath,
                thumbnailPath,
                actionType,
                launchPath,
                slotImageMode,
                autoIcon,
                launchUrl,
                mediaProvider,
                mediaCommand,
                pasteShortcutMode,
                terminalCommand,
                terminalShell,
                runAsAdministrator));
    }

    public void UpdateAutoIcon(Guid id, AutoIconCacheEntry? autoIcon)
    {
        using var dbContext = _dbContextFactory.Create();

        var snippet = dbContext.Snippets.First(item => item.Id == id);
        SetAutoIcon(snippet, autoIcon);

        dbContext.SaveChanges();
    }

    public ImageFileSet Delete(Guid id)
    {
        using var dbContext = _dbContextFactory.Create();

        var snippet = dbContext.Snippets.First(item => item.Id == id);
        var imageFiles = new ImageFileSet(snippet.ImagePath, snippet.ThumbnailPath);
        dbContext.Snippets.Remove(snippet);
        dbContext.SaveChanges();

        return imageFiles;
    }

    public SnippetTransferResult CopyToSlot(
        Guid sourceId,
        SlotKey targetSlotKey,
        Func<ImageFileSet, ImageFileSet> copyImageFiles)
    {
        using var dbContext = _dbContextFactory.Create();
        var source = dbContext.Snippets
            .AsNoTracking()
            .First(item => item.Id == sourceId);

        if (source.SlotKey == targetSlotKey)
        {
            throw new InvalidOperationException("같은 슬롯으로는 복사할 수 없습니다.");
        }

        using var transaction = dbContext.Database.BeginTransaction();
        var overwrittenImageFiles = RemoveSnippetInSlot(dbContext, source.CategoryId, targetSlotKey);
        var now = DateTime.UtcNow;
        var snippetImageFiles = copyImageFiles(new ImageFileSet(source.ImagePath, source.ThumbnailPath));
        var copiedSnippet = CopySnippet(source, targetSlotKey, snippetImageFiles, now);

        dbContext.Snippets.Add(copiedSnippet);
        dbContext.SaveChanges();
        transaction.Commit();

        return new SnippetTransferResult(copiedSnippet, overwrittenImageFiles);
    }

    public SnippetTransferResult MoveToSlot(Guid sourceId, SlotKey targetSlotKey)
    {
        using var dbContext = _dbContextFactory.Create();
        var source = dbContext.Snippets.First(item => item.Id == sourceId);

        if (source.SlotKey == targetSlotKey)
        {
            throw new InvalidOperationException("같은 슬롯으로는 이동할 수 없습니다.");
        }

        using var transaction = dbContext.Database.BeginTransaction();
        var overwrittenImageFiles = RemoveSnippetInSlot(dbContext, source.CategoryId, targetSlotKey);

        source.SlotKey = targetSlotKey;
        source.UpdatedAt = DateTime.UtcNow;
        dbContext.SaveChanges();
        transaction.Commit();

        return new SnippetTransferResult(source, overwrittenImageFiles);
    }

    private static Snippet CopySnippet(
        Snippet source,
        SlotKey targetSlotKey,
        ImageFileSet imageFiles,
        DateTime now)
    {
        return new Snippet
        {
            Id = Guid.NewGuid(),
            CategoryId = source.CategoryId,
            SlotKey = targetSlotKey,
            Title = source.Title,
            Content = source.Content,
            ActionType = source.ActionType,
            PasteShortcutMode = source.PasteShortcutMode,
            LaunchPath = source.LaunchPath,
            LaunchUrl = source.LaunchUrl,
            MediaProvider = source.MediaProvider,
            MediaCommand = source.MediaCommand,
            TerminalCommand = source.TerminalCommand,
            TerminalShell = source.TerminalShell,
            RunAsAdministrator = source.RunAsAdministrator,
            SlotImageMode = source.SlotImageMode,
            Description = source.Description,
            ImagePath = imageFiles.ImagePath,
            ThumbnailPath = imageFiles.ThumbnailPath,
            AutoIconPath = source.AutoIconPath,
            AutoIconSourcePath = source.AutoIconSourcePath,
            AutoIconSourceLastWriteTimeUtc = source.AutoIconSourceLastWriteTimeUtc,
            AutoIconSourceLength = source.AutoIconSourceLength,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static IReadOnlyList<ImageFileSet> RemoveSnippetInSlot(
        AppDbContext dbContext,
        Guid categoryId,
        SlotKey slotKey)
    {
        var snippet = dbContext.Snippets.FirstOrDefault(item =>
            item.CategoryId == categoryId && item.SlotKey == slotKey);

        if (snippet is null)
        {
            return [];
        }

        var imageFiles = new ImageFileSet(snippet.ImagePath, snippet.ThumbnailPath);
        dbContext.Snippets.Remove(snippet);
        dbContext.SaveChanges();

        return [imageFiles];
    }

    private static void SetAutoIcon(Snippet snippet, AutoIconCacheEntry? autoIcon)
    {
        snippet.AutoIconPath = autoIcon?.IconPath;
        snippet.AutoIconSourcePath = autoIcon?.SourcePath;
        snippet.AutoIconSourceLastWriteTimeUtc = autoIcon?.SourceLastWriteTimeUtc;
        snippet.AutoIconSourceLength = autoIcon?.SourceLength;
    }

    private static void ApplySaveData(Snippet snippet, SnippetSaveData data)
    {
        snippet.Title = data.Title;
        snippet.Content = data.Content;
        snippet.ActionType = data.ActionType;
        snippet.PasteShortcutMode = data.PasteShortcutMode;
        snippet.LaunchPath = data.LaunchPath;
        snippet.LaunchUrl = data.LaunchUrl;
        snippet.MediaProvider = data.MediaProvider;
        snippet.MediaCommand = data.MediaCommand;
        snippet.TerminalCommand = data.TerminalCommand;
        snippet.TerminalShell = data.TerminalShell;
        snippet.RunAsAdministrator = data.RunAsAdministrator;
        snippet.SlotImageMode = data.SlotImageMode;
        snippet.Description = data.Description;
        snippet.ImagePath = data.ImagePath;
        snippet.ThumbnailPath = data.ThumbnailPath;
        SetAutoIcon(snippet, data.AutoIcon);
    }

    UseCaseImageFileReference ISnippetRepository.Delete(Guid id)
    {
        return ToUseCaseImageFileReference(Delete(id));
    }

    SnippetTransferRepositoryResult ISnippetRepository.CopyToSlot(
        Guid sourceId,
        SlotKey targetSlotKey,
        Func<UseCaseImageFileReference, UseCaseImageFileReference> copyImageFiles)
    {
        var result = CopyToSlot(
            sourceId,
            targetSlotKey,
            imageFiles =>
            {
                var copied = copyImageFiles(ToUseCaseImageFileReference(imageFiles));
                return new ImageFileSet(copied.ImagePath, copied.ThumbnailPath);
            });

        return new SnippetTransferRepositoryResult(
            result.Snippet,
            result.OverwrittenImageFiles.Select(ToUseCaseImageFileReference).ToList());
    }

    SnippetTransferRepositoryResult ISnippetRepository.MoveToSlot(
        Guid sourceId,
        SlotKey targetSlotKey)
    {
        var result = MoveToSlot(sourceId, targetSlotKey);

        return new SnippetTransferRepositoryResult(
            result.Snippet,
            result.OverwrittenImageFiles.Select(ToUseCaseImageFileReference).ToList());
    }

    private static UseCaseImageFileReference ToUseCaseImageFileReference(ImageFileSet imageFiles)
    {
        return new UseCaseImageFileReference(imageFiles.ImagePath, imageFiles.ThumbnailPath);
    }
}

