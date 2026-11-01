using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

/// <summary>What a touch on the crop frame picked up.</summary>
internal enum CropGrab
{
	None,
	Move,
	TopLeft,
	TopRight,
	BottomLeft,
	BottomRight,
}

/// <summary>The crop frame's touch rules (Slice 78): which corner or the body a touch grabs, and the frame after a drag. Pure geometry.</summary>
internal static class CropDrag
{
	private const double HandleReach = 32;

	public static CropGrab Grab(Rect frame, Point touch)
	{
		(CropGrab Kind, Point At)[] corners =
		[
			(CropGrab.TopLeft, new Point(frame.Left, frame.Top)),
			(CropGrab.TopRight, new Point(frame.Right, frame.Top)),
			(CropGrab.BottomLeft, new Point(frame.Left, frame.Bottom)),
			(CropGrab.BottomRight, new Point(frame.Right, frame.Bottom)),
		];
		(CropGrab kind, Point at) = corners.OrderBy(c => Distance(c.At, touch)).First();
		if (Distance(at, touch) <= HandleReach)
		{
			return kind;
		}

		return frame.Contains(touch) ? CropGrab.Move : CropGrab.None;
	}

	/// <summary>The frame after dragging by (<paramref name="dx"/>, <paramref name="dy"/>) in picture fractions from <paramref name="start"/>.</summary>
	public static CropRect Drag(CropRect start, CropGrab grab, double dx, double dy)
	{
		double left = start.Left;
		double top = start.Top;
		double right = start.Left + start.Width;
		double bottom = start.Top + start.Height;
		if (grab == CropGrab.Move)
		{
			double shiftX = Math.Clamp(dx, -left, 1 - right);
			double shiftY = Math.Clamp(dy, -top, 1 - bottom);
			return new CropRect(left + shiftX, top + shiftY, start.Width, start.Height);
		}

		bool movesLeft = grab is CropGrab.TopLeft or CropGrab.BottomLeft;
		bool movesTop = grab is CropGrab.TopLeft or CropGrab.TopRight;
		if (movesLeft)
		{
			left = Math.Clamp(left + dx, 0, right - CropRect.MinimumSide);
		}
		else
		{
			right = Math.Clamp(right + dx, left + CropRect.MinimumSide, 1);
		}

		if (movesTop)
		{
			top = Math.Clamp(top + dy, 0, bottom - CropRect.MinimumSide);
		}
		else
		{
			bottom = Math.Clamp(bottom + dy, top + CropRect.MinimumSide, 1);
		}

		return new CropRect(left, top, right - left, bottom - top);
	}

	private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}

/// <summary>Draws the dimmed outside, the frame and the four corner handles over the picture.</summary>
internal sealed class CropOverlay : IDrawable
{
	private const float FrameStroke = 2;
	private const float HandleRadius = 9;
	private const string DimColour = "#992b2540";

	public Rect Shown { get; set; }

	public CropRect Crop { get; set; } = CropRect.Full;

	/// <summary>The frame in the view's own coordinates.</summary>
	public Rect Frame => new(
		Shown.X + Crop.Left * Shown.Width, Shown.Y + Crop.Top * Shown.Height, Crop.Width * Shown.Width, Crop.Height * Shown.Height);

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		if (Shown.Width <= 0 || Shown.Height <= 0)
		{
			return;
		}

		Rect frame = Frame;
		canvas.FillColor = Color.FromArgb(DimColour);
		canvas.FillRectangle((float)Shown.X, (float)Shown.Y, (float)Shown.Width, (float)(frame.Top - Shown.Top));
		canvas.FillRectangle((float)Shown.X, (float)frame.Bottom, (float)Shown.Width, (float)(Shown.Bottom - frame.Bottom));
		canvas.FillRectangle((float)Shown.X, (float)frame.Top, (float)(frame.Left - Shown.Left), (float)frame.Height);
		canvas.FillRectangle((float)frame.Right, (float)frame.Top, (float)(Shown.Right - frame.Right), (float)frame.Height);
		canvas.StrokeColor = Colors.White;
		canvas.StrokeSize = FrameStroke;
		canvas.DrawRectangle(frame);
		DrawHandles(canvas, frame);
	}

	private static void DrawHandles(ICanvas canvas, Rect frame)
	{
		canvas.FillColor = DinoTokens.Get(DinoTokens.Brand);
		foreach (Point corner in new[] { new Point(frame.Left, frame.Top), new Point(frame.Right, frame.Top), new Point(frame.Left, frame.Bottom), new Point(frame.Right, frame.Bottom) })
		{
			canvas.FillCircle((float)corner.X, (float)corner.Y, HandleRadius);
			canvas.DrawCircle((float)corner.X, (float)corner.Y, HandleRadius);
		}
	}
}

/// <summary>
/// Rotates and crops one page (Slice 78): rotate left or right in quarter turns (the crop frame turns with the picture) and drag
/// the frame or its corners. Done writes the result into the page draft; Back leaves the page as it was.
/// </summary>
public partial class PageEditorPage : ContentPage, IGrownUpPage
{
	private const string PreviewNameFormat = "preview-{0}.jpg";

	private readonly WizardSession _session;
	private readonly PageDraft _page;
	private readonly CropOverlay _overlay = new();
	private readonly BusyGuard _busy = new();
	private int _turns;
	private Size _pictureSize;
	private string? _previewFile;
	private bool _previewStarted;
	private CropGrab _grab;
	private Point _grabAt;
	private CropRect _grabStart;

	public PageEditorPage(WizardSession session, PageDraft page)
	{
		InitializeComponent();
		_session = session;
		_page = page;
		_turns = page.RotationQuarterTurns;
		_overlay.Crop = page.Crop is { } c ? new CropRect(c.X, c.Y, c.Width, c.Height) : CropRect.Full;
		CropSurface.Drawable = _overlay;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_previewStarted)
		{
			return;
		}

		_previewStarted = true;
		await _busy.RunAsync(ShowFirstPreviewAsync);
	}

	// A picture that cannot be decoded or rotated must not crash the page: the editor is told and closed.
	private async Task ShowFirstPreviewAsync()
	{
		try
		{
			await ShowPreviewAsync();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			await ReportFailureAsync(ex);
			await LeaveAsync();
		}
	}

	private async Task ReportFailureAsync(Exception ex)
	{
		DinoLog.Error(_session.Services, "could not show or turn a page", ex);
		await DisplayAlertAsync(AppResources.Add_ReadFailedTitle, AppResources.Add_ReadFailedBody, AppResources.Common_Ok);
	}

	// The picture as it looks at the current rotation (a rotated copy; the original stays as it is).
	private async Task ShowPreviewAsync()
	{
		string source = _page.SourceFile!;
		string? shown = source;
		if (ImageTransform.WrapTurns(_turns) != 0)
		{
			shown = Path.Combine(_session.WorkFolder, string.Format(System.Globalization.CultureInfo.InvariantCulture, PreviewNameFormat, Guid.NewGuid().ToString("N")));
			await ImageTransform.RotateAsync(source, shown, _turns);
		}

		string? old = _previewFile;
		_previewFile = shown == source ? null : shown;
		Preview.Source = shown;
		_pictureSize = PageImageSize.Read(shown) ?? default;
		DeletePreview(old);
		UpdateOverlay();
	}

	private static void DeletePreview(string? file)
	{
		if (file is not null)
		{
			ScannedImageFiles.TryDelete(file);
		}
	}

	private void OnStageSizeChanged(object? sender, EventArgs e) => UpdateOverlay();

	// The picture is fitted into the stage, so the frame is placed by the same fit.
	private void UpdateOverlay()
	{
		if (_pictureSize.Width <= 0 || _pictureSize.Height <= 0 || Stage.Width <= 0 || Stage.Height <= 0)
		{
			return;
		}

		double scale = Math.Min(Stage.Width / _pictureSize.Width, Stage.Height / _pictureSize.Height);
		double width = _pictureSize.Width * scale;
		double height = _pictureSize.Height * scale;
		_overlay.Shown = new Rect((Stage.Width - width) / 2, (Stage.Height - height) / 2, width, height);
		CropSurface.Invalidate();
	}

	private void OnStartInteraction(object? sender, TouchEventArgs e)
	{
		_grabAt = e.Touches[0];
		_grab = CropDrag.Grab(_overlay.Frame, _grabAt);
		_grabStart = _overlay.Crop;
	}

	private void OnDragInteraction(object? sender, TouchEventArgs e)
	{
		if (_grab == CropGrab.None || _overlay.Shown.Width <= 0)
		{
			return;
		}

		double dx = (e.Touches[0].X - _grabAt.X) / _overlay.Shown.Width;
		double dy = (e.Touches[0].Y - _grabAt.Y) / _overlay.Shown.Height;
		_overlay.Crop = CropDrag.Drag(_grabStart, _grab, dx, dy);
		CropSurface.Invalidate();
	}

	private void OnEndInteraction(object? sender, TouchEventArgs e) => _grab = CropGrab.None;

	private async void OnRotateLeftClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => RotateAsync(-1));

	private async void OnRotateRightClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => RotateAsync(1));

	private async Task RotateAsync(int quarterTurns)
	{
		int previousTurns = _turns;
		CropRect previousCrop = _overlay.Crop;
		_turns = ImageTransform.WrapTurns(_turns + quarterTurns);
		_overlay.Crop = TurnedCrop(previousCrop, quarterTurns);
		try
		{
			await ShowPreviewAsync();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_turns = previousTurns;
			_overlay.Crop = previousCrop;
			await ReportFailureAsync(ex);
		}
	}

	// The frame stays on the same part of the picture as the picture turns.
	private static CropRect TurnedCrop(CropRect crop, int quarterTurns)
	{
		NormalizedRect? turned = PageDraft.CropAfterTurn(new NormalizedRect(crop.Left, crop.Top, crop.Width, crop.Height), quarterTurns);
		return turned is { } t ? new CropRect(t.X, t.Y, t.Width, t.Height) : crop;
	}

	private void OnResetClicked(object? sender, EventArgs e)
	{
		_overlay.Crop = CropRect.Full;
		CropSurface.Invalidate();
	}

	private async void OnBackTapped(object? sender, EventArgs e) => await _busy.RunAsync(LeaveAsync);

	private async Task LeaveAsync()
	{
		DeletePreview(_previewFile);
		await Navigation.PopAsync();
	}

	private async void OnDoneClicked(object? sender, EventArgs e) => await _busy.RunAsync(DoneAsync);

	private async Task DoneAsync()
	{
		NormalizedRect? crop = _overlay.Crop.IsFull ? null : ToCore(_overlay.Crop);
		bool unchanged = ImageTransform.WrapTurns(_turns) == _page.RotationQuarterTurns && Equals(crop, _page.Crop);
		if (!unchanged)
		{
			try
			{
				PageDraft updated = await _session.RenderAsync(_page.WithTransform(_turns, crop));
				_session.Draft.ReplacePageDraft(updated);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				await ReportFailureAsync(ex);
				return;
			}
		}

		await LeaveAsync();
	}

	private static NormalizedRect ToCore(CropRect crop) => new(crop.Left, crop.Top, crop.Width, crop.Height);
}
