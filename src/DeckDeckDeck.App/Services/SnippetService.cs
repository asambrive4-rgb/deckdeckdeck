using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using Microsoft.EntityFrameworkCore;

namespace DeckDeckDeck.App.Services;

public sealed record SnippetTransferResult(Snippet Snippet, IReadOnlyList<ImageFileSet> OverwrittenImageFiles);

public sealed class SnippetService
{
    private readonly AppDbContextFactory _dbContextFactory;

    public SnippetService(AppDbContextFactory dbContextFactory)
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
        string title,
        string content,
        string? description,
        string? imagePath = null,
        string? thumbnailPath = null,
        SnippetActionType actionType = SnippetActionType.PasteText,
        string? launchPath = null,
        SlotImageMode slotImageMode = SlotImageMode.Auto,
        AutoIconCacheEntry? autoIcon = null,
        string? launchUrl = null)
    {
        using var dbContext = _dbContextFactory.Create();

        var now = DateTime.UtcNow;
        var storedImageMode = GetStoredSlotImageMode(slotImageMode, imagePath);
        var snippet = new Snippet
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            SlotKey = slotKey,
            Title = title.Trim(),
            Content = GetStoredContent(actionType, content),
            ActionType = actionType,
            LaunchPath = GetStoredLaunchPath(actionType, launchPath),
            LaunchUrl = GetStoredLaunchUrl(actionType, launchUrl),
            SlotImageMode = storedImageMode,
            Description = NormalizeOptionalText(description),
            ImagePath = imagePath,
            ThumbnailPath = thumbnailPath,
            AutoIconPath = GetStoredAutoIcon(actionType, storedImageMode, autoIcon)?.IconPath,
            AutoIconSourcePath = GetStoredAutoIcon(actionType, storedImageMode, autoIcon)?.SourcePath,
            AutoIconSourceLastWriteTimeUtc = GetStoredAutoIcon(actionType, storedImageMode, autoIcon)?.SourceLastWriteTimeUtc,
            AutoIconSourceLength = GetStoredAutoIcon(actionType, storedImageMode, autoIcon)?.SourceLength,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Snippets.Add(snippet);
        dbContext.SaveChanges();

        return snippet;
    }

    public Snippet Update(Guid id, string title, string content, string? description)
    {
        using var dbContext = _dbContextFactory.Create();

        var snippet = dbContext.Snippets.First(item => item.Id == id);
        UpdateText(snippet, title, content, description, SnippetActionType.PasteText, null, null);

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
        string? launchUrl = null)
    {
        using var dbContext = _dbContextFactory.Create();

        var snippet = dbContext.Snippets.First(item => item.Id == id);
        UpdateText(snippet, title, content, description, actionType, launchPath, launchUrl);
        var storedImageMode = GetStoredSlotImageMode(slotImageMode, imagePath);
        var storedAutoIcon = GetStoredAutoIcon(actionType, storedImageMode, autoIcon);
        snippet.SlotImageMode = storedImageMode;
        snippet.ImagePath = imagePath;
        snippet.ThumbnailPath = thumbnailPath;
        SetAutoIcon(snippet, storedAutoIcon);

        dbContext.SaveChanges();

        return snippet;
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
            LaunchPath = source.LaunchPath,
            LaunchUrl = source.LaunchUrl,
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

    private static string? GetStoredLaunchUrl(SnippetActionType actionType, string? launchUrl)
    {
        return actionType == SnippetActionType.LaunchUrl ? NormalizeOptionalText(launchUrl) : null;
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

    private static void SetAutoIcon(Snippet snippet, AutoIconCacheEntry? autoIcon)
    {
        snippet.AutoIconPath = autoIcon?.IconPath;
        snippet.AutoIconSourcePath = autoIcon?.SourcePath;
        snippet.AutoIconSourceLastWriteTimeUtc = autoIcon?.SourceLastWriteTimeUtc;
        snippet.AutoIconSourceLength = autoIcon?.SourceLength;
    }

    private static void UpdateText(
        Snippet snippet,
        string title,
        string content,
        string? description,
        SnippetActionType actionType,
        string? launchPath,
        string? launchUrl)
    {
        snippet.Title = title.Trim();
        snippet.Content = GetStoredContent(actionType, content);
        snippet.ActionType = actionType;
        snippet.LaunchPath = GetStoredLaunchPath(actionType, launchPath);
        snippet.LaunchUrl = GetStoredLaunchUrl(actionType, launchUrl);
        snippet.Description = NormalizeOptionalText(description);
        snippet.UpdatedAt = DateTime.UtcNow;
    }
}
