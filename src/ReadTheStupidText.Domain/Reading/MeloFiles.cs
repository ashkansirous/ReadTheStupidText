namespace ReadTheStupidText.Domain.Reading;

/// <summary>The file layout of a MeloTTS zh_en voice pack (the upstream model folder, unchanged).</summary>
public static class MeloFiles
{
    public const string Model = "model.onnx";
    public const string Lexicon = "lexicon.txt";
    public const string Tokens = "tokens.txt";
    public const string DictDir = "dict";
    public const string DateRules = "date.fst";
    public const string NumberRules = "number.fst";
    public const string PhoneRules = "phone.fst";
    // The upstream example passes them in this order.
    public static readonly string[] RuleFsts = [PhoneRules, DateRules, NumberRules];
}
