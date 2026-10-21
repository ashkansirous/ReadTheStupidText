using ReadTheStupidText.Application.Images;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// OCR with line boxes on Windows (Slice 67) over <c>Windows.Media.Ocr</c>: the engine of the
/// requested language when its OCR language is installed, else the one for the user's profile
/// languages. Each line's box is the union of its words' bounding rects, in the pixels of the
/// upright image (EXIF orientation applied), scaled down first when the picture exceeds the
/// engine's maximum. Everything runs on the device.
/// </summary>
public sealed class WindowsOcrTextExtractor : IImageTextExtractor, IOcrLineExtractor
{
	private const string ChineseCode = "zh";
	private const string ChineseTag = "zh-Hans-CN";
	private const string JapaneseCode = "ja";

	public async Task<string> ExtractTextAsync(string imagePath) =>
		(await ReadAsync(imagePath, language: null, CancellationToken.None).ConfigureAwait(false)).Text;

	public Task<ImageText> ExtractLinesAsync(string imagePath, string language, CancellationToken cancellationToken) =>
		ReadAsync(imagePath, language, cancellationToken);

	private static async Task<ImageText> ReadAsync(string location, string? language, CancellationToken cancellationToken)
	{
		string path = ScannedText.ToLocalPath(location)
			?? throw new IOException($"Not a local image: '{location}'.");
		(OcrEngine engine, bool matchesLanguage) = CreateEngine(language);
		using SoftwareBitmap bitmap = await DecodeAsync(path, (int)OcrEngine.MaxImageDimension).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();

		OcrResult result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken).ConfigureAwait(false);
		IReadOnlyList<ReadTheStupidText.Application.Images.OcrLine> lines = ToLines(result, language);
		OcrSource source = matchesLanguage ? OcrSource.MlKit : OcrSource.PlatformFallback;
		return new ImageText(lines, source, bitmap.PixelWidth, bitmap.PixelHeight);
	}

	// The requested language's engine if Windows has its OCR language; otherwise the profile
	// languages' engine, flagged so the page can say the language is not read well here.
	private static (OcrEngine Engine, bool MatchesLanguage) CreateEngine(string? language)
	{
		if (language is not null)
		{
			var requested = new Language(language == ChineseCode ? ChineseTag : language);
			if (OcrEngine.IsLanguageSupported(requested) && OcrEngine.TryCreateFromLanguage(requested) is { } exact)
			{
				return (exact, true);
			}
		}

		OcrEngine engine = OcrEngine.TryCreateFromUserProfileLanguages() ?? throw new OcrUnavailableException();
		return (engine, language is null);
	}

	private static async Task<SoftwareBitmap> DecodeAsync(string path, int maxSide)
	{
		StorageFile file = await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);
		using IRandomAccessStream stream = await file.OpenReadAsync().AsTask().ConfigureAwait(false);
		BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
		return await decoder.GetSoftwareBitmapAsync(
			BitmapPixelFormat.Bgra8,
			BitmapAlphaMode.Premultiplied,
			WindowsImageScale.Fit(decoder, maxSide),
			ExifOrientationMode.RespectExifOrientation,
			ColorManagementMode.DoNotColorManage).AsTask().ConfigureAwait(false);
	}

	private static IReadOnlyList<ReadTheStupidText.Application.Images.OcrLine> ToLines(OcrResult result, string? language) =>
		result.Lines
			.Where(l => l.Words.Count > 0)
			.Select(l => new ReadTheStupidText.Application.Images.OcrLine(LineText(l, language), BoxOf(l)))
			.Where(l => !string.IsNullOrWhiteSpace(l.Text))
			.ToList();

	// Chinese and Japanese words are single characters or short runs the engine already spaced.
	private static string LineText(Windows.Media.Ocr.OcrLine line, string? language) =>
		language is ChineseCode or JapaneseCode
			? string.Concat(line.Words.Select(w => w.Text))
			: line.Text;

	private static OcrBox BoxOf(Windows.Media.Ocr.OcrLine line)
	{
		float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
		foreach (OcrWord word in line.Words)
		{
			left = Math.Min(left, (float)word.BoundingRect.Left);
			top = Math.Min(top, (float)word.BoundingRect.Top);
			right = Math.Max(right, (float)word.BoundingRect.Right);
			bottom = Math.Max(bottom, (float)word.BoundingRect.Bottom);
		}

		return new OcrBox(left, top, right, bottom);
	}
}
