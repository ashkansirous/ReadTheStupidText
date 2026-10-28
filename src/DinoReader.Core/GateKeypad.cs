namespace DinoReader.Core;

/// <summary>The grown-up gate's number pad (Slice 164): what a key press does to the typed answer.</summary>
public static class GateKeypad
{
    public const int MaxAnswerDigits = 2;
    public const string BackspaceKey = "⌫";
    public const string OkKey = "OK";

    public static bool IsSubmit(string key) => key == OkKey;

    /// <summary>The answer after a digit or backspace key; OK leaves it unchanged (the caller submits).</summary>
    public static string Press(string answer, string key)
    {
        if (key == OkKey)
        {
            return answer;
        }

        if (key == BackspaceKey)
        {
            return answer.Length > 0 ? answer[..^1] : answer;
        }

        return answer.Length < MaxAnswerDigits ? answer + key : answer;
    }
}
