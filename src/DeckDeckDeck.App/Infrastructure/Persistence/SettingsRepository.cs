// 역할: 프로그램 전체 환경설정 및 사용자 맞춤 옵션을 읽어오고 안전하게 저장합니다.
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Persistence;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly SettingEntryRepository _entryStore;
    private readonly object _settingsLock = new();
    private readonly SlotSettingsRepository _slotSettingsRepository;
    private bool _defaultsEnsured;
    private AppSettings? _settingsCache;

    public SettingsRepository(AppDbContextFactory dbContextFactory)
    {
        _entryStore = new SettingEntryRepository(dbContextFactory);
        _slotSettingsRepository = new SlotSettingsRepository(_entryStore);
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

    public void ReloadAfterExternalDataChange()
    {
        lock (_settingsLock)
        {
            _defaultsEnsured = false;
            _settingsCache = null;
            EnsureDefaultsLocked();
        }
    }

    public void SetCategorySlotEnabled(SlotKey slotKey, bool enabled)
    {
        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _slotSettingsRepository.SetCategorySlotEnabled(slotKey, enabled);
            _settingsCache?.EnabledCategorySlotKeys[slotKey] = enabled;
        }
    }

    public void SetSnippetSlotEnabled(SlotKey slotKey, bool enabled)
    {
        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _slotSettingsRepository.SetSnippetSlotEnabled(slotKey, enabled);
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

    public void SetPreferredBluetoothDeviceName(string? deviceName)
    {
        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _entryStore.Upsert(
                SettingsKeys.PreferredBluetoothDeviceName,
                deviceName ?? string.Empty);
            if (_settingsCache is not null)
            {
                _settingsCache.PreferredBluetoothDeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName;
            }
        }
    }

    public void Save(AppSettings settings)
    {
        KeyValuePair<string, string>[] values =
        [
            new(SettingsKeys.BringWindowToFrontOnHotkey, settings.BringWindowToFrontOnHotkey.ToString()),
            new(SettingsKeys.AutoHideAfterPaste, settings.AutoHideAfterPaste.ToString()),
            new(SettingsKeys.RestoreClipboardAfterPaste, settings.RestoreClipboardAfterPaste.ToString()),
            new(SettingsKeys.AutoBackupEnabled, settings.AutoBackupEnabled.ToString()),
            new(SettingsKeys.BackupFolderPath, settings.BackupFolderPath),
            new(SettingsKeys.AutoBackupRetentionCount, settings.AutoBackupRetentionCount.ToString()),
            new(SettingsKeys.LastBackupCreatedAt, SettingsValueParser.FormatNullableDateTimeOffset(settings.LastBackupCreatedAt)),
            new(SettingsKeys.SpotifyClientId, settings.SpotifyClientId),
            new(SettingsKeys.SpotifyAccessToken, ProtectedSettingValueCodec.Protect(settings.SpotifyAccessToken)),
            new(SettingsKeys.SpotifyRefreshToken, ProtectedSettingValueCodec.Protect(settings.SpotifyRefreshToken)),
            new(SettingsKeys.SpotifyTokenExpiresAt, SettingsValueParser.FormatNullableDateTimeOffset(settings.SpotifyTokenExpiresAt)),
            new(SettingsKeys.SpotifyConnectedUserDisplayName, settings.SpotifyConnectedUserDisplayName),
            new(SettingsKeys.HomeHotkey, settings.HomeHotkey),
            new(SettingsKeys.DirectCategoryHotkeys, settings.DirectCategoryHotkeys),
            new(SettingsKeys.LastWindowLeft, SettingsValueParser.FormatNullableDouble(settings.LastWindowLeft)),
            new(SettingsKeys.LastWindowTop, SettingsValueParser.FormatNullableDouble(settings.LastWindowTop)),
            new(SettingsKeys.LastWindowScreenDeviceName, settings.LastWindowScreenDeviceName ?? string.Empty),
            new(SettingsKeys.PreferredBluetoothDeviceName, settings.PreferredBluetoothDeviceName ?? string.Empty)
        ];

        lock (_settingsLock)
        {
            EnsureDefaultsLocked();
            _entryStore.UpsertMany(values.Concat(_slotSettingsRepository.ToSettingEntries(settings)));
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

        _entryStore.EnsureDefaults(SettingsKeys.Defaults.Concat(_slotSettingsRepository.GetDefaultEntries()));
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
            SpotifyAccessToken = ProtectedSettingValueCodec.Unprotect(SettingsValueParser.ReadString(
                entries,
                SettingsKeys.SpotifyAccessToken,
                string.Empty)),
            SpotifyRefreshToken = ProtectedSettingValueCodec.Unprotect(SettingsValueParser.ReadString(
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
            PreferredBluetoothDeviceName = SettingsValueParser.ReadNullableString(
                entries,
                SettingsKeys.PreferredBluetoothDeviceName),
            EnabledCategorySlotKeys = _slotSettingsRepository.ReadCategorySlotStates(entries),
            EnabledSnippetSlotKeys = _slotSettingsRepository.ReadSnippetSlotStates(entries)
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
            LastWindowScreenDeviceName = settings.LastWindowScreenDeviceName,
            PreferredBluetoothDeviceName = settings.PreferredBluetoothDeviceName
        };
    }
}
