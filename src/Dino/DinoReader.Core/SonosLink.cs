using System.Text.Json;

namespace DinoReader.Core;

/// <summary>
/// The pure part of linking a Sonos home to Dino (Slice 438): reading the link Sonos opens, the languages the link screen offers, the
/// message the app sends to the Worker and the Sonos address the Worker answers with. No UI and no network, so it is unit-tested.
/// </summary>
public static class SonosLink
{
    public const string Scheme = "dinoreader";
    public const string LinkHost = "sonos-link";
    public const string CodeParameter = "code";
    public const int CodeLength = 32;

    private const string SiteHost = "dinoreader.sirous.uk";
    private const string SonosHost = "sonos.com";
    private const string SonosSchemePrefix = "sonos";
    private const string OperationField = "op";
    private const string ConfirmOperation = "confirm";
    private const string LanguageField = "language";
    private const string ProofField = "proof";
    private const string CallbackField = "callback";

    /// <summary>
    /// The link code of a link the app was opened with (<c>dinoreader://sonos-link?code=...</c>, or the same query on the Dino site's
    /// <c>sonos-link.html</c> page), or null for any other link or a code that is not 32 lower-case hex digits.
    /// </summary>
    public static string? CodeOf(string? link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) || !IsLinkAddress(uri))
        {
            return null;
        }

        foreach (string part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = part.IndexOf('=');
            if (equals > 0 && part[..equals] == CodeParameter)
            {
                string value = Uri.UnescapeDataString(part[(equals + 1)..]);
                return IsCode(value) ? value : null;
            }
        }

        return null;
    }

    private static bool IsLinkAddress(Uri uri) =>
        (uri.Scheme == Scheme && uri.Host == LinkHost)
        || (uri.Scheme == Uri.UriSchemeHttps && uri.Host == SiteHost && uri.AbsolutePath.EndsWith("/sonos-link.html", StringComparison.Ordinal));

    private static bool IsCode(string value) =>
        value.Length == CodeLength && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>The story languages the link screen offers: the app's reading language first (English when it is not one of the fifteen), then the rest in the app's order.</summary>
    public static IReadOnlyList<string> LanguagesFor(string? readingLanguage)
    {
        string first = UiLanguage.Resolve(readingLanguage);
        return [first, .. UiLanguage.Supported.Where(code => code != first)];
    }

    /// <summary>The JSON the app posts to the Worker when the grown-up presses Connect. <paramref name="proof"/> is the subscription proof key (Slice 439), null until then.</summary>
    public static string ConfirmBody(string code, string language, string? proof = null)
    {
        var fields = new Dictionary<string, string>
        {
            [OperationField] = ConfirmOperation,
            [CodeParameter] = code,
            [LanguageField] = language,
        };
        if (!string.IsNullOrEmpty(proof))
        {
            fields[ProofField] = proof;
        }

        return JsonSerializer.Serialize(fields);
    }

    /// <summary>
    /// The Sonos address the Worker's answer names, which the app opens to hand control back to Sonos; null when the answer has none or the address is not
    /// Sonos's own (a <c>sonos...:</c> app scheme, or https on sonos.com), so the app never opens anything else.
    /// </summary>
    public static Uri? CallbackOf(string? answerJson)
    {
        if (string.IsNullOrEmpty(answerJson))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(answerJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(CallbackField, out JsonElement value)
                || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return Uri.TryCreate(value.GetString(), UriKind.Absolute, out Uri? callback) && IsSonosAddress(callback) ? callback : null;
        }
        catch (JsonException)
        {
            // no-fallback: not JSON, so there is no address to open
            return null;
        }
    }

    private static bool IsSonosAddress(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
            ? uri.Host == SonosHost || uri.Host.EndsWith("." + SonosHost, StringComparison.Ordinal)
            : uri.Scheme.StartsWith(SonosSchemePrefix, StringComparison.Ordinal);
}
