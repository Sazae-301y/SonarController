using Microsoft.Win32;

namespace SonarController.Helpers;

/// <summary>
/// Windowsログイン時の自動起動をレジストリ Run キーで管理する
/// </summary>
public static class AutoStartHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // 版ごとに値名を分け、同時にインストールしても自動起動設定が衝突しないようにする
#if LICENSE_STORE || LICENSE_STORE_DEV
    private const string ValueName = "SonarController.Store";
#elif LICENSE_TRIAL_TEST
    private const string ValueName = "SonarController.TrialTest";
#elif LICENSE_TRIAL
    private const string ValueName = "SonarController.Trial";
#else
    private const string ValueName = "SonarController";
#endif

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("実行ファイルパスを取得できません。");
            key.SetValue(ValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
