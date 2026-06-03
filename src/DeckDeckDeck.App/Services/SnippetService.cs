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

    public Snippet Create(Guid categoryId, SlotKey slotKey, string title, string content, string? description)
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
        snippet.Title = title.Trim();
        snippet.Content = content;
        snippet.Description = NormalizeOptionalText(description);
        snippet.UpdatedAt = DateTime.UtcNow;

        dbContext.SaveChanges();

        return snippet;
    }

    public void Delete(Guid id)
    {
        using var dbContext = _dbContextFactory.Create();

        var snippet = dbContext.Snippets.First(item => item.Id == id);
        dbContext.Snippets.Remove(snippet);
        dbContext.SaveChanges();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
