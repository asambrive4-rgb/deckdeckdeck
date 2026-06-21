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

    public DbSet<HotkeyAction> HotkeyActions => Set<HotkeyAction>();

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
            entity.Property(snippet => snippet.PasteShortcutMode).HasConversion<string>().IsRequired();
            entity.Property(snippet => snippet.FileActionMode).HasConversion<string>().IsRequired();
            entity.Property(snippet => snippet.MediaProvider).HasConversion<string>();
            entity.Property(snippet => snippet.MediaCommand).HasConversion<string>();
            entity.Property(snippet => snippet.TerminalShell).HasConversion<string>();
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

        modelBuilder.Entity<HotkeyAction>(entity =>
        {
            entity.HasKey(action => action.Id);
            entity.HasIndex(action => new { action.HotkeyVirtualKey, action.HotkeyModifiers });
            entity.Property(action => action.Id).HasConversion<string>();
            entity.Property(action => action.HotkeyModifiers).HasConversion<int>().IsRequired();
            entity.Property(action => action.ActionType).HasConversion<string>().IsRequired();
            entity.Property(action => action.PasteShortcutMode).HasConversion<string>().IsRequired();
            entity.Property(action => action.FileActionMode).HasConversion<string>().IsRequired();
            entity.Property(action => action.MediaProvider).HasConversion<string>();
            entity.Property(action => action.MediaCommand).HasConversion<string>();
            entity.Property(action => action.TerminalShell).HasConversion<string>();
            entity.Property(action => action.SlotImageMode).HasConversion<string>().IsRequired();
            entity.Property(action => action.Title).IsRequired();
            entity.Property(action => action.Content).IsRequired();
            entity.Property(action => action.IsEnabled).IsRequired();
            entity.Property(action => action.CreatedAt).IsRequired();
            entity.Property(action => action.UpdatedAt).IsRequired();
            entity.Ignore(action => action.Gesture);
            entity.Ignore(action => action.HotkeyDisplayText);
        });

        modelBuilder.Entity<SettingEntry>(entity =>
        {
            entity.HasKey(setting => setting.Key);
            entity.Property(setting => setting.Key).IsRequired();
            entity.Property(setting => setting.Value).IsRequired();
        });
    }
}
