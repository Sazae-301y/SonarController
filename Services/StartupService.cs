using SonarController.Helpers;
using Windows.ApplicationModel;

namespace SonarController.Services;

public enum StartupChangeResult
{
    Success,
    DisabledByUser,
    DisabledByPolicy,
    Failed,
}

/// <summary>Windows起動時の自動起動。MSIXはStartupTask、EXE版はレジストリRunキーで管理する。</summary>
public interface IStartupService
{
    Task<bool> IsEnabledAsync();

    Task<StartupChangeResult> SetEnabledAsync(bool enabled);
}

public sealed class StartupService : IStartupService
{
    // Package.appxmanifest の windows.startupTask の TaskId と一致させる
    public const string StartupTaskId = "SonarControllerStartup";

    public async Task<bool> IsEnabledAsync()
    {
        if (!PackageIdentity.IsPackaged)
            return AutoStartHelper.IsEnabled();

        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Failed to read the startup task state.", ex);
            return false;
        }
    }

    public async Task<StartupChangeResult> SetEnabledAsync(bool enabled)
    {
        try
        {
            if (!PackageIdentity.IsPackaged)
            {
                AutoStartHelper.SetEnabled(enabled);
                return StartupChangeResult.Success;
            }

            var task = await StartupTask.GetAsync(StartupTaskId);
            if (!enabled)
            {
                task.Disable();
                return StartupChangeResult.Success;
            }

            var state = await task.RequestEnableAsync();
            return state switch
            {
                StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => StartupChangeResult.Success,
                StartupTaskState.DisabledByPolicy => StartupChangeResult.DisabledByPolicy,
                StartupTaskState.DisabledByUser => StartupChangeResult.DisabledByUser,
                _ => StartupChangeResult.Failed,
            };
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to change the startup setting.", ex);
            return StartupChangeResult.Failed;
        }
    }
}
