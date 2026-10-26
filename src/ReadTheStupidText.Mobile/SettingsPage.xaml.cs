using System.Globalization;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Usage;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The one Settings page (Slice 99, Decision 103): the app language, the voice (opens
/// <see cref="VoicePickerPage"/>) and the reading speed. The reading page keeps none of them.
/// </summary>
public partial class SettingsPage : ContentPage
{
    private const string CountsSwitchOffLight = "#33000000";
    private const string CountsSwitchOffDark = "#4DFFFFFF";

    private readonly ISettingsStore _settings;
    private readonly ISpeechReader _reader;
    private readonly IVoiceModelService _model;
    private readonly EditorDraft _draft;
    private readonly RtstUsage _usage;

    public SettingsPage(ISettingsStore settings, ISpeechReader reader, IVoiceModelService model, EditorDraft draft, RtstUsage usage)
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        _settings = settings;
        _reader = reader;
        _model = model;
        _draft = draft;
        _usage = usage;
        ApplyStaticTexts();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // The voice picker may have changed the voice while this page was off-stack.
        RenderValues();
        UpdateSpeedHighlight();
        RenderCountsSwitch();
        Microsoft.Maui.Controls.Application.Current!.RequestedThemeChanged += OnThemeChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Microsoft.Maui.Controls.Application.Current!.RequestedThemeChanged -= OnThemeChanged;
    }

    // The off track is coloured in code, so it follows a theme change made while this page is open.
    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RenderCountsSwitch);
    }

    private void ApplyStaticTexts()
    {
        TextFlow.ForUi(TitleLabel, AppResources.Settings_Title);
        TextFlow.ForUi(LanguageTitle, AppResources.Common_Language);
        TextFlow.ForUi(LanguageCaption, AppResources.Settings_LanguageCaption);
        TextFlow.ForUi(VoiceTitle, AppResources.Common_Voice);
        TextFlow.ForUi(SpeedTitle, AppResources.Settings_ReadingSpeed);
        TextFlow.ForUi(CountsTitle, AppResources.Settings_CountsTitle);
        TextFlow.ForUi(CountsCaption, AppResources.Settings_CountsCaption);
        SemanticProperties.SetDescription(BackButton, AppResources.Common_Back);
    }

    private void RenderValues()
    {
        TextFlow.ForContent(LanguageValue, LanguageChoices.NativeName(AppLanguageSetting.Current(_settings)));
        TextFlow.ForUi(VoiceValue, CurrentVoiceName());
    }

    // The voice that reads in the app language: the phone voice until the pack is ready, or when chosen.
    private string CurrentVoiceName()
    {
        VoiceModelPaths? paths = _model.Paths;
        bool phoneReads = _settings.VoiceId == SystemVoice.Id || !_model.IsReady || paths is null;
        return phoneReads ? VoicePickerPage.PhoneVoiceName : VoiceLabel.For(NeuralVoiceMemory.Resolve(paths!, _settings.VoiceId));
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    // The "Help improve the app" card (Slice 137, Decision 143): on by default; off sends and keeps nothing.
    private void OnCountsTapped(object? sender, TappedEventArgs e)
    {
        _usage.Enabled = !_usage.Enabled;
        RenderCountsSwitch();
    }

    private void RenderCountsSwitch()
    {
        bool on = _usage.Enabled;
        bool isDark = Microsoft.Maui.Controls.Application.Current!.RequestedTheme == AppTheme.Dark;
        CountsSwitch.BackgroundColor = on
            ? (Color)Microsoft.Maui.Controls.Application.Current!.Resources["Primary"]
            : Color.FromArgb(isDark ? CountsSwitchOffDark : CountsSwitchOffLight);
        CountsKnob.HorizontalOptions = on ? LayoutOptions.End : LayoutOptions.Start;
        SemanticProperties.SetDescription(CountsCard, $"{AppResources.Settings_CountsTitle}, {(on ? AppResources.Settings_CountsOn : AppResources.Settings_CountsOff)}");
    }

    private async void OnLanguageRowTapped(object? sender, TappedEventArgs e) =>
        await LanguageChooser.ChooseAsync(this, _settings, _draft.PrepareForRebuild);

    private async void OnVoiceRowTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(AppShell.VoicePickerRoute);

    private void OnSpeedPresetClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { ClassId: string classId } || !double.TryParse(classId, CultureInfo.InvariantCulture, out double value))
        {
            return;
        }

        var speed = new PlaybackRate(value);
        _settings.Speed = speed;
        _reader.SetSpeed(speed);
        UpdateSpeedHighlight();
    }

    private void UpdateSpeedHighlight()
    {
        bool isDark = Microsoft.Maui.Controls.Application.Current!.RequestedTheme == AppTheme.Dark;
        var unselectedBackground = (Color)Microsoft.Maui.Controls.Application.Current!.Resources[isDark ? "ChipBackgroundDark" : "ChipBackgroundLight"];
        var unselectedText = (Color)Microsoft.Maui.Controls.Application.Current!.Resources[isDark ? "TextSecondaryDark" : "TextSecondaryLight"];
        var selectedBackground = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["Primary"];
        double current = _settings.Speed.Value;

        foreach (Button button in SpeedPresetGrid.Children.OfType<Button>())
        {
            bool selected = button.ClassId is string classId
                && double.TryParse(classId, CultureInfo.InvariantCulture, out double value)
                && new PlaybackRate(value).Value == current;

            button.BackgroundColor = selected ? selectedBackground : unselectedBackground;
            button.TextColor = selected ? Colors.White : unselectedText;
        }
    }
}
