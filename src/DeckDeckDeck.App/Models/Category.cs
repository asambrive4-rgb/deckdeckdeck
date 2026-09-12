// 역할: 슬롯들을 묶어 관리하는 카테고리의 고유 식별자, 이름, 색상 정보를 담는 데이터 모델입니다.
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
