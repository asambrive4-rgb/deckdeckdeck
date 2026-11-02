// 역할: 파일 및 앱 실행 경로(환경변수 경로, shell:AppsFolder 앱 경로)의 판별, 확장, 이식 가능한 경로 변환 규칙을 담당합니다.
using System.IO;

namespace DeckDeckDeck.App.Domain;

public static class LaunchPathRules
{
    public const string ShellAppsFolderPrefix = @"shell:AppsFolder\";
    private const string ShellAppsFolderAltPrefix = @"shell:AppsFolder/";

    private static readonly (string FolderPath, string EnvVar)[] PortableFolderCandidates =
        new (Environment.SpecialFolder Folder, string EnvVar)[]
        {
            (Environment.SpecialFolder.LocalApplicationData, "%LOCALAPPDATA%"),
            (Environment.SpecialFolder.ApplicationData, "%APPDATA%"),
            (Environment.SpecialFolder.ProgramFilesX86, "%ProgramFiles(x86)%"),
            (Environment.SpecialFolder.ProgramFiles, "%ProgramFiles%"),
        }
        .Select(c => (FolderPath: Environment.GetFolderPath(c.Folder), c.EnvVar))
        .Where(c => !string.IsNullOrWhiteSpace(c.FolderPath))
        .OrderByDescending(c => c.FolderPath.Length)
        .ToArray();

    public static bool IsShellAppsFolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();
        if (trimmed.StartsWith(ShellAppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Length > ShellAppsFolderPrefix.Length;
        }

        if (trimmed.StartsWith(ShellAppsFolderAltPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Length > ShellAppsFolderAltPrefix.Length;
        }

        return false;
    }

    public static string NormalizeShellAppsFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (trimmed.StartsWith(ShellAppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ShellAppsFolderPrefix + trimmed[ShellAppsFolderPrefix.Length..];
        }

        if (trimmed.StartsWith(ShellAppsFolderAltPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ShellAppsFolderPrefix + trimmed[ShellAppsFolderAltPrefix.Length..];
        }

        return trimmed;
    }

    public static string ExpandPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (IsShellAppsFolderPath(trimmed))
        {
            return NormalizeShellAppsFolderPath(trimmed);
        }

        return Environment.ExpandEnvironmentVariables(trimmed);
    }

    public static string ToPortablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (trimmed.Contains('%'))
        {
            return trimmed;
        }

        if (IsShellAppsFolderPath(trimmed))
        {
            return NormalizeShellAppsFolderPath(trimmed);
        }

        foreach (var (folderPath, envVar) in PortableFolderCandidates)
        {
            if (IsSubPathOf(trimmed, folderPath, out var relativePath))
            {
                return string.IsNullOrEmpty(relativePath)
                    ? envVar
                    : $@"{envVar}\{relativePath.Replace('/', '\\').TrimStart('\\')}";
            }
        }

        return trimmed;
    }

    private static bool IsSubPathOf(string path, string basePath, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrEmpty(basePath))
        {
            return false;
        }

        var normalizedBase = Path.TrimEndingDirectorySeparator(basePath);
        if (path.Equals(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith(normalizedBase + "\\", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(normalizedBase + "/", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = path[(normalizedBase.Length + 1)..];
            return true;
        }

        return false;
    }
}
