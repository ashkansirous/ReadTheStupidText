using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

#if WINDOWS
/// <summary>Windows fallback when no system log is registered yet (early startup): the debugger output.</summary>
internal sealed class DebugSystemLog : ISystemLog
{
	public void Info(string message, int? activityId = null) => Write(message, null);

	public void Debug(string message, int? activityId = null) => Write(message, null);

	public void Warning(string message, int? activityId = null, Exception? error = null) => Write(message, error);

	public void Error(string message, int? activityId = null, Exception? error = null) => Write(message, error);

	private static void Write(string message, Exception? error) => System.Diagnostics.Debug.WriteLine($"{message} {error}");
}

#endif
/// <summary>One place that finds the system log, with the per-platform fallback, so every swallowed failure leaves a trace.</summary>
internal static class DinoLog
{
	/// <summary>Logs an exception (never the user's text). Never throws: a broken log must not hide the failure or skip cleanup.</summary>
	public static void Error(IServiceProvider? services, string message, Exception error)
	{
		try
		{
			Resolve(services).Error(message, null, error);
		}
		catch (Exception logFailure)
		{
			Debug.WriteLine($"Dino Reader could not log '{message}': {logFailure.Message}; original: {error}");
		}
	}

	private static ISystemLog Resolve(IServiceProvider? services)
	{
#if ANDROID
		return services?.GetService<ISystemLog>() ?? new AndroidSystemLog();
#elif IOS
		return services?.GetService<ISystemLog>() ?? new IosSystemLog();
#else
		return services?.GetService<ISystemLog>() ?? new DebugSystemLog();
#endif
	}
}
