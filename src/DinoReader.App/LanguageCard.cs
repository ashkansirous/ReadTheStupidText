using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The white card of the wizard's Text step (mock 9) with the story language row ("English · detected") and, when this
/// build can translate, the "Translate to my language" switch row.
/// </summary>
public sealed class LanguageCard : Border
{
	private const string RtlIsolate = "\u2067";
	private const string PopDirection = "\u2069";

	private readonly Label _value = SettingsRows.ValueLabel(string.Empty);
	private readonly Label _label = new() { Text = AppResources.Story_Translate, FontSize = 14.5 };
	private readonly Label _note = new() { FontSize = 12.5 };
	private readonly ToggleSwitch _switch = new() { VerticalOptions = LayoutOptions.Center };
	private bool _canTranslate;
	private bool _syncing;

	// What the grown-up (or the new-book default) wants. A greyed switch only shows it off; it never overwrites it, so a book made on
	// Windows still translates once it opens on Android (Slice 107).
	private bool _wanted;

	public LanguageCard(bool canTranslate, bool engineAvailable, string storyLanguage, Func<Task> chooseLanguage, BusyGuard busy)
	{
		BackgroundColor = Colors.White;
		StrokeThickness = 0;
		StrokeShape = new RoundRectangle { CornerRadius = 18 };
		var rows = new VerticalStackLayout();
		rows.Children.Add(SettingsRows.Row(AppResources.Story_Language, _value, chooseLanguage, busy));
		_switch.Toggled += OnSwitchToggled;
		rows.Children.Add(SettingsRows.Divider());
		rows.Children.Add(TranslateRow());
		Content = rows;
		SetCanTranslate(canTranslate, engineAvailable, storyLanguage);
	}

	public event Action? TranslateChanged;

	/// <summary>The wanted setting, kept while the switch is greyed (the switch then shows off, the value stays).</summary>
	public bool Translate
	{
		get => _wanted;
		set
		{
			_wanted = value;
			SyncSwitch();
		}
	}

	/// <summary>Enables the switch, or greys it and refreshes the second line: "Can't translate {language} yet" when this build has an engine that
	/// lacks the pair, "works on Android for now" when it has none.</summary>
	public void SetCanTranslate(bool canTranslate, bool engineAvailable, string storyLanguage)
	{
		_canTranslate = canTranslate;
		_switch.IsEnabled = canTranslate;
		_label.TextColor = DinoTokens.Get(canTranslate ? DinoTokens.Ink : DinoTokens.DisabledLabel);
		_note.TextColor = DinoTokens.Get(canTranslate ? DinoTokens.Muted : DinoTokens.DisabledNote);
		if (canTranslate)
		{
			_note.FormattedText = NamedNote(AppResources.Story_TranslateReadsIn, LanguageNames.NativeName(StoryTranslator.MyLanguage));
		}
		else if (engineAvailable)
		{
			_note.FormattedText = NamedNote(AppResources.Story_TranslateUnsupportedPair, LanguageNames.NativeName(storyLanguage));
		}
		else
		{
			_note.FormattedText = null;
			_note.Text = AppResources.Story_TranslateAndroidOnly;
		}

		SyncSwitch();
	}

	// The switch row (Slice 107, mock frame 37): the label and a second line naming the language it reads and shows in.
	private Grid TranslateRow()
	{
		var texts = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(0, 10) };
		texts.Children.Add(_label);
		texts.Children.Add(_note);
		var row = new Grid { Padding = new Thickness(14, 0), ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		row.Add(texts, 0);
		row.Add(_switch, 1);
		return row;
	}

	private void OnSwitchToggled(object? sender, EventArgs e)
	{
		if (_syncing || !_canTranslate)
		{
			return;
		}

		_wanted = _switch.IsOn;
		TranslateChanged?.Invoke();
	}

	private void SyncSwitch()
	{
		_syncing = true;
		_switch.IsOn = _wanted && _canTranslate;
		_syncing = false;
	}

	// A note with the language name in Vazirmatn, isolated right to left when it is Farsi or Arabic (mock frame 37). A locale string that lost its
	// {0} still shows the name, after the text.
	private static FormattedString NamedNote(string template, string name)
	{
		bool rtl = TextDirection.StartsRightToLeft(name);
		string shown = rtl ? RtlIsolate + name + PopDirection : name;
		string full = string.Format(AppResources.Culture, template, shown);
		int at = full.IndexOf(shown, StringComparison.Ordinal);
		if (at < 0)
		{
			full = full + " " + shown;
			at = full.Length - shown.Length;
		}

		var text = new FormattedString();
		text.Spans.Add(new Span { Text = full[..at] });
		text.Spans.Add(new Span { Text = shown, FontFamily = rtl ? DinoFonts.Persian : null });
		text.Spans.Add(new Span { Text = full[(at + shown.Length)..] });
		return text;
	}

	/// <summary>Shows the language by its own name, with "· detected" while it is the app's guess.</summary>
	public void Show(string language, bool detected)
	{
		string name = LanguageNames.NativeName(language);
		_value.Text = detected ? AppResources.Format(AppResources.Story_LanguageDetected, name) : name;
	}
}
