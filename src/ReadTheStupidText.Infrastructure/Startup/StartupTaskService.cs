using ReadTheStupidText.Application.Startup;
using Windows.ApplicationModel;

namespace ReadTheStupidText.Infrastructure.Startup;

/// <summary>
/// Implements launch-at-startup with the packaged <see cref="StartupTask"/> API.
/// For packaged desktop apps enabling shows no consent dialog, but the user can
/// still block it via Task Manager — hence the resulting state is reported back.
/// </summary>
public sealed class StartupTaskService : IStartupService
{
    // Must match the TaskId of the windows.startupTask extension in
    // Package.appxmanifest.
    private const string TaskId = "ReadTheStupidTextStartup";

    public async Task<bool> IsEnabledAsync()
    {
        StartupTask task = await StartupTask.GetAsync(TaskId);
        return IsEnabled(task.State);
    }

    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        StartupTask task = await StartupTask.GetAsync(TaskId);
        if (!enabled)
        {
            task.Disable();
            return false;
        }

        StartupTaskState state = await task.RequestEnableAsync();
        return IsEnabled(state);
    }

    private static bool IsEnabled(StartupTaskState state) =>
        state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
}
