// 역할: 슬롯에 적용되는 원본 이미지와 썸네일 이미지 파일 경로 쌍을 보관하는 데이터 모델입니다.
namespace DeckDeckDeck.App.Models;

public sealed record ImageFileSet(string? ImagePath, string? ThumbnailPath);
