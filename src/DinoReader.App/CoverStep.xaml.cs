using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

/// <summary>
/// Step 2 of the wizard (Slice 78): pick a page, a gallery photo or a colour as the cover (Slice 77's <see cref="CoverPicker"/>).
/// The picker is built again every time the step is shown, because the pages may have been reordered, rotated or cropped since.
/// </summary>
public partial class CoverStep : ContentView
{
	private WizardSession? _session;

	public CoverStep() => InitializeComponent();

	/// <summary>Connects the step to the wizard's session. Called once.</summary>
	public void Attach(WizardSession session) => _session = session;

	public void Show()
	{
		if (_session is null)
		{
			return;
		}

		CoverPickerHost.Content = new CoverPicker(
			_session.Draft, _session.Library, _session.Services.GetRequiredService<ExternalActivityGuard>(), RenderCover);
		RenderCover();
	}

	private void OnFirstPageTapped(object? sender, TappedEventArgs e)
	{
		StoryDraft draft = _session!.Draft;
		if (!draft.HasPageImages)
		{
			return;
		}

		PickedPhotoCache.Delete(draft.PendingCoverPhoto);
		draft.ChooseCover(CoverStyle.FirstPage);
		(CoverPickerHost.Content as CoverPicker)?.Refresh();
		RenderCover();
	}

	private void RenderCover()
	{
		StoryDraft draft = _session!.Draft;
		bool selected = draft.CoverStyle == CoverStyle.FirstPage;
		FirstPageChip.BackgroundColor = selected ? DinoTokens.Get(DinoTokens.BrandTint) : Colors.White;
		FirstPageLabel.TextColor = selected ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.Body);
		FirstPageChip.Opacity = draft.HasPageImages ? 1 : 0.45;
		StoryTile tile = StoryTile.From(draft.Current with { Title = string.IsNullOrWhiteSpace(draft.Title) ? TextStep.SuggestTitle(draft) : draft.Title }, _session.Library);
		CoverPreview.BindingContext = draft.HasPendingCover ? tile with { CoverImagePath = draft.PendingCoverPhoto } : tile;
	}
}
