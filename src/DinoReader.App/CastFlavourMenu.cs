using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// The flavour menu (Slice 113, mock frame 48): a floating white list, 190 wide, over a dimmed cast screen. The current flavour
/// is highlighted (tint and a 2 px ring); each item has its name and, under it, its hint. Like the merge sheet it lives inside the
/// cast page, so leaving the app cannot strand it.
/// </summary>
internal sealed class CastFlavourMenu : Grid
{
	private const double MenuWidth = 190;
	private const double ItemHeight = 44;
	private const double ItemRadius = 10;
	private const double NameFont = 14;
	private const double HintFont = 11.5;
	private const double RingWidth = 2;
	private TaskCompletionSource<VoiceFlavour?>? _pending;

	public CastFlavourMenu()
	{
		IsVisible = false;
		BackgroundColor = DinoTokens.Get(DinoTokens.SheetScrim);
		CastViews.AddTap(this, () => { Close(null); return Task.CompletedTask; });
	}

	/// <summary>Shows the menu and returns the flavour picked, or null when it was dismissed.</summary>
	public Task<VoiceFlavour?> PickAsync(VoiceFlavour current)
	{
		Close(null);
		Children.Clear();
		Children.Add(MenuFor(current));
		_pending = new TaskCompletionSource<VoiceFlavour?>();
		IsVisible = true;
		return _pending.Task;
	}

	private void Close(VoiceFlavour? picked)
	{
		IsVisible = false;
		_pending?.TrySetResult(picked);
		_pending = null;
	}

	private Border MenuFor(VoiceFlavour current)
	{
		var list = new VerticalStackLayout { Spacing = 2 };
		foreach (VoiceFlavour flavour in VoiceFlavours.Pickable)
		{
			list.Add(Item(flavour, flavour == current));
		}

		var menu = new Border
		{
			WidthRequest = MenuWidth,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = new Thickness(6),
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.25f, Radius = 30, Offset = new Point(0, 10) },
			Content = list,
		};
		CastViews.AddTap(menu, () => Task.CompletedTask);
		return menu;
	}

	private Border Item(VoiceFlavour flavour, bool selected)
	{
		var text = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
		text.Add(new Label
		{
			Text = CastText.FlavourName(flavour), FontSize = NameFont,
			TextColor = DinoTokens.Get(selected ? DinoTokens.Brand : DinoTokens.Ink),
		});
		if (CastText.FlavourHint(flavour).Length > 0)
		{
			text.Add(new Label { Text = CastText.FlavourHint(flavour), FontSize = HintFont, TextColor = DinoTokens.Get(DinoTokens.Muted) });
		}

		var item = new Border
		{
			MinimumHeightRequest = ItemHeight,
			Padding = new Thickness(10, 4),
			BackgroundColor = selected ? DinoTokens.Get(DinoTokens.BrandTint) : Colors.Transparent,
			Stroke = selected ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent,
			StrokeThickness = selected ? RingWidth : 0,
			StrokeShape = new RoundRectangle { CornerRadius = ItemRadius },
			Content = text,
		};
		SemanticProperties.SetDescription(item, CastText.FlavourChoice(flavour));
		CastViews.AddTap(item, () => { Close(flavour); return Task.CompletedTask; });
		return item;
	}
}
