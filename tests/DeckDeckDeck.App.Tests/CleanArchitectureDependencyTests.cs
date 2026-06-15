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
            "DeckDeckDeck.App.Infrastructure",
            "DeckDeckDeck.App.Composition",
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

    [Fact]
    public void ViewModelsDoNotReferenceInfrastructureOrComposition()
    {
        var projectRoot = FindProjectRoot();
        var checkedFiles = Directory
            .EnumerateFiles(
                Path.Combine(projectRoot, "src", "DeckDeckDeck.App", "ViewModels"),
                "*.cs",
                SearchOption.AllDirectories)
            .ToList();
        var forbiddenTokens = new[]
        {
            "DeckDeckDeck.App.Infrastructure",
            "DeckDeckDeck.App.Composition"
        };

        var violations = FindTokenViolations(projectRoot, checkedFiles, forbiddenTokens);

        Assert.Empty(violations);
    }

    [Fact]
    public void CompositionIsOnlyReferencedByCompositionRoot()
    {
        var projectRoot = FindProjectRoot();
        var appRoot = Path.Combine(projectRoot, "src", "DeckDeckDeck.App");
        var compositionRoot = Path.Combine(appRoot, "Composition");
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(appRoot, "MainWindow.xaml.cs")
        };
        var checkedFiles = Directory
            .EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.StartsWith(compositionRoot, StringComparison.OrdinalIgnoreCase)
                && !allowedFiles.Contains(path))
            .ToList();

        var violations = FindTokenViolations(
            projectRoot,
            checkedFiles,
            ["DeckDeckDeck.App.Composition"]);

        Assert.Empty(violations);
    }

    private static IReadOnlyList<string> FindTokenViolations(
        string projectRoot,
        IReadOnlyList<string> checkedFiles,
        IReadOnlyList<string> forbiddenTokens)
    {
        return checkedFiles
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => text.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(projectRoot, path)} -> {token}");
            })
            .ToList();
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
