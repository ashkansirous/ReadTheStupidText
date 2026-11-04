using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>The mouse cursors of the shelf's reorder mode (Slice 264, mock Batch 47 frame 176): an open hand while a cover is held or can be dragged, a closed hand once it is lifted. Only Windows draws them (<c>Platforms/Windows/DinoCursor.cs</c>).</summary>
internal enum DinoCursorShape
{
	Default,
	OpenHand,
	ClosedHand,
}

/// <summary>One book in the reorder mode (Slice 264): the tile, the spot it was laid out at and the spot it is in now.</summary>
internal sealed class ReorderEntry(StoryTile tile, Grid view, int homeIndex)
{
	public StoryTile Tile { get; } = tile;

	public Grid View { get; } = view;

	/// <summary>The grid spot the view's layout bounds sit at; the view moves from there with its translation.</summary>
	public int HomeIndex { get; } = homeIndex;

	/// <summary>The grid spot the book is in now (its place in the working order).</summary>
	public int Slot { get; set; } = homeIndex;

	/// <summary>Lifted by a tap, or by the sheet's Reorder row: bigger, with a shadow, still, title hidden.</summary>
	public bool Lifted { get; set; }

	/// <summary>Bumped by every StartWiggle and StopWiggle: a delayed start whose number is no longer current has been cancelled.</summary>
	public int WiggleGeneration { get; set; }

	public string Id => Tile.Story.Id;
}

/// <summary>
/// Release-visible trace of the reorder mode (Slice 264): on Windows a line in window-trace.log ("Reorder trace: step", step names only, never a title or text), nothing elsewhere.
/// Added because a Release build showed no [Reorder] log and the app died opening the mode on the VM.
/// </summary>
internal static class ReorderTrace
{
	public const string Prefix = "Reorder trace: ";

	public static void Step(string step)
	{
#if WINDOWS
		WindowTrace.Write(Prefix + step);
#endif
	}
}

/// <summary>The views and motion of the reorder mode's tiles (Slice 264, mock Batch 47 frames 173 and 174).</summary>
internal static class ShelfReorderTiles
{
	public const double WiggleDegrees = 1.6;
	public const uint WigglePeriod = 520; // 0.26 s each way
	public const int WiggleStaggerMs = 70;
	public const double LiftScale = 1.10;
	public const double LiftRise = 10;
	public const uint SlideMs = 350;
	public const string WiggleAnimation = "reorder-wiggle";
	private const double CoverCorner = 16;
	private const double OutlineStroke = 2;
	private const double TitleSize = 12.5;
	private const double GripDot = 3.5;
	private const double GripGap = 3;
	private const string LiftShadowId = "ReorderLiftShadow";
	private const float ShadowAlpha = 0.32f;
	private const double ShadowOffset = 14;
	private static readonly Color ShadowInk = Color.FromArgb("#2b2540");

	/// <summary>A tile: the cover (as on the shelf), its title under it, and, with reduced motion, a dashed outline in place of the wiggle.</summary>
	public static Grid Build(StoryTile tile, ShelfOrderGrid grid, double titleHeight, double titleSpacing, bool outline)
	{
		var cover = new StoryCover { BindingContext = tile, InputTransparent = true, AutomationId = "ReorderCover" };
		var title = new Label
		{
			Text = tile.Title, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, LineHeight = 1.25, MaxLines = 2,
			LineBreakMode = LineBreakMode.TailTruncation, HeightRequest = titleHeight, InputTransparent = true, AutomationId = "ReorderTileTitle",
		};
		var view = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)], RowSpacing = titleSpacing, AutomationId = "ReorderTile", FlowDirection = FlowDirection.LeftToRight };
		view.Add(LiftShadow(), 0, 0); // first, so it draws behind the cover
		view.Add(cover, 0, 0);
		view.Add(title, 0, 1);
		if (outline)
		{
			view.Add(DashedOutline(Colors.Transparent, OutlineStroke), 0, 0);
		}

		SemanticProperties.SetDescription(view, tile.Title);
		view.WidthRequest = grid.TileWidth;
		view.HeightRequest = grid.TileHeight;
		return view;
	}

	/// <summary>The lit target spot: Brand at 14% with a 3 dp dashed Brand ring, the size of a cover.</summary>
	public static Border LitSpot(double width, double height) => DashedOutline(Color.FromRgba(DinoTokens.Get(DinoTokens.Brand).Red, DinoTokens.Get(DinoTokens.Brand).Green, DinoTokens.Get(DinoTokens.Brand).Blue, 0.14), 3, width, height);

	private static Border DashedOutline(Color fill, double stroke, double width = -1, double height = -1) => new()
	{
		Stroke = DinoTokens.Get(DinoTokens.Brand),
		StrokeThickness = stroke,
		StrokeDashArray = [4, 3],
		BackgroundColor = fill,
		StrokeShape = new RoundRectangle { CornerRadius = CoverCorner },
		InputTransparent = true,
		WidthRequest = width,
		HeightRequest = height,
		VerticalOptions = height > 0 ? LayoutOptions.Start : LayoutOptions.Fill,
	};

	/// <summary>The six-dot grip of the sheet's Reorder row (two columns of three dots).</summary>
	public static View GripDots(Color colour)
	{
		var grid = new Grid { ColumnSpacing = GripGap, RowSpacing = GripGap, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
		for (int i = 0; i < 6; i++)
		{
			var dot = new BoxView { Color = colour, WidthRequest = GripDot, HeightRequest = GripDot, CornerRadius = GripDot / 2 };
			grid.Add(dot, i % 2, i / 2);
		}

		return grid;
	}

	/// <summary>Starts the endless +-1.6 degree rotation, each cover 0.07 s after the one before; nothing under reduced motion or for a lifted book.</summary>
	public static void StartWiggle(ReorderEntry entry, int order, IDispatcher dispatcher)
	{
		if (ReduceMotion.IsOn || entry.Lifted)
		{
			return;
		}

		View view = entry.View;
		int generation = ++entry.WiggleGeneration;
		dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(WiggleStaggerMs * (order % 8)), () =>
		{
			if (generation == entry.WiggleGeneration && !entry.Lifted && view.IsVisible)
			{
				new Animation(t => view.Rotation = WiggleDegrees * Math.Sin(2 * Math.PI * t)).Commit(view, WiggleAnimation, 16, WigglePeriod, Easing.Linear, repeat: () => true);
			}
		});
	}

	public static void StopWiggle(ReorderEntry entry)
	{
		entry.WiggleGeneration++; // cancels a start still waiting for its stagger
		entry.View.AbortAnimation(WiggleAnimation);
		entry.View.Rotation = 0;
	}

	/// <summary>The lifted look (scale 1.10, shadow, title hidden, 10 dp up) or the resting one.</summary>
	public static void ApplyLift(ReorderEntry entry)
	{
		ReorderTrace.Step($"lift apply before lifted={entry.Lifted}");
		entry.View.Scale = entry.Lifted ? LiftScale : 1;
		ReorderTrace.Step("lift scale set");
		foreach (IView child in entry.View.Children)
		{
			ApplyLiftToChild(child, entry.Lifted);
		}

		ReorderTrace.Step("lift apply after");
	}

	// The shadow is a plain element behind the cover (no MAUI Shadow: it faults on Windows composition and would look different per platform); the title hides while lifted.
	private static void ApplyLiftToChild(IView child, bool lifted)
	{
		if (child is Border { AutomationId: LiftShadowId } shadow)
		{
			shadow.IsVisible = lifted;
		}
		else if (child is Label title)
		{
			title.Opacity = lifted ? 0 : 1;
		}
	}

	private static Border LiftShadow() => new()
	{
		AutomationId = LiftShadowId,
		BackgroundColor = ShadowInk.WithAlpha(ShadowAlpha),
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = CoverCorner },
		TranslationY = ShadowOffset,
		InputTransparent = true,
		IsVisible = false,
	};
}
