namespace ReadTheStupidText.Application.Startup;

/// <summary>
/// Controls whether the app launches automatically at Windows logon, backed by a
/// packaged <c>windows.startupTask</c>. The OS keeps the user in control: a task
/// the user disabled in Task Manager/Settings can't be re-enabled in code.
/// </summary>
public interface IStartupService
{
    /// <summary>Whether the startup task is currently enabled.</summary>
    Task<bool> IsEnabledAsync();

    /// <summary>
    /// Requests enabling or disabling launch-at-startup. Returns the resulting
    /// enabled state, which may stay <c>false</c> when enabling is refused by the
    /// user (disabled in Task Manager) or by policy.
    /// </summary>
    Task<bool> SetEnabledAsync(bool enabled);
}
