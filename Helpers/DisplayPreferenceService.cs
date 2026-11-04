using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace SonarController.Helpers;

/// <param name="MonitorId">モニター固有ID。null は「カーソルのあるディスプレイ」</param>
public sealed record DisplayOption(string? MonitorId, string DisplayName, bool IsConnected = true);

public sealed class DisplayPreferenceService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonarController",
        "display-settings.json");

    private sealed record MonitorInfo(Screen Screen, string Id, string Name);

    private string? _preferredId;
    private string? _preferredName;
    private readonly LanguageService _language;
    // 旧バージョンが保存した \\.\DISPLAYn 形式。再起動で番号が変わり得るため、接続確認できた時点でIDへ移行する
    private string? _legacyDeviceName;

    public string? PreferredId => _preferredId;
    public string? LoadError { get; }

    public DisplayPreferenceService(LanguageService language)
    {
        _language = language;
        if (!File.Exists(SettingsPath)) return;

        try
        {
            var stored = JsonSerializer.Deserialize<StoredDisplayPreference>(File.ReadAllText(SettingsPath));
            _preferredId = stored?.MonitorId;
            _preferredName = stored?.DisplayName;
            _legacyDeviceName = _preferredId is null ? stored?.DeviceName : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadError = string.Format(_language.GetString("DisplayLoadError"), exception.Message);
        }
    }

    public IReadOnlyList<DisplayOption> GetDisplayOptions()
    {
        var monitors = GetMonitors();
        MigrateLegacy(monitors);

        var options = new List<DisplayOption> { new(null, _language.GetString("DisplayCursor")) };
        options.AddRange(monitors.Select(monitor => new DisplayOption(monitor.Id, monitor.Name)));

        // 保存済みのディスプレイが未接続でも設定は保持し、選択肢として残す
        if (_preferredId is not null && monitors.All(monitor => monitor.Id != _preferredId))
            options.Add(new DisplayOption(_preferredId,
                $"{_preferredName ?? _language.GetString("DisplaySaved")} ({_language.GetString("DisplayDisconnected")})", false));

        return options;
    }

    public Screen GetDisplayForPopup(System.Drawing.Point cursorPosition)
    {
        var monitors = GetMonitors();
        MigrateLegacy(monitors);

        var preferred = _preferredId is null
            ? null
            : monitors.FirstOrDefault(monitor => monitor.Id == _preferredId);

        return preferred?.Screen ?? Screen.FromPoint(cursorPosition);
    }

    public bool TrySetPreferredDisplay(string? monitorId, out string? error)
    {
        string? name = null;
        if (monitorId is not null)
        {
            var monitor = GetMonitors().FirstOrDefault(m => m.Id == monitorId);
            if (monitor is null)
            {
                error = _language.GetString("DisplayNotConnectedError");
                return false;
            }

            name = monitor.Name;
        }

        if (!TrySave(monitorId, name, out error)) return false;

        _preferredId = monitorId;
        _preferredName = name;
        _legacyDeviceName = null;
        return true;
    }

    private void MigrateLegacy(IReadOnlyList<MonitorInfo> monitors)
    {
        if (_legacyDeviceName is null) return;

        var monitor = monitors.FirstOrDefault(m =>
            string.Equals(m.Screen.DeviceName, _legacyDeviceName, StringComparison.OrdinalIgnoreCase));
        if (monitor is null) return;

        _preferredId = monitor.Id;
        _preferredName = monitor.Name;
        _legacyDeviceName = null;
        TrySave(_preferredId, _preferredName, out _);
    }

    private bool TrySave(string? monitorId, string? displayName, out string? error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(new StoredDisplayPreference
            {
                MonitorId = monitorId,
                DisplayName = displayName
            });
            File.WriteAllText(SettingsPath, json);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = string.Format(_language.GetString("DisplaySaveError"), exception.Message);
            return false;
        }
    }

    private IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var details = DisplayConfigNative.GetMonitorDetails();

        var screens = Screen.AllScreens
            .OrderBy(screen => screen.Bounds.X)
            .ThenBy(screen => screen.Bounds.Y)
            .ToArray();

        var monitors = screens.Select(screen =>
        {
            details.TryGetValue(screen.DeviceName, out var detail);
            var id = string.IsNullOrEmpty(detail.DevicePath) ? screen.DeviceName : detail.DevicePath;
            var model = string.IsNullOrWhiteSpace(detail.FriendlyName)
                ? _language.GetString("DisplayDevice")
                : detail.FriendlyName;
            return (Screen: screen, Id: id, Model: model);
        }).ToArray();

        return monitors.Select(monitor =>
        {
            var sameModel = monitors.Where(other => other.Model == monitor.Model).ToArray();
            var name = sameModel.Length > 1
                ? $"{monitor.Model} #{Array.IndexOf(sameModel, monitor) + 1}"
                : monitor.Model;
            var bounds = monitor.Screen.Bounds;
            name += $" ({bounds.Width}×{bounds.Height})";
            if (monitor.Screen.Primary) name += $" ({_language.GetString("DisplayMain")})";
            return new MonitorInfo(monitor.Screen, monitor.Id, name);
        }).ToArray();
    }

    private sealed class StoredDisplayPreference
    {
        public string? MonitorId { get; set; }
        public string? DisplayName { get; set; }
        public string? DeviceName { get; set; }
    }

    // 再起動で変わる \\.\DISPLAYn ではなく、EDID由来のモニター固有パスと実機名を取得する
    private static class DisplayConfigNative
    {
        private const uint QdcOnlyActivePaths = 2;
        private const uint GetSourceName = 1;
        private const uint GetTargetName = 2;

        public static Dictionary<string, (string FriendlyName, string DevicePath)> GetMonitorDetails()
        {
            var result = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0)
                    return result;

                var paths = new PathInfo[pathCount];
                var modes = new ModeInfo[modeCount];
                if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                    return result;

                for (var i = 0; i < pathCount; i++)
                {
                    var source = new SourceDeviceName
                    {
                        Header = CreateHeader<SourceDeviceName>(GetSourceName, paths[i].Source.AdapterId, paths[i].Source.Id)
                    };
                    var target = new TargetDeviceName
                    {
                        Header = CreateHeader<TargetDeviceName>(GetTargetName, paths[i].Target.AdapterId, paths[i].Target.Id)
                    };

                    if (GetSourceInfo(ref source) != 0 || GetTargetInfo(ref target) != 0) continue;

                    result[source.ViewGdiDeviceName] = (target.MonitorFriendlyDeviceName, target.MonitorDevicePath);
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException
                                                  or MarshalDirectiveException)
            {
            }

            return result;
        }

        private static DeviceInfoHeader CreateHeader<T>(uint type, Luid adapterId, uint id) => new()
        {
            Type = type,
            Size = (uint)Marshal.SizeOf<T>(),
            AdapterId = adapterId,
            Id = id
        };

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint pathCount,
            [Out] PathInfo[] paths,
            ref uint modeCount,
            [Out] ModeInfo[] modes,
            IntPtr currentTopologyId);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        private static extern int GetSourceInfo(ref SourceDeviceName request);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        private static extern int GetTargetInfo(ref TargetDeviceName request);

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PathSourceInfo
        {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIdx;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PathTargetInfo
        {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIdx;
            public uint OutputTechnology;
            public uint Rotation;
            public uint Scaling;
            public uint RefreshRateNumerator;
            public uint RefreshRateDenominator;
            public uint ScanLineOrdering;
            public int TargetAvailable;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PathInfo
        {
            public PathSourceInfo Source;
            public PathTargetInfo Target;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct ModeInfo
        {
            public uint InfoType;
            public uint Id;
            public Luid AdapterId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceInfoHeader
        {
            public uint Type;
            public uint Size;
            public Luid AdapterId;
            public uint Id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SourceDeviceName
        {
            public DeviceInfoHeader Header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ViewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TargetDeviceName
        {
            public DeviceInfoHeader Header;
            public uint Flags;
            public uint OutputTechnology;
            public ushort EdidManufactureId;
            public ushort EdidProductCodeId;
            public uint ConnectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string MonitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string MonitorDevicePath;
        }
    }
}
