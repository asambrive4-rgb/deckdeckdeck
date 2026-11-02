// 역할: 윈도우에 설치된 앱(Microsoft Store/UWP 앱 및 데스크톱 앱) 목록을 shell:AppsFolder에서 조회하는 카탈로그입니다.
using System.Runtime.InteropServices;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed record InstalledAppEntry(
    string DisplayName,
    string LaunchPath,
    string Subtitle);

public static class WindowsInstalledAppCatalog
{
    public static IReadOnlyList<InstalledAppEntry> GetInstalledApps()
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
        {
            return Array.Empty<InstalledAppEntry>();
        }

        object? shellObj = null;
        object? folderObj = null;
        object? itemsObj = null;
        var entries = new List<InstalledAppEntry>();

        try
        {
            shellObj = Activator.CreateInstance(shellType);
            if (shellObj is null)
            {
                return Array.Empty<InstalledAppEntry>();
            }

            dynamic shell = shellObj;
            folderObj = shell.NameSpace("shell:AppsFolder");
            if (folderObj is null)
            {
                return Array.Empty<InstalledAppEntry>();
            }

            dynamic folder = folderObj;
            itemsObj = folder.Items();
            if (itemsObj is null)
            {
                return Array.Empty<InstalledAppEntry>();
            }

            dynamic items = itemsObj;
            foreach (dynamic item in items)
            {
                try
                {
                    string name = item?.Name?.ToString()?.Trim() ?? string.Empty;
                    string rawPath = item?.Path?.ToString()?.Trim() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rawPath))
                    {
                        continue;
                    }

                    if (rawPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        rawPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        rawPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase) ||
                        rawPath.EndsWith(".chm", StringComparison.OrdinalIgnoreCase) ||
                        rawPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                        rawPath.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) ||
                        rawPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string launchPath = rawPath.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase)
                        ? rawPath
                        : @"shell:AppsFolder\" + rawPath;

                    string subtitle;
                    if (rawPath.Contains('!'))
                    {
                        subtitle = "Microsoft Store / 패키지 앱";
                    }
                    else if (rawPath.Contains('\\') || rawPath.Contains('/'))
                    {
                        subtitle = rawPath;
                    }
                    else
                    {
                        subtitle = "Windows 등록 앱";
                    }

                    entries.Add(new InstalledAppEntry(name, launchPath, subtitle));
                }
                catch
                {
                    // 개별 앱 정보 읽기 실패 시 건너뜁니다.
                }
                finally
                {
                    if (item is not null && Marshal.IsComObject(item))
                    {
                        Marshal.ReleaseComObject(item);
                    }
                }
            }
        }
        catch
        {
            return Array.Empty<InstalledAppEntry>();
        }
        finally
        {
            if (itemsObj is not null && Marshal.IsComObject(itemsObj))
            {
                Marshal.ReleaseComObject(itemsObj);
            }

            if (folderObj is not null && Marshal.IsComObject(folderObj))
            {
                Marshal.ReleaseComObject(folderObj);
            }

            if (shellObj is not null && Marshal.IsComObject(shellObj))
            {
                Marshal.ReleaseComObject(shellObj);
            }
        }

        return entries
            .DistinctBy(x => x.LaunchPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
