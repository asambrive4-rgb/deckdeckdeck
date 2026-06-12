namespace DeckDeckDeck.App.Tests;

public sealed class CleanArchitectureDependencyTests
{
    [Fact]
    public void DomainAndUseCasesDoNotReferenceOuterDetails()
    {
        var projectRoot = FindProjectRoot();
        var checkedFiles = Directory
            .EnumerateFiles(Path.Combine(projectRoot, "src", "DeckDeckDeck.App"), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                path.Contains($"{Path.DirectorySeparatorChar}Domain{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}UseCases{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        var forbiddenTokens = new[]
        {
            "System.Windows",
            "Microsoft.EntityFrameworkCore",
            "DeckDeckDeck.App.Data",
            "DeckDeckDeck.App.Views",
            "DeckDeckDeck.App.ViewModels",
            "DeckDeckDeck.App.Native",
            "HttpClient"
        };

        var violations = checkedFiles
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => text.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(projectRoot, path)} -> {token}");
            })
            .ToList();

        Assert.Empty(violations);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeckDeckDeck.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Project root not found.");
    }
}
