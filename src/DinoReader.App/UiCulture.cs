using System.Globalization;
using System.Runtime.CompilerServices;
using DinoReader.Core;
using Microsoft.Maui.Handlers;

namespace DinoReader;

/// <summary>
/// Picks and applies the culture the screens speak (Slice 66, Decision 74): the device language when it is one
/// of the fourteen, else English. Dino Reader has no in-app UI language picker: a child's device already has one.
/// </summary>
public static class UiCulture
{
	/// <summary>The culture for a device culture: one of the fourteen languages, English when the device speaks another.</summary>
	public static CultureInfo Resolve(CultureInfo device) => CultureInfo.GetCultureInfo(UiLanguage.Resolve(device.Name));

	/// <summary>
	/// Makes <paramref name="culture"/> the UI culture of every thread. Only the process-wide default is set (the per-flow
	/// <c>CurrentUICulture</c> is undone when an async continuation returns), and number and date formats stay the device's.
	/// </summary>
	public static void Apply(CultureInfo culture)
	{
		CultureInfo.DefaultThreadCurrentUICulture = culture;
		CultureInfo.CurrentUICulture = culture;
	}

	public static bool IsRightToLeft => UiLanguage.IsRightToLeft(CultureInfo.CurrentUICulture.Name);
}

/// <summary>
/// Dino's layout never mirrors (Slice 96, Decision 100): every screen is left to right in every language. Only a text
/// element takes its direction from its own text, through <see cref="ForText"/>.
/// </summary>
public static class UiFlow
{
	/// <summary>The direction of a screen: always left to right.</summary>
	public static readonly FlowDirection Screen = FlowDirection.LeftToRight;

	/// <summary>The direction of one piece of text: right to left when its first strong letter is Arabic-script or Hebrew.</summary>
	public static FlowDirection ForText(string? text) =>
		TextDirection.StartsRightToLeft(text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}

/// <summary>Binds a label's <c>FlowDirection</c> to its own text (Slice 96).</summary>
public sealed class TextFlowConverter : IValueConverter
{
	public static readonly TextFlowConverter Instance = new();

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => UiFlow.ForText(value as string);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Gives every label, entry and editor the direction of its own text (Slice 96), so no screen has to bind it label by label.
/// The mapping runs on every text change. It leaves alone any element whose direction was set or bound by the page, and a
/// line drawn from formatted spans (which sets its own); empty plain text goes back to left to right.
/// </summary>
public static class TextFlowHandlers
{
	private static readonly ConditionalWeakTable<VisualElement, object> Driven = new();

	public static void Register()
	{
		LabelHandler.Mapper.AppendToMapping(nameof(IText.Text), (_, view) => Follow(view, (view as Label)?.Text, (view as Label)?.FormattedText is not null));
		EntryHandler.Mapper.AppendToMapping(nameof(IText.Text), (_, view) => Follow(view, (view as Entry)?.Text, false));
		EditorHandler.Mapper.AppendToMapping(nameof(IText.Text), (_, view) => Follow(view, (view as Editor)?.Text, false));
	}

	private static void Follow(IView view, string? text, bool hasFormattedText)
	{
		if (view is not VisualElement element || !MayDrive(element) || (hasFormattedText && string.IsNullOrEmpty(text)))
		{
			return;
		}

		FlowDirection wanted = UiFlow.ForText(text);
		if (element.FlowDirection != wanted)
		{
			element.FlowDirection = wanted;
		}

		Driven.AddOrUpdate(element, Driven);
	}

	/// <summary>True unless the page set or bound the element's direction itself (not this mapper).</summary>
	private static bool MayDrive(VisualElement element) =>
		!element.IsSet(VisualElement.FlowDirectionProperty) || Driven.TryGetValue(element, out _);
}
