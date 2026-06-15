using DeckDeckDeck.App.Composition;
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

public sealed record CategoryTransferResult(Category Category, IReadOnlyList<ImageFileSet> OverwrittenImageFiles);

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContextFactory _dbContextFactory;

    public CategoryRepository(AppDbContextFactory dbContextFactory)
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

    public CategoryTransferResult CopyToSlot(
        Guid sourceId,
        SlotKey targetSlotKey,
        Func<ImageFileSet, ImageFileSet> copyImageFiles)
    {
        using var dbContext = _dbContextFactory.Create();
        var source = dbContext.Categories
            .AsNoTracking()
            .Include(item => item.Snippets)
            .First(item => item.Id == sourceId);

        if (source.SlotKey == targetSlotKey)
        {
            throw new InvalidOperationException("같은 슬롯으로는 복사할 수 없습니다.");
        }

        using var transaction = dbContext.Database.BeginTransaction();
        var overwrittenImageFiles = RemoveCategoryInSlot(dbContext, targetSlotKey);
        var now = DateTime.UtcNow;
        var categoryImageFiles = copyImageFiles(new ImageFileSet(source.ImagePath, source.ThumbnailPath));
        var copiedCategory = new Category
        {
            Id = Guid.NewGuid(),
            SlotKey = targetSlotKey,
            Name = source.Name,
            Description = source.Description,
            ImagePath = categoryImageFiles.ImagePath,
            ThumbnailPath = categoryImageFiles.ThumbnailPath,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var sourceSnippet in source.Snippets.OrderBy(snippet => snippet.SlotKey.GetSortOrder()))
        {
            var snippetImageFiles = copyImageFiles(new ImageFileSet(
                sourceSnippet.ImagePath,
                sourceSnippet.ThumbnailPath));
            copiedCategory.Snippets.Add(new Snippet
            {
                Id = Guid.NewGuid(),
                CategoryId = copiedCategory.Id,
                SlotKey = sourceSnippet.SlotKey,
                Title = sourceSnippet.Title,
                Content = sourceSnippet.Content,
                ActionType = sourceSnippet.ActionType,
                PasteShortcutMode = sourceSnippet.PasteShortcutMode,
                LaunchPath = sourceSnippet.LaunchPath,
                LaunchUrl = sourceSnippet.LaunchUrl,
                MediaProvider = sourceSnippet.MediaProvider,
                MediaCommand = sourceSnippet.MediaCommand,
                SlotImageMode = sourceSnippet.SlotImageMode,
                Description = sourceSnippet.Description,
                ImagePath = snippetImageFiles.ImagePath,
                ThumbnailPath = snippetImageFiles.ThumbnailPath,
                AutoIconPath = sourceSnippet.AutoIconPath,
                AutoIconSourcePath = sourceSnippet.AutoIconSourcePath,
                AutoIconSourceLastWriteTimeUtc = sourceSnippet.AutoIconSourceLastWriteTimeUtc,
                AutoIconSourceLength = sourceSnippet.AutoIconSourceLength,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        dbContext.Categories.Add(copiedCategory);
        dbContext.SaveChanges();
        transaction.Commit();

        return new CategoryTransferResult(copiedCategory, overwrittenImageFiles);
    }

    public CategoryTransferResult MoveToSlot(Guid sourceId, SlotKey targetSlotKey)
    {
        using var dbContext = _dbContextFactory.Create();
        var source = dbContext.Categories.First(item => item.Id == sourceId);

        if (source.SlotKey == targetSlotKey)
        {
            throw new InvalidOperationException("같은 슬롯으로는 이동할 수 없습니다.");
        }

        using var transaction = dbContext.Database.BeginTransaction();
        var overwrittenImageFiles = RemoveCategoryInSlot(dbContext, targetSlotKey);

        source.SlotKey = targetSlotKey;
        source.UpdatedAt = DateTime.UtcNow;
        dbContext.SaveChanges();
        transaction.Commit();

        return new CategoryTransferResult(source, overwrittenImageFiles);
    }

    private static IReadOnlyList<ImageFileSet> RemoveCategoryInSlot(AppDbContext dbContext, SlotKey slotKey)
    {
        var category = dbContext.Categories
            .Include(item => item.Snippets)
            .FirstOrDefault(item => item.SlotKey == slotKey);

        if (category is null)
        {
            return [];
        }

        var imageFiles = GetImageFiles(category);
        dbContext.Categories.Remove(category);
        dbContext.SaveChanges();

        return imageFiles;
    }

    private static IReadOnlyList<ImageFileSet> GetImageFiles(Category category)
    {
        var imageFiles = new List<ImageFileSet>
        {
            new(category.ImagePath, category.ThumbnailPath)
        };
        imageFiles.AddRange(category.Snippets.Select(snippet => new ImageFileSet(
            snippet.ImagePath,
            snippet.ThumbnailPath)));

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

    IReadOnlyList<UseCaseImageFileReference> ICategoryRepository.Delete(Guid id)
    {
        return Delete(id).Select(ToUseCaseImageFileReference).ToList();
    }

    CategoryTransferRepositoryResult ICategoryRepository.CopyToSlot(
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

        return new CategoryTransferRepositoryResult(
            result.Category,
            result.OverwrittenImageFiles.Select(ToUseCaseImageFileReference).ToList());
    }

    CategoryTransferRepositoryResult ICategoryRepository.MoveToSlot(
        Guid sourceId,
        SlotKey targetSlotKey)
    {
        var result = MoveToSlot(sourceId, targetSlotKey);

        return new CategoryTransferRepositoryResult(
            result.Category,
            result.OverwrittenImageFiles.Select(ToUseCaseImageFileReference).ToList());
    }

    private static UseCaseImageFileReference ToUseCaseImageFileReference(ImageFileSet imageFiles)
    {
        return new UseCaseImageFileReference(imageFiles.ImagePath, imageFiles.ThumbnailPath);
    }
}

