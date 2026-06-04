using DeckDeckDeck.App.Models;
using Microsoft.EntityFrameworkCore;

namespace DeckDeckDeck.App.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Snippet> Snippets => Set<Snippet>();

    public DbSet<SettingEntry> Settings => Set<SettingEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.HasIndex(category => category.SlotKey).IsUnique();
            entity.Property(category => category.Id).HasConversion<string>();
            entity.Property(category => category.SlotKey).HasConversion<string>().IsRequired();
            entity.Property(category => category.Name).IsRequired();
            entity.Property(category => category.CreatedAt).IsRequired();
            entity.Property(category => category.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<Snippet>(entity =>
        {
            entity.HasKey(snippet => snippet.Id);
            entity.HasIndex(snippet => new { snippet.CategoryId, snippet.SlotKey }).IsUnique();
            entity.Property(snippet => snippet.Id).HasConversion<string>();
            entity.Property(snippet => snippet.CategoryId).HasConversion<string>();
            entity.Property(snippet => snippet.SlotKey).HasConversion<string>().IsRequired();
            entity.Property(snippet => snippet.ActionType).HasConversion<string>().IsRequired();
            entity.Property(snippet => snippet.SlotImageMode).HasConversion<string>().IsRequired();
            entity.Property(snippet => snippet.Title).IsRequired();
            entity.Property(snippet => snippet.Content).IsRequired();
            entity.Property(snippet => snippet.CreatedAt).IsRequired();
            entity.Property(snippet => snippet.UpdatedAt).IsRequired();
            entity.HasOne(snippet => snippet.Category)
                .WithMany(category => category.Snippets)
                .HasForeignKey(snippet => snippet.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SettingEntry>(entity =>
        {
            entity.HasKey(setting => setting.Key);
            entity.Property(setting => setting.Key).IsRequired();
            entity.Property(setting => setting.Value).IsRequired();
        });
    }
}
