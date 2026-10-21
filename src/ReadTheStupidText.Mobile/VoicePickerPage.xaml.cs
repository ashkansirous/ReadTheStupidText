using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Resources.Strings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Screen 3 — voice picker (Slice 31, Decision 43). Lists the ten bundled
/// Supertonic voices grouped by MALE/FEMALE; tapping a row selects it (persisted
/// via <see cref="ISettingsStore"/>) and speaks its display name as the preview
/// — hearing the voice *is* the preview, so there is no separate preview
/// affordance to wire up. A change applies at the next chunk if a read is
/// already in progress (Slice 23's Windows behavior, inherited for free since
/// both platforms' neural readers use the same mid-read voice-swap logic).
/// </summary>
public partial class VoicePickerPage : ContentPage
{
    private static readonly Color SelectedRowTint = Color.FromArgb("#125B57E8");
    private static readonly Color SelectedCheckColor = Color.FromArgb("#5B57E8");

    /// <summary>Display name of the labelled system voice (Slice 50, Decision 61), in the current language.</summary>
    public static string PhoneVoiceName => AppResources.VoicePicker_PhoneVoiceName;

    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IPlatformServices _platform;
    private readonly IVoiceModelService _model;
    private readonly VoicePackInstaller _installer;
    private readonly Dictionary<string, Border> _rowsByVoiceId = new();

    public VoicePickerPage(
        ISpeechReader reader,
        ISettingsStore settings,
        IPlatformServices platform,
        IVoiceModelService model,
        VoicePackInstaller installer)
    {
        InitializeComponent();
        FlowDirection = UiFlowDirection.ForCurrentCulture();
        BackGlyph.Text = AppResources.Common_BackGlyph;
        HeaderGrid.Padding = UiFlowDirection.Mirror(HeaderGrid.Padding);
        TitleStack.Margin = UiFlowDirection.Mirror(TitleStack.Margin);
        _reader = reader;
        _settings = settings;
        _platform = platform;
        _model = model;
        _installer = installer;
    }

    // The voices on offer follow the installed packs and the language, which can change while
    // the page is open (a pack finishing its download), so the page rebuilds on each change
    // and only listens while it is on screen.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _installer.Changed += OnPacksChanged;
        _model.ReadyChanged += OnPacksChanged;
        Rebuild();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _installer.Changed -= OnPacksChanged;
        _model.ReadyChanged -= OnPacksChanged;
    }

    private void OnPacksChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(Rebuild);

    // The voices on offer are the installed pack's for the user's language (Slice 51);
    // with none, only the phone voice reads and the page says why.
    private void Rebuild()
    {
        _rowsByVoiceId.Clear();
        PhoneVoiceList.Children.Clear();
        MaleVoiceList.Children.Clear();
        FemaleVoiceList.Children.Clear();
        EngineVoiceList.Children.Clear();
        PhoneVoiceList.Children.Add(BuildRow(new VoiceInfo(SystemVoice.Id, PhoneVoiceName, string.Empty)));

        VoiceModelPaths? paths = _platform.SupportsNeuralVoice ? _model.Paths : null;
        NeuralVoiceSections.IsVisible = paths is not null;
        if (paths is null)
        {
            AddFallbackReason();
        }
        else
        {
            AddNeuralVoices(paths);
        }

        UpdateSelection(SelectedVoiceId(paths));
    }

    private string SelectedVoiceId(VoiceModelPaths? paths) =>
        paths is null || _settings.VoiceId == SystemVoice.Id
            ? SystemVoice.Id
            : NeuralVoiceMemory.Resolve(paths, _settings.VoiceId).Id;

    private void AddFallbackReason()
    {
        string reason = _platform.SupportsNeuralVoice
            ? PackStatusText.Compose(_installer.State, _installer.Progress) ?? AppResources.VoicePicker_ReasonPackNotInstalled
            : AppResources.VoicePicker_ReasonNoNeuralEngine;
        PhoneVoiceList.Children.Add(new Label
        {
            Text = reason,
            FontSize = 11,
            Margin = new Thickness(12, 0, 12, 10),
        });
    }

    // Supertonic keeps its MALE/FEMALE cards (Decision 43); any other engine's voices form
    // one card headed by the engine's name.
    private void AddNeuralVoices(VoiceModelPaths paths)
    {
        bool supertonic = paths.Kind == VoiceEngineKind.Supertonic;
        SupertonicSections.IsVisible = supertonic;
        EngineSection.IsVisible = !supertonic;
        if (supertonic)
        {
            AddSupertonicVoices(paths.AvailableVoices);
            return;
        }

        EngineHeading.Text = EngineHeadingText(paths.Kind);
        foreach (VoiceInfo voice in paths.AvailableVoices)
        {
            EngineVoiceList.Children.Add(BuildRow(voice));
        }
    }

    private static string EngineHeadingText(VoiceEngineKind kind) => kind switch
    {
        VoiceEngineKind.Supertonic => AppResources.VoicePicker_EngineSupertonic,
        VoiceEngineKind.Vits => AppResources.VoicePicker_EnginePiper,
        VoiceEngineKind.Melo => AppResources.VoicePicker_EngineMelo,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown voice engine."),
    };

    private void AddSupertonicVoices(IReadOnlyList<VoiceInfo> voices)
    {
        foreach (VoiceInfo voice in voices.Where(v => !SupertonicVoiceTable.IsFemale(v.Id)))
        {
            MaleVoiceList.Children.Add(BuildRow(voice));
        }

        foreach (VoiceInfo voice in voices.Where(v => SupertonicVoiceTable.IsFemale(v.Id)))
        {
            FemaleVoiceList.Children.Add(BuildRow(voice));
        }
    }

    private Border BuildRow(VoiceInfo voice)
    {
        var avatar = new Border
        {
            WidthRequest = 30,
            HeightRequest = 30,
            Padding = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 15 },
            Stroke = Colors.Transparent,
            BackgroundColor = Color.FromArgb("#5B57E8"),
            Content = new Label
            {
                Text = voice.DisplayName[..1],
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            },
        };

        var check = new Label
        {
            Text = "✓",
            TextColor = SelectedCheckColor,
            FontAttributes = FontAttributes.Bold,
            IsVisible = false,
            VerticalOptions = LayoutOptions.Center,
        };

        var nameLabel = new Label
        {
            Text = voice.DisplayName,
            FontSize = 13.5,
            VerticalOptions = LayoutOptions.Center,
        };

        var row = new Grid
        {
            Padding = new Thickness(12, 10),
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            ],
            ColumnSpacing = 12,
        };
        row.Add(avatar, 0);
        row.Add(nameLabel, 1);
        row.Add(check, 2);

        var border = new Border
        {
            StrokeShape = new Rectangle(),
            Stroke = Colors.Transparent,
            BackgroundColor = Colors.Transparent,
            Content = row,
        };
        border.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => SelectVoice(voice)),
        });

        _rowsByVoiceId[voice.Id] = border;
        return border;
    }

    private void SelectVoice(VoiceInfo voice)
    {
        _settings.VoiceId = voice.Id;
        _reader.SetVoice(voice.Id);
        UpdateSelection(voice.Id);
        _ = _reader.SpeakAsync(voice.DisplayName);
    }

    private void UpdateSelection(string selectedVoiceId)
    {
        foreach ((string voiceId, Border row) in _rowsByVoiceId)
        {
            bool selected = voiceId == selectedVoiceId;
            row.BackgroundColor = selected ? SelectedRowTint : Colors.Transparent;
            if (row.Content is Grid { Children: [_, _, Label check] })
            {
                check.IsVisible = selected;
            }
        }
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
