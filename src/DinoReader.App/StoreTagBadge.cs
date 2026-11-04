using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace DinoReader;

/// <summary>
/// The badges of one store card (Slice 260): a row of <see cref="StoreTagBadge"/>s, gap 4, that wraps to a second line before the title shrinks. Set <see cref="Tags"/>;
/// an empty list leaves the row empty and hidden, so an older Worker's untagged rows look as before.
/// </summary>
public sealed class StoreTagRow : FlexLayout
{
	public static readonly BindableProperty TagsProperty =
		BindableProperty.Create(nameof(Tags), typeof(IReadOnlyList<StoreTag>), typeof(StoreTagRow), null, propertyChanged: (bindable, _, _) => ((StoreTagRow)bindable).Rebuild());

	private const double BadgeGap = 4;

	public StoreTagRow()
	{
		Wrap = FlexWrap.Wrap;
		Direction = FlexDirection.Row;
		AlignItems = FlexAlignItems.Start;
		IsVisible = false;
	}

	public IReadOnlyList<StoreTag>? Tags
	{
		get => (IReadOnlyList<StoreTag>?)GetValue(TagsProperty);
		set => SetValue(TagsProperty, value);
	}

	private void Rebuild()
	{
		Children.Clear();
		foreach (StoreTag tag in Tags ?? [])
		{
			Children.Add(new StoreTagBadge { Tag = tag, Margin = new Thickness(0, 0, BadgeGap, BadgeGap) });
		}

		IsVisible = Children.Count > 0;
	}
}

/// <summary>
/// The small coloured pill a store card shows for one <see cref="StoreTag"/> (Slice 260, mock Batch 47 frame 162): 22 high, radius 11, 11.5 bold text, no icon.
/// Colours are <see cref="DinoTokens"/> keys. Set <see cref="Tag"/> (for example from a <c>BindableLayout</c> over the row's badges) and it fills itself in.
/// </summary>
public sealed class StoreTagBadge : Border
{
	public static readonly BindableProperty TagProperty =
		BindableProperty.Create(nameof(Tag), typeof(StoreTag), typeof(StoreTagBadge), StoreTag.Hot, propertyChanged: (bindable, _, _) => ((StoreTagBadge)bindable).Refresh());

	private const double PillHeight = 22;
	private const double PillRadius = 11;
	private const double SidePadding = 8;
	private const double TextSize = 11.5;

	private readonly Label _label = new() { FontSize = TextSize, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };

	public StoreTagBadge()
	{
		MinimumHeightRequest = PillHeight; // grows with the system font size instead of clipping the label
		Padding = new Thickness(SidePadding, 0);
		StrokeThickness = 0;
		StrokeShape = new RoundRectangle { CornerRadius = PillRadius };
		Content = _label;
		Refresh();
	}

	public StoreTag Tag
	{
		get => (StoreTag)GetValue(TagProperty);
		set => SetValue(TagProperty, value);
	}

	/// <summary>The badge text in the app language (Slice 159: every word follows the app language).</summary>
	public static string LabelOf(StoreTag tag) => tag switch
	{
		StoreTag.Hot => AppResources.Store_Tag_Hot,
		StoreTag.Popular => AppResources.Store_Tag_Popular,
		StoreTag.Recommended => AppResources.Store_Tag_Recommended,
		_ => AppResources.Store_Tag_New,
	};

	/// <summary>The <see cref="DinoTokens"/> keys of the pill's fill and its text, from the mock's spec card.</summary>
	public static (string Fill, string Ink) TokensOf(StoreTag tag) => tag switch
	{
		StoreTag.Hot => (DinoTokens.TagHotFill, DinoTokens.TagHotInk),
		StoreTag.Popular => (DinoTokens.BrandTint, DinoTokens.TagPopularInk),
		StoreTag.Recommended => (DinoTokens.TagRecommendedFill, DinoTokens.TagRecommendedInk),
		_ => (DinoTokens.TagNewFill, DinoTokens.TagNewInk),
	};

	private void Refresh()
	{
		(string fill, string ink) = TokensOf(Tag);
		_label.Text = LabelOf(Tag);
		_label.TextColor = DinoTokens.Get(ink);
		BackgroundColor = DinoTokens.Get(fill);
		AutomationId = $"store-tag-{Tag}";
	}
}
