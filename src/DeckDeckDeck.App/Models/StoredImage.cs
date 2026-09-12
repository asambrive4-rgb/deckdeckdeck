// 역할: 프로그램 저장소에 보관된 이미지 파일의 고유 식별자와 파일명 정보를 보관하는 데이터 모델입니다.
namespace DeckDeckDeck.App.Models;

public sealed record StoredImage(string ImagePath, string ThumbnailPath);
