using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Locates the Supertonic-3 neural voice model on Android (Slice 50, Decision 61).
/// The model no longer ships in the package: it arrives as the downloaded
/// <c>supertonic-3</c> voice pack, which <see cref="IVoicePackService"/> installs
/// under app-local storage. This service just finds that folder (the pack's manifest
/// check in <see cref="IVoicePackService.GetInstalledPath"/> is the one completeness
/// test) and raises <see cref="ReadyChanged"/> so the speech reader switches from the phone voice to the neural one.
/// </summary>
public sealed class MobileVoiceModelService : IVoiceModelService
{
    private readonly IVoicePackService _packs;

    public MobileVoiceModelService(IVoicePackService packs) => _packs = packs;

    public bool IsReady { get; private set; }

    public VoiceModelPaths? Paths { get; private set; }

    public event EventHandler? ReadyChanged;

    // Safe to call again after a pack finishes downloading; stays not-ready when the
    // pack is absent or incomplete, and raises ReadyChanged only on the first transition.
    public Task InitializeAsync(IProgress<double>? progress = null)
    {
        string? folder = _packs.GetInstalledPath(PackConstants.SupertonicPackId);
        if (!IsReady && folder is not null)
        {
            Paths = new VoiceModelPaths(folder);
            IsReady = true;
            ReadyChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }
}
