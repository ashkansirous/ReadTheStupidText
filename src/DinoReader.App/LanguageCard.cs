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
	private readonly Label _value = SettingsRows.ValueLabel(string.Empty);
	private readonly ToggleSwitch? _switch;

	public LanguageCard(bool canTranslate, Func<Task> chooseLanguage, BusyGuard busy)
	{
		BackgroundColor = Colors.White;
		StrokeThickness = 0;
		StrokeShape = new RoundRectangle { CornerRadius = 18 };
		var rows = new VerticalStackLayout();
		rows.Children.Add(SettingsRows.Row(AppResources.Story_Language, _value, chooseLanguage, busy));
		if (canTranslate)
		{
			_switch = new ToggleSwitch { VerticalOptions = LayoutOptions.Center };
			_switch.Toggled += (_, _) => TranslateChanged?.Invoke();
			rows.Children.Add(SettingsRows.Divider());
			rows.Children.Add(SettingsRows.Row(AppResources.Story_Translate, _switch, null, busy));
		}

		Content = rows;
	}

	public event Action? TranslateChanged;

	public bool Translate
	{
		get => _switch?.IsOn == true;
		set
		{
			if (_switch is not null)
			{
				_switch.IsOn = value;
			}
		}
	}

	/// <summary>Shows the language by its own name, with "· detected" while it is the app's guess.</summary>
	public void Show(string language, bool detected)
	{
		string name = LanguageNames.NativeName(language);
		_value.Text = detected ? AppResources.Format(AppResources.Story_LanguageDetected, name) : name;
	}
}
