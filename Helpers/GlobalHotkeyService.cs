using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace SonarController.Helpers;

public sealed record HotkeyBinding(ModifierKeys Modifiers, Key Key)
{
    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(Key == Key.Pause ? "Pause/Break" : Key.ToString());
            return string.Join(" + ", parts);
        }
    }
}

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonarController",
        "settings.json");

    private HwndSource? _source;
    private Action? _onHotkey;
    private bool _isRegistered;
    private bool _isSuspended;
    private HotkeyBinding _current = LoadBinding();

    public HotkeyBinding Current => _current;
    public string? InitializationError { get; private set; }

    public bool Initialize(Window window, Action onHotkey, out string? error)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle);
        if (_source is null)
        {
            error = "ホットキーを初期化できませんでした。";
            InitializationError = error;
            return false;
        }

        _onHotkey = onHotkey;
        _source.AddHook(WindowProc);
        _isRegistered = Register(_current, out error);
        InitializationError = error;
        return _isRegistered;
    }

    public bool TrySetBinding(HotkeyBinding binding, out string? error)
    {
        if (_source is null)
        {
            error = "ホットキーが初期化されていません。";
            return false;
        }

        if (binding.Key == Key.None)
        {
            error = "キーを指定してください。";
            return false;
        }

        var previous = _current;
        if (_isRegistered)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _isRegistered = false;
        }

        if (!Register(binding, out error))
        {
            if (!_isSuspended)
                _isRegistered = Register(previous, out _);
            return false;
        }

        try
        {
            SaveBinding(binding);
        }
        catch (Exception exception)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            if (!_isSuspended)
                _isRegistered = Register(previous, out _);
            error = $"設定を保存できませんでした: {exception.Message}";
            return false;
        }

        _current = binding;
        _isRegistered = true;
        _isSuspended = false;
        InitializationError = null;
        error = null;
        return true;
    }

    public void Suspend()
    {
        _isSuspended = true;
        if (!_isRegistered || _source is null) return;

        UnregisterHotKey(_source.Handle, HotkeyId);
        _isRegistered = false;
    }

    public void Resume()
    {
        _isSuspended = false;
        if (_isRegistered || _source is null) return;

        _isRegistered = Register(_current, out var error);
        InitializationError = error;
    }

    private bool Register(HotkeyBinding binding, out string? error)
    {
        var modifiers = (uint)binding.Modifiers | ModNoRepeat;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(binding.Key);
        if (RegisterHotKey(_source!.Handle, HotkeyId, modifiers, virtualKey))
        {
            error = null;
            return true;
        }

        var exception = new Win32Exception(Marshal.GetLastWin32Error());
        error = $"このキーは登録できませんでした。別のキーを選んでください。({exception.Message})";
        return false;
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            _onHotkey?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static HotkeyBinding LoadBinding()
    {
        try
        {
            var json = File.ReadAllText(SettingsPath);
            var stored = JsonSerializer.Deserialize<StoredHotkey>(json);
            if (stored is not null && Enum.TryParse<Key>(stored.Key, out var key) && key != Key.None &&
                Enum.TryParse<ModifierKeys>(stored.Modifiers, out var modifiers))
                return new HotkeyBinding(modifiers, key);
        }
        catch
        {
        }

        return new HotkeyBinding(ModifierKeys.None, Key.Pause);
    }

    private static void SaveBinding(HotkeyBinding binding)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(new StoredHotkey
        {
            Key = binding.Key.ToString(),
            Modifiers = binding.Modifiers.ToString()
        });
        File.WriteAllText(SettingsPath, json);
    }

    public void Dispose()
    {
        if (_source is null) return;
        if (_isRegistered)
            UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WindowProc);
        _source = null;
        _isRegistered = false;
    }

    private sealed class StoredHotkey
    {
        public string Key { get; set; } = "Pause";
        public string Modifiers { get; set; } = "None";
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}