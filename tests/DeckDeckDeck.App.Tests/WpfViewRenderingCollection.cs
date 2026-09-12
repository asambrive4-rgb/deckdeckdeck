// 역할: UI 렌더링 테스트에서 공통으로 사용할 WPF 뷰 인스턴스 모음을 제공하는 테스트 픽스처입니다.
namespace DeckDeckDeck.App.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfViewRenderingCollection
{
    public const string Name = "WPF view rendering";
}
