using Binders.Application.Reading;
using Windows.ApplicationModel.DataTransfer;

namespace Binders.Infrastructure.Reading;

/// <summary>
/// Reads text from the Windows clipboard via the WinRT clipboard API. Must be
/// called from the UI thread (the hotkey handler already is).
/// </summary>
public sealed class ClipboardReader : IClipboardReader
{
    public async Task<string?> GetTextAsync()
    {
        try
        {
            DataPackageView content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text))
            {
                return null;
            }

            return await content.GetTextAsync();
        }
        catch (Exception)
        {
            // The clipboard can be momentarily locked by another process; treat
            // an unreadable clipboard as "nothing to read" rather than crashing.
            return null;
        }
    }
}
