namespace DeckDeckDeck.App.Models;

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public SlotKey SlotKey { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImagePath { get; set; }

    public string? ThumbnailPath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<Snippet> Snippets { get; set; } = [];
}
