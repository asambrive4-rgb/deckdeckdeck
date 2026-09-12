// 역할: 페어링된 블루투스 장치 목록에서 오디오 기기를 정확히 식별해내는지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Infrastructure.Platform;

namespace DeckDeckDeck.App.Tests;

public sealed class WindowsBluetoothDeviceCatalogTests
{
    [Fact]
    public void ContainerIdPropertyKey_MatchesWindowsStandardKey()
    {
        Assert.Equal(
            new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"),
            WindowsBluetoothDeviceCatalog.ContainerIdPropertyFormatId);
        Assert.Equal(2U, WindowsBluetoothDeviceCatalog.ContainerIdPropertyId);
    }
}
