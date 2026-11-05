using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using SonarController.Helpers;

namespace SonarController.Services;

/// <summary>Store版の購入後に、インストーラー版(Trial)が残っていれば削除を案内するための検出・実行</summary>
public static class TrialUninstaller
{
    // Setup.iss の Trial 版 AppId（{{ は Inno Setup のエスケープ）に _is1 を付けたキー名
    public const string TrialUninstallKeyName = "{B1C1B6C1-6F5D-4E9A-9C3B-2A6E7F9D1A21}_is1";

    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";

    private static readonly string DismissedMarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonarController", "trial-uninstall-dismissed");

    public static string? FindUninstallCommand() => FindUninstallCommand(ReadFromRegistry);

    /// <param name="readUninstallString">(ハイブ, ビュー) から UninstallString を返す。未登録なら null</param>
    public static string? FindUninstallCommand(Func<RegistryHive, RegistryView, string?> readUninstallString)
    {
        (RegistryHive Hive, RegistryView View)[] locations =
        [
            (RegistryHive.CurrentUser, RegistryView.Default),
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
        ];

        foreach (var (hive, view) in locations)
        {
            var command = readUninstallString(hive, view);
            if (!string.IsNullOrWhiteSpace(command))
                return command;
        }
        return null;
    }

    public static bool IsDismissed() => File.Exists(DismissedMarkerPath);

    public static void Dismiss()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DismissedMarkerPath)!);
            File.WriteAllText(DismissedMarkerPath, string.Empty);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Failed to save trial uninstall prompt preference.", ex);
        }
    }

    public static bool TryStartUninstall(string uninstallCommand)
    {
        try
        {
            // UninstallString は引用符付きの実行ファイルパスのみ（Inno Setup の標準形式）
            var path = uninstallCommand.Trim().Trim('"');
            if (!File.Exists(path))
                return false;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Failed to start trial uninstaller.", ex);
            return false;
        }
    }

    private static string? ReadFromRegistry(RegistryHive hive, RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(UninstallRoot + TrialUninstallKeyName);
            return key?.GetValue("UninstallString") as string;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Failed to read trial uninstall entry.", ex);
            return null;
        }
    }
}
