// 역할: 프로그램 업데이트 시 구버전의 파일 저장 경로를 최신 경로로 안전하게 이전합니다.
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Persistence;

public sealed class StoredPathMigration : IStoredPathMigrationGateway
{
    private readonly AppDbContextFactory _dbContextFactory;
    private readonly AppStoragePaths _fileStorageService;

    public StoredPathMigration(
        AppDbContextFactory dbContextFactory,
        AppStoragePaths fileStorageService)
    {
        _dbContextFactory = dbContextFactory;
        _fileStorageService = fileStorageService;
    }

    public void NormalizeManagedPaths()
    {
        using var dbContext = _dbContextFactory.Create();
        var changed = false;

        foreach (var category in dbContext.Categories)
        {
            changed |= NormalizePath(value => category.ImagePath = value, category.ImagePath);
            changed |= NormalizePath(value => category.ThumbnailPath = value, category.ThumbnailPath);
        }

        foreach (var snippet in dbContext.Snippets)
        {
            changed |= NormalizePath(value => snippet.ImagePath = value, snippet.ImagePath);
            changed |= NormalizePath(value => snippet.ThumbnailPath = value, snippet.ThumbnailPath);
            changed |= NormalizePath(value => snippet.AutoIconPath = value, snippet.AutoIconPath);
        }

        foreach (var hotkeyAction in dbContext.HotkeyActions)
        {
            changed |= NormalizePath(value => hotkeyAction.ImagePath = value, hotkeyAction.ImagePath);
            changed |= NormalizePath(value => hotkeyAction.ThumbnailPath = value, hotkeyAction.ThumbnailPath);
            changed |= NormalizePath(value => hotkeyAction.AutoIconPath = value, hotkeyAction.AutoIconPath);
        }

        if (changed)
        {
            dbContext.SaveChanges();
        }
    }

    private bool NormalizePath(Action<string?> setValue, string? currentValue)
    {
        if (!_fileStorageService.TryGetManagedRelativePath(currentValue, out var relativePath)
            || string.Equals(currentValue, relativePath, StringComparison.Ordinal))
        {
            return false;
        }

        setValue(relativePath);
        return true;
    }
}
