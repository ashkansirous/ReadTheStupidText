using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

// Early start (Slice 139, Decision 147): the review page decides what to voice before the tap, this page knows who would read it.
public partial class ReaderPage
{
    // Voices a head into the sentence cache in the voice and language the read will use, for the review page. Nothing is
    // prepared while a read runs. Cancelling stops the reader's shared preparation only while it is this call's: never this
    // page's own head start, which may have taken over the reader's single preparation meanwhile.
    // True only when something was prepared and nothing stopped it (a Play meanwhile sets ReadInProgress).
    private async Task<bool> VoiceEarlyAsync(string text, string language, CancellationToken token)
    {
        if (_retired || _headStart.ReadInProgress || BlankText.IsBlank(text))
        {
            return false;
        }

        using CancellationTokenRegistration stop = token.Register(CancelEarlyPreparation);
        bool prepared = await PrepareInLanguageAsync(text, language, token);
        return prepared && !_retired && !_headStart.ReadInProgress;
    }

    private void CancelEarlyPreparation()
    {
        if (_ownPreparations == 0)
        {
            _headStart.CancelPrepare();
        }
    }
}
