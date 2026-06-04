namespace DeckDeckDeck.App.Services;

public interface IFileIconExtractor
{
    bool TryExtractIcon(string sourcePath, string destinationPngPath);
}
