using Microsoft.EntityFrameworkCore;

namespace DeckDeckDeck.App.Data;

public sealed class AppDbContextFactory
{
    private readonly string _databasePath;

    public AppDbContextFactory(string databasePath)
    {
        _databasePath = databasePath;
    }

    public AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        return new AppDbContext(options);
    }

    public void EnsureCreated()
    {
        using var dbContext = Create();
        dbContext.Database.EnsureCreated();
    }
}
