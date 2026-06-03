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
        string? thumbnailPath = null)
    {
        using var dbContext = _dbContextFactory.Create();

        var now = DateTime.UtcNow;
        var snippet = new Snippet
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            SlotKey = slotKey,
            Title = title.Trim(),
            Content = content,
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
        UpdateText(snippet, title, content, description);

        dbContext.SaveChanges();

        return snippet;
    }

    public Snippet Update(
        Guid id,
        string title,
        string content,
        string? description,
        string? imagePath,
        string? thumbnailPath)
    {
        using var dbContext = _dbContextFactory.Create();

        var snippet = dbContext.Snippets.First(item => item.Id == id);
        UpdateText(snippet, title, content, description);
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

    private static void UpdateText(Snippet snippet, string title, string content, string? description)
    {
        snippet.Title = title.Trim();
        snippet.Content = content;
        snippet.Description = NormalizeOptionalText(description);
        snippet.UpdatedAt = DateTime.UtcNow;
    }
}
