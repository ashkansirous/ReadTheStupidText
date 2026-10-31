#if DEBUG
namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>
/// One Debug-only test command (Slice 162): a name plus string arguments. Android delivers it as an
/// <c>adb shell am broadcast</c> (extras <c>cmd</c> and the rest), iOS as a URL
/// (<c>rtst-test://skip-first-run?lang=en</c>); both parse into this, so the dispatcher sees one shape.
/// Pure: no platform types.
/// </summary>
public sealed record TestHookCommand(string Name, IReadOnlyDictionary<string, string> Args)
{
	/// <summary>The extra that carries the command name in an Android broadcast.</summary>
	public const string NameKey = "cmd";

	/// <summary>Builds a command from a name and its arguments; null when the name is missing or blank.</summary>
	public static TestHookCommand? Parse(string? name, IEnumerable<KeyValuePair<string, string>>? args)
	{
		string trimmed = (name ?? string.Empty).Trim().ToLowerInvariant();
		if (trimmed.Length == 0)
		{
			return null;
		}

		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, string> pair in args ?? [])
		{
			if (!string.Equals(pair.Key, NameKey, StringComparison.OrdinalIgnoreCase))
			{
				map[pair.Key] = pair.Value;
			}
		}

		return new TestHookCommand(trimmed, map);
	}

	/// <summary>Reads <c>scheme://name?key=value&amp;key=value</c>; null when it is not a test URL for <paramref name="scheme"/>.</summary>
	public static TestHookCommand? FromUrl(string? url, string scheme)
	{
		string prefix = scheme + "://";
		if (url is null || !url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		string rest = url[prefix.Length..];
		int query = rest.IndexOf('?');
		string name = query < 0 ? rest : rest[..query];
		return Parse(name.Trim('/'), query < 0 ? [] : ParseQuery(rest[(query + 1)..]));
	}

	/// <summary>The argument's value, or <paramref name="fallback"/> when it was not given.</summary>
	public string Get(string key, string fallback) => Args.TryGetValue(key, out string? value) ? value : fallback;

	/// <summary>Slice 209: whether a switch argument is on (<c>true</c> or <c>1</c>, any case); not given or anything else is off.</summary>
	public bool Flag(string key) =>
		Get(key, string.Empty) is var value && (value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

	private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
	{
		foreach (string part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
		{
			int eq = part.IndexOf('=');
			string key = Uri.UnescapeDataString(eq < 0 ? part : part[..eq]);
			string value = eq < 0 ? string.Empty : Uri.UnescapeDataString(part[(eq + 1)..].Replace('+', ' '));
			yield return new KeyValuePair<string, string>(key, value);
		}
	}
}
#endif
