#if DEBUG
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 451 (Slice 300's emulator check): <c>user-report-refuse status=&lt;400-599&gt;|off</c> starts (or stops) a listener on the phone's own loopback (port <see cref="UserReportRefusePlan.Port"/>) that
/// answers every request with that status and no body, so "Report a problem" gets a refusal; a flow points the report address at it with <c>crash-stub url=http://127.0.0.1:8788</c> (the stub
/// reuses the app's own Debug address, so nothing reaches the Worker). <c>usage-switch state=on|off</c> sets the usage switch as the settings toggle does. Outcomes: <c>HOOK user-report-refuse ok &lt;status&gt;</c>,
/// <c>ok off</c> or <c>failed &lt;reason&gt;</c>; each request it answers writes <c>HOOK user-report-refuse answered &lt;status&gt;</c>; <c>HOOK usage-switch ok on|off</c>.
/// </summary>
internal static partial class DinoTestCommands
{
	private const int ListenBacklog = 4;
	private const int ReadBufferBytes = 4096;
	private const int HeaderLimitBytes = 16 * 1024;
	private static readonly object RefuseGate = new();
	private static TcpListener? refuseListener;
	private static int refuseStatus;

	// Called once from MauiProgram.cs (inside #if DEBUG) right before CrashTestHook.Install(), after TestHooks.Install() has run Register(), so it chains onto the app's commands on the main thread
	// with no timing guess; DinoTestCommands.cs stays untouched.
	internal static void ChainUserReportHooks()
	{
		Func<TestHookCommand, string?>? previous = TestHookDispatcher.AppCommands;
		TestHookDispatcher.AppCommands = command => UserReportCommand(command) ?? previous?.Invoke(command);
		TestHookDispatcher.AppLaunchKeys = TestHookDispatcher.AppLaunchKeys.Concat(UserReportRefusePlan.LaunchKeys).Distinct().ToList();
	}

	private static string? UserReportCommand(TestHookCommand command) => command.Name switch
	{
		UserReportRefusePlan.RefuseName => RefuseUserReports(command.Get(UserReportRefusePlan.StatusKey, string.Empty)),
		UserReportRefusePlan.UsageSwitchName => SetUsageSwitch(command.Get(UserReportRefusePlan.StateKey, string.Empty)),
		_ => null,
	};

	private static string SetUsageSwitch(string state)
	{
		if (UserReportRefusePlan.ParseSwitch(state) is not { } on)
		{
			return $"{TestHookDispatcher.Failed} bad state '{state}' (use on or off)";
		}

		IPlatformApplication.Current!.Services.GetRequiredService<UsageCounter>().Enabled = on; // the setter the settings toggle uses
		return $"{TestHookDispatcher.Ok} {state.ToLowerInvariant()}";
	}

	private static string RefuseUserReports(string statusText)
	{
		if (!UserReportRefusePlan.TryParseStatus(statusText, out int? status, out string error))
		{
			return $"{TestHookDispatcher.Failed} {error}";
		}

		try
		{
			lock (RefuseGate)
			{
				return status is { } code ? StartRefusing(code) : StopRefusing();
			}
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the fault is returned to the hook log
			return $"{TestHookDispatcher.Failed} {TestHookLog.FaultSummary(ex)}";
		}
	}

	private static string StartRefusing(int status)
	{
		Volatile.Write(ref refuseStatus, status);
		if (refuseListener is null)
		{
			var listener = new TcpListener(IPAddress.Loopback, UserReportRefusePlan.Port);
			listener.Start(ListenBacklog);
			refuseListener = listener;
			_ = Task.Run(() => AnswerLoopAsync(listener));
		}

		return $"{TestHookDispatcher.Ok} {status}";
	}

	private static string StopRefusing()
	{
		refuseListener?.Stop();
		refuseListener = null;
		return $"{TestHookDispatcher.Ok} {UserReportRefusePlan.OffName}";
	}

	private static async Task AnswerLoopAsync(TcpListener listener)
	{
		while (true)
		{
			TcpClient client;
			try
			{
				client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is ObjectDisposedException or SocketException or InvalidOperationException)
			{
				// no-fallback: Debug-only test hook; stopping the listener ends the loop
				ClearDeadListener(listener);
				return;
			}

			_ = Task.Run(() => AnswerOneAsync(client));
		}
	}

	// A loop that ended without StopRefusing leaves a dead listener: forget it, so the next hook starts a new one instead of reporting ok.
	private static void ClearDeadListener(TcpListener listener)
	{
		lock (RefuseGate)
		{
			if (ReferenceEquals(refuseListener, listener))
			{
				refuseListener = null;
				Log($"{UserReportRefusePlan.RefuseName} listener ended");
			}
		}
	}

	private static async Task AnswerOneAsync(TcpClient client)
	{
		try
		{
			using (client)
			{
				NetworkStream stream = client.GetStream();
				await ReadWholeRequestAsync(stream).ConfigureAwait(false);
				int status = Volatile.Read(ref refuseStatus);
				await stream.WriteAsync(UserReportRefusePlan.ResponseFor(status)).ConfigureAwait(false);
				Log($"{UserReportRefusePlan.RefuseName} answered {status}");
			}
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the fault is written to the hook log
			Log($"{UserReportRefusePlan.RefuseName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	// Reads the header block and the announced body, so the answer is not sent (and the socket closed) over an unread request.
	private static async Task ReadWholeRequestAsync(NetworkStream stream)
	{
		var received = new List<byte>();
		var buffer = new byte[ReadBufferBytes];
		int headerEnd = -1;
		int bodyLength = 0;
		while (received.Count < HeaderLimitBytes + bodyLength + ReadBufferBytes && (headerEnd < 0 || received.Count < headerEnd + bodyLength))
		{
			int read = await stream.ReadAsync(buffer).ConfigureAwait(false);
			if (read == 0)
			{
				return;
			}

			received.AddRange(buffer.AsSpan(0, read).ToArray());
			if (headerEnd < 0)
			{
				string text = Encoding.ASCII.GetString(received.ToArray());
				int marker = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
				if (marker >= 0)
				{
					headerEnd = marker + 4;
					bodyLength = UserReportRefusePlan.ContentLengthOf(text[..marker]);
				}
			}
		}
	}
}
#endif
