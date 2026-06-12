using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Services;

public sealed class SettingsService : ISettingsStore
{
    private readonly SettingEntryStore _entryStore;
    private readonly object _settingsLock = new();
    private readonly SlotSettingsService _slotSettingsService;
    private bool _defaultsEnsured;
    private AppSettings? _settingsCache;

    public SettingsService(AppDbContextFactory dbContextFactory)
    {
        _entryStore = new SettingEntryStore(dbContextFactory);
        _slotSettingsService = new SlotSettingsService(_entryStore);
    }

    public AppSettings Load()
    {
        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _settingsCache ??= LoadFromStore();

            return Clone(_settingsCache);
        }
    }

    public void EnsureDefaults()
    {
        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
        }
    }

    public void SetCategorySlotEnabled(SlotKey slotKey, bool enabled)
    {
        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _slotSettingsService.SetCategorySlotEnabled(slotKey, enabled);
            _settingsCache?.EnabledCategorySlotKeys[slotKey] = enabled;
        }
    }

    public void SetSnippetSlotEnabled(SlotKey slotKey, bool enabled)
    {
        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _slotSettingsService.SetSnippetSlotEnabled(slotKey, enabled);
            _settingsCache?.EnabledSnippetSlotKeys[slotKey] = enabled;
        }
    }

    public void SetLastBackupCreatedAt(DateTimeOffset createdAt)
    {
        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _entryStore.Upsert(
                SettingsKeys.LastBackupCreatedAt,
                SettingsValueParser.FormatNullableDateTimeOffset(createdAt));
            if (_settingsCache is not null)
            {
                _settingsCache.LastBackupCreatedAt = createdAt;
            }
        }
    }

    public void Save(AppSettings settings)
    {
        var values = new[]
        {
            new KeyValuePair<string, string>(
                SettingsKeys.BringWindowToFrontOnHotkey,
                settings.BringWindowToFrontOnHotkey.ToString()),
            new KeyValuePair<string, string>(
                SettingsKeys.AutoHideAfterPaste,
                settings.AutoHideAfterPaste.ToString()),
            new KeyValuePair<string, string>(
                SettingsKeys.RestoreClipboardAfterPaste,
                settings.RestoreClipboardAfterPaste.ToString()),
            new KeyValuePair<string, string>(
                SettingsKeys.AutoBackupEnabled,
                settings.AutoBackupEnabled.ToString()),
            new KeyValuePair<string, string>(
                SettingsKeys.BackupFolderPath,
                settings.BackupFolderPath),
            new KeyValuePair<string, string>(
                SettingsKeys.AutoBackupRetentionCount,
                settings.AutoBackupRetentionCount.ToString()),
            new KeyValuePair<string, string>(
                SettingsKeys.LastBackupCreatedAt,
                SettingsValueParser.FormatNullableDateTimeOffset(settings.LastBackupCreatedAt)),
            new KeyValuePair<string, string>(
                SettingsKeys.SpotifyClientId,
                settings.SpotifyClientId),
            new KeyValuePair<string, string>(
                SettingsKeys.SpotifyAccessToken,
                ProtectedSettingValueService.Protect(settings.SpotifyAccessToken)),
            new KeyValuePair<string, string>(
                SettingsKeys.SpotifyRefreshToken,
                ProtectedSettingValueService.Protect(settings.SpotifyRefreshToken)),
            new KeyValuePair<string, string>(
                SettingsKeys.SpotifyTokenExpiresAt,
                SettingsValueParser.FormatNullableDateTimeOffset(settings.SpotifyTokenExpiresAt)),
            new KeyValuePair<string, string>(
                SettingsKeys.SpotifyConnectedUserDisplayName,
                settings.SpotifyConnectedUserDisplayName),
            new KeyValuePair<string, string>(SettingsKeys.HomeHotkey, settings.HomeHotkey),
            new KeyValuePair<string, string>(SettingsKeys.DirectCategoryHotkeys, settings.DirectCategoryHotkeys),
            new KeyValuePair<string, string>(
                SettingsKeys.LastWindowLeft,
                SettingsValueParser.FormatNullableDouble(settings.LastWindowLeft)),
            new KeyValuePair<string, string>(
                SettingsKeys.LastWindowTop,
                SettingsValueParser.FormatNullableDouble(settings.LastWindowTop)),
            new KeyValuePair<string, string>(
                SettingsKeys.LastWindowScreenDeviceName,
                settings.LastWindowScreenDeviceName ?? string.Empty)
        };

        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _entryStore.UpsertMany(values.Concat(_slotSettingsService.ToSettingEntries(settings)));
            _settingsCache = Clone(settings);
        }
    }

    public void SaveWindowPlacement(double left, double top, string screenDeviceName)
    {
        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _settingsCache ??= LoadFromStore();

            if (_settingsCache.LastWindowLeft == left
                && _settingsCache.LastWindowTop == top
                && _settingsCache.LastWindowScreenDeviceName == screenDeviceName)
            {
                return;
            }

            _entryStore.UpsertMany(
            [
                new KeyValuePair<string, string>(
                    SettingsKeys.LastWindowLeft,
                    SettingsValueParser.FormatNullableDouble(left)),
                new KeyValuePair<string, string>(
                    SettingsKeys.LastWindowTop,
                    SettingsValueParser.FormatNullableDouble(top)),
                new KeyValuePair<string, string>(
                    SettingsKeys.LastWindowScreenDeviceName,
                    screenDeviceName)
            ]);

            _settingsCache.LastWindowLeft = left;
            _settingsCache.LastWindowTop = top;
            _settingsCache.LastWindowScreenDeviceName = screenDeviceName;
        }
    }

    private void EnsureDefaultsLocked()
    {
        if (_defaultsEnsured)
        {
            return;
        }

        _entryStore.EnsureDefaults(SettingsKeys.Defaults.Concat(_slotSettingsService.GetDefaultEntries()));
        _defaultsEnsured = true;
    }

    private AppSettings LoadFromStore()
    {
        var entries = _entryStore.LoadAll();

        return new AppSettings
        {
            BringWindowToFrontOnHotkey = SettingsValueParser.ReadBool(
                entries,
                SettingsKeys.BringWindowToFrontOnHotkey,
                true),
            AutoHideAfterPaste = SettingsValueParser.ReadBool(
                entries,
                SettingsKeys.AutoHideAfterPaste,
                true),
            RestoreClipboardAfterPaste = SettingsValueParser.ReadBool(
                entries,
                SettingsKeys.RestoreClipboardAfterPaste,
                true),
            AutoBackupEnabled = SettingsValueParser.ReadBool(
                entries,
                SettingsKeys.AutoBackupEnabled,
                false),
            BackupFolderPath = SettingsValueParser.ReadString(
                entries,
                SettingsKeys.BackupFolderPath,
                string.Empty),
            AutoBackupRetentionCount = SettingsValueParser.ReadInt(
                entries,
                SettingsKeys.AutoBackupRetentionCount,
                10),
            LastBackupCreatedAt = SettingsValueParser.ReadNullableDateTimeOffset(
                entries,
                SettingsKeys.LastBackupCreatedAt),
            SpotifyClientId = SettingsValueParser.ReadString(
                entries,
                SettingsKeys.SpotifyClientId,
                string.Empty),
            SpotifyAccessToken = ProtectedSettingValueService.Unprotect(SettingsValueParser.ReadString(
                entries,
                SettingsKeys.SpotifyAccessToken,
                string.Empty)),
            SpotifyRefreshToken = ProtectedSettingValueService.Unprotect(SettingsValueParser.ReadString(
                entries,
                SettingsKeys.SpotifyRefreshToken,
                string.Empty)),
            SpotifyTokenExpiresAt = SettingsValueParser.ReadNullableDateTimeOffset(
                entries,
                SettingsKeys.SpotifyTokenExpiresAt),
            SpotifyConnectedUserDisplayName = SettingsValueParser.ReadString(
                entries,
                SettingsKeys.SpotifyConnectedUserDisplayName,
                string.Empty),
            HomeHotkey = SettingsValueParser.ReadString(
                entries,
                SettingsKeys.HomeHotkey,
                "Ctrl + Numpad0"),
            DirectCategoryHotkeys = SettingsValueParser.ReadString(
                entries,
                SettingsKeys.DirectCategoryHotkeys,
                "Ctrl + Numpad1~9, /, *, -, +, ."),
            LastWindowLeft = SettingsValueParser.ReadNullableDouble(entries, SettingsKeys.LastWindowLeft),
            LastWindowTop = SettingsValueParser.ReadNullableDouble(entries, SettingsKeys.LastWindowTop),
            LastWindowScreenDeviceName = SettingsValueParser.ReadNullableString(
                entries,
                SettingsKeys.LastWindowScreenDeviceName),
            EnabledCategorySlotKeys = _slotSettingsService.ReadCategorySlotStates(entries),
            EnabledSnippetSlotKeys = _slotSettingsService.ReadSnippetSlotStates(entries)
        };
    }

    private static AppSettings Clone(AppSettings settings)
    {
        return new AppSettings
        {
            BringWindowToFrontOnHotkey = settings.BringWindowToFrontOnHotkey,
            AutoHideAfterPaste = settings.AutoHideAfterPaste,
            RestoreClipboardAfterPaste = settings.RestoreClipboardAfterPaste,
            AutoBackupEnabled = settings.AutoBackupEnabled,
            BackupFolderPath = settings.BackupFolderPath,
            AutoBackupRetentionCount = settings.AutoBackupRetentionCount,
            LastBackupCreatedAt = settings.LastBackupCreatedAt,
            SpotifyClientId = settings.SpotifyClientId,
            SpotifyAccessToken = settings.SpotifyAccessToken,
            SpotifyRefreshToken = settings.SpotifyRefreshToken,
            SpotifyTokenExpiresAt = settings.SpotifyTokenExpiresAt,
            SpotifyConnectedUserDisplayName = settings.SpotifyConnectedUserDisplayName,
            EnabledCategorySlotKeys = new Dictionary<SlotKey, bool>(settings.EnabledCategorySlotKeys),
            EnabledSnippetSlotKeys = new Dictionary<SlotKey, bool>(settings.EnabledSnippetSlotKeys),
            HomeHotkey = settings.HomeHotkey,
            DirectCategoryHotkeys = settings.DirectCategoryHotkeys,
            LastWindowLeft = settings.LastWindowLeft,
            LastWindowTop = settings.LastWindowTop,
            LastWindowScreenDeviceName = settings.LastWindowScreenDeviceName
        };
    }
}
