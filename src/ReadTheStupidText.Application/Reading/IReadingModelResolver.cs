using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Resolves the installed natural model for the language of one read (Slice 90) and returns it, without
/// changing the app-language model that <see cref="IVoiceModelService"/> reports to everyone else.
/// </summary>
public interface IReadingModelResolver
{
    /// <summary>The installed pack's model for <paramref name="language"/>, or null when none is installed.</summary>
    Task<VoiceModelPaths?> ModelForAsync(string language, CancellationToken cancellationToken = default);
}

/// <summary>
/// Who reads one read and with which model: the phone voice, or the natural <see cref="Model"/>.
/// Handed to the reader with that read only (never held as state); the stored voice choice is never written.
/// </summary>
public sealed record ReadVoiceScope(VoiceModelPaths? Model, bool PhoneVoice, string Language);
