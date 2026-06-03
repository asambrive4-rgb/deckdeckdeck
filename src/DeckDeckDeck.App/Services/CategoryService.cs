using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using Microsoft.EntityFrameworkCore;

namespace DeckDeckDeck.App.Services;

public sealed class CategoryService
{
    private readonly AppDbContextFactory _dbContextFactory;

    public CategoryService(AppDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyList<Category> GetAll()
    {
        using var dbContext = _dbContextFactory.Create();

        return dbContext.Categories
            .AsNoTracking()
            .ToList()
            .OrderBy(category => category.SlotKey.GetSortOrder())
            .ToList();
    }

    public Category? GetById(Guid id)
    {
        using var dbContext = _dbContextFactory.Create();

        return dbContext.Categories
            .AsNoTracking()
            .FirstOrDefault(category => category.Id == id);
    }

    public Category Create(SlotKey slotKey, string name, string? description)
    {
        using var dbContext = _dbContextFactory.Create();

        var now = DateTime.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            SlotKey = slotKey,
            Name = name.Trim(),
            Description = NormalizeOptionalText(description),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Categories.Add(category);
        dbContext.SaveChanges();

        return category;
    }

    public Category Update(Guid id, string name, string? description)
    {
        using var dbContext = _dbContextFactory.Create();

        var category = dbContext.Categories.First(item => item.Id == id);
        category.Name = name.Trim();
        category.Description = NormalizeOptionalText(description);
        category.UpdatedAt = DateTime.UtcNow;

        dbContext.SaveChanges();

        return category;
    }

    public void Delete(Guid id)
    {
        using var dbContext = _dbContextFactory.Create();

        var category = dbContext.Categories.First(item => item.Id == id);
        dbContext.Categories.Remove(category);
        dbContext.SaveChanges();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
