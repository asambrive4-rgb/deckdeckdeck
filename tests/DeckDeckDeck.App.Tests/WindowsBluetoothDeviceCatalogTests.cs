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
