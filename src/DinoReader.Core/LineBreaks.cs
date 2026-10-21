namespace DinoReader.Core;

/// <summary>Line-break handling for text read back from a platform text box.</summary>
public static class LineBreaks
{
    /// <summary>A Windows text box returns a lone CR per line break; Core only knows LF. CRLF and CR become LF.</summary>
    public static string ToLf(string? text) => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
}
