using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Lists the narrator voices installed on the machine. The implementation
/// (Infrastructure) enumerates the OS speech engines; the Application and UI
/// layers only ever see <see cref="VoiceInfo"/>.
/// </summary>
public interface IVoiceCatalog
{
    /// <summary>All voices installed in Windows, in OS-reported order.</summary>
    IReadOnlyList<VoiceInfo> InstalledVoices { get; }

    /// <summary>The current system default voice, or null if none is installed.</summary>
    VoiceInfo? DefaultVoice { get; }
}
