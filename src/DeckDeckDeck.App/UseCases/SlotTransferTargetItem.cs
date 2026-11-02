// 역할: 카테고리 또는 실행 항목을 다른 슬롯으로 복사·이동할 때 대상 슬롯 정보를 표현하는 데이터 모델입니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.UseCases;

public sealed record SlotTransferTargetItem(SlotKey SlotKey, string Label);
