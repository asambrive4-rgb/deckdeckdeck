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

    public Category? GetBySlotKey(SlotKey slotKey)
    {
        using var dbContext = _dbContextFactory.Create();

        return dbContext.Categories
            .AsNoTracking()
            .FirstOrDefault(category => category.SlotKey == slotKey);
    }

    public Category Create(
        SlotKey slotKey,
        string name,
        string? description,
        string? imagePath = null,
        string? thumbnailPath = null)
    {
        using var dbContext = _dbContextFactory.Create();

        var now = DateTime.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            SlotKey = slotKey,
            Name = name.Trim(),
            Description = NormalizeOptionalText(description),
            ImagePath = imagePath,
            ThumbnailPath = thumbnailPath,
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
        UpdateText(category, name, description);

        dbContext.SaveChanges();

        return category;
    }

    public Category Update(
        Guid id,
        string name,
        string? description,
        string? imagePath,
        string? thumbnailPath)
    {
        using var dbContext = _dbContextFactory.Create();

        var category = dbContext.Categories.First(item => item.Id == id);
        UpdateText(category, name, description);
        category.ImagePath = imagePath;
        category.ThumbnailPath = thumbnailPath;

        dbContext.SaveChanges();

        return category;
    }

    public IReadOnlyList<ImageFileSet> Delete(Guid id)
    {
        using var dbContext = _dbContextFactory.Create();

        var category = dbContext.Categories
            .Include(item => item.Snippets)
            .First(item => item.Id == id);
        var imageFiles = new List<ImageFileSet>
        {
            new(category.ImagePath, category.ThumbnailPath)
        };
        imageFiles.AddRange(category.Snippets.Select(snippet => new ImageFileSet(
            snippet.ImagePath,
            snippet.ThumbnailPath)));

        dbContext.Categories.Remove(category);
        dbContext.SaveChanges();

        return imageFiles;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void UpdateText(Category category, string name, string? description)
    {
        category.Name = name.Trim();
        category.Description = NormalizeOptionalText(description);
        category.UpdatedAt = DateTime.UtcNow;
    }
}
