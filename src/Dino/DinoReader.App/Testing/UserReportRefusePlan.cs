#if DEBUG || DINO_TEST_BOOKS // Debug app builds and any project that defines DINO_TEST_BOOKS (DinoReader.Tests does not link this file); never in a Release app
using System.Globalization;
using System.Text;

namespace DinoReader.Testing;

/// <summary>
/// Pure part of the Debug hooks of Slice 451: <c>user-report-refuse status=&lt;400-599&gt;|off</c> makes the "Report a problem" POST fail with that status (a loopback listener in the
/// app's own process answers every request with it; the flow points the report address at it with <c>crash-stub url=http://127.0.0.1:8788</c>), and
/// <c>usage-switch state=on|off</c> sets the person's anonymous-usage switch as the settings toggle does. Slice 300's check needs both: the switch off and a refused report.
/// </summary>
internal static class UserReportRefusePlan
{
	internal const string RefuseName = "user-report-refuse";
	internal const string UsageSwitchName = "usage-switch";
	internal const string StatusKey = "status";
	internal const string StateKey = "state";
	internal const string OffName = "off";
	internal const string OnName = "on";
	internal const int Port = 8788;
	internal const int FirstRefusal = 400;
	internal const int LastRefusal = 599;

	/// <summary>The launch-argument names the hooks read; the app adds them to <c>TestHookDispatcher.AppLaunchKeys</c> so the iOS entry point passes them on.</summary>
	internal static readonly string[] LaunchKeys = [StatusKey, StateKey];

	/// <summary>Reads <c>status</c>: <c>off</c> gives null status and no error (stop answering); a whole number from 400 to 599 gives that status; anything else gives the reason in <paramref name="error"/>.</summary>
	internal static bool TryParseStatus(string text, out int? status, out string error)
	{
		status = null;
		error = string.Empty;
		if (string.Equals(text, OffName, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number is < FirstRefusal or > LastRefusal)
		{
			error = $"bad status '{text}' (use {FirstRefusal}-{LastRefusal} or {OffName})";
			return false;
		}

		status = number;
		return true;
	}

	/// <summary>Reads <c>state</c> of the usage switch: <c>on</c> or <c>off</c>; null for anything else.</summary>
	internal static bool? ParseSwitch(string text) => text.ToLowerInvariant() switch
	{
		OnName => true,
		OffName => false,
		_ => null,
	};

	/// <summary>The whole HTTP answer for <paramref name="status"/>: the status line, no body, the connection closed.</summary>
	internal static byte[] ResponseFor(int status) =>
		Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Refused by the Slice 451 test hook\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");

	/// <summary>The body length a request announces in its header block (0 when it names none), so the listener can read the whole request before it answers.</summary>
	internal static int ContentLengthOf(string headerBlock)
	{
		foreach (string line in headerBlock.Split("\r\n"))
		{
			const string Prefix = "content-length:";
			if (line.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
				&& int.TryParse(line[Prefix.Length..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int length))
			{
				return length;
			}
		}

		return 0;
	}
}
#endif
