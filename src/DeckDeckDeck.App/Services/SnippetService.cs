using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using Microsoft.EntityFrameworkCore;

namespace DeckDeckDeck.App.Services;

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
        string? launchPath = null)
    {
        using var dbContext = _dbContextFactory.Create();

        var now = DateTime.UtcNow;
        var snippet = new Snippet
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            SlotKey = slotKey,
            Title = title.Trim(),
            Content = GetStoredContent(actionType, content),
            ActionType = actionType,
            LaunchPath = GetStoredLaunchPath(actionType, launchPath),
            Description = NormalizeOptionalText(description),
            ImagePath = imagePath,
            ThumbnailPath = thumbnailPath,
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
        UpdateText(snippet, title, content, description, SnippetActionType.PasteText, null);

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
        string? launchPath = null)
    {
        using var dbContext = _dbContextFactory.Create();

        var snippet = dbContext.Snippets.First(item => item.Id == id);
        UpdateText(snippet, title, content, description, actionType, launchPath);
        snippet.ImagePath = imagePath;
        snippet.ThumbnailPath = thumbnailPath;

        dbContext.SaveChanges();

        return snippet;
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

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetStoredContent(SnippetActionType actionType, string content)
    {
        return actionType == SnippetActionType.LaunchFile ? string.Empty : content;
    }

    private static string? GetStoredLaunchPath(SnippetActionType actionType, string? launchPath)
    {
        return actionType == SnippetActionType.LaunchFile ? NormalizeOptionalText(launchPath) : null;
    }

    private static void UpdateText(
        Snippet snippet,
        string title,
        string content,
        string? description,
        SnippetActionType actionType,
        string? launchPath)
    {
        snippet.Title = title.Trim();
        snippet.Content = GetStoredContent(actionType, content);
        snippet.ActionType = actionType;
        snippet.LaunchPath = GetStoredLaunchPath(actionType, launchPath);
        snippet.Description = NormalizeOptionalText(description);
        snippet.UpdatedAt = DateTime.UtcNow;
    }
}
