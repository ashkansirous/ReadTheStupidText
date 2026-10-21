namespace ReadTheStupidText.Application.Translation;

/// <summary>Why a read was, or was not, translated (Slice 57). Every value but <see cref="Translated"/> reads the original.</summary>
public enum TranslationOutcome
{
    /// <summary>The toggle is off, or no target language is chosen yet.</summary>
    NotRequested,

    /// <summary>The text was translated; the translation is what is spoken.</summary>
    Translated,

    /// <summary>The text is already in the target language.</summary>
    SameLanguage,

    /// <summary>The language could not be detected with enough confidence.</summary>
    SourceUnknown,

    /// <summary>The engine has no model for this language pair.</summary>
    PairUnsupported,

    /// <summary>The translation pack is not installed (or could not be fetched).</summary>
    ModelMissing,

    /// <summary>The translation did not finish within its time budget.</summary>
    TimedOut,

    /// <summary>The engine failed or returned nothing.</summary>
    Failed,
}

/// <summary>What <see cref="ReadTranslator"/> hands the reader: the text to speak and why.</summary>
public sealed record ReadTranslationResult(string Text, TranslationOutcome Outcome, string? Source, string? Target)
{
    /// <summary>True when an attempt was made and the user may want to know how it went.</summary>
    public bool IsNotable => Outcome is not (TranslationOutcome.NotRequested or TranslationOutcome.SameLanguage);
}
