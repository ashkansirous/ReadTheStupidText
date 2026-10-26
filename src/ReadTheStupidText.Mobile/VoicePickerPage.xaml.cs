using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Usage;

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
    private VoiceEngineKind? _listedKind;
    private readonly VoicePackInstaller _installer;
    private readonly RtstUsage _usage;
    private readonly Dictionary<string, Border> _rowsByVoiceId = new();
    private Label? _statusLabel;
    private PackInstallState _statusState;

    // What the phone voice can read (Slice 82); null until the platform has answered, when the row is still offered.
    private VoiceAvailability? _voices = PlatformVoiceAvailability.Known;

    public VoicePickerPage(
        ISpeechReader reader,
        ISettingsStore settings,
        IPlatformServices platform,
        IVoiceModelService model,
        VoicePackInstaller installer,
        RtstUsage usage)
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        BackGlyph.Text = TransportGlyphs.Back;
        _reader = reader;
        _settings = settings;
        _platform = platform;
        _model = model;
        _installer = installer;
        _usage = usage;
    }

    // The voices on offer follow the installed packs and the language, which can change while
    // the page is open (a pack finishing its download), so the page rebuilds on each change
    // and only listens while it is on screen.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _installer.Changed += OnPacksChanged;
        _model.ReadyChanged += OnPacksChanged;
        _voices = PlatformVoiceAvailability.Known;
        LeavePhoneVoiceIfItCannotReadTheApp();
        Rebuild();
        _ = LoadVoicesAsync();
    }

    private async Task LoadVoicesAsync()
    {
        bool before = PhoneCanReadAppLanguage();
        _voices = await PlatformVoiceAvailability.BuildAsync(_platform);
        if (PhoneCanReadAppLanguage() != before)
        {
            LeavePhoneVoiceIfItCannotReadTheApp();
            Rebuild();
        }
    }

    // The explicit, one-off step that moves a stored "Phone voice" choice to the natural voice when
    // the phone is known (a real answer, never an unknown phone) to have none for the app language.
    // Rendering never writes settings.
    private void LeavePhoneVoiceIfItCannotReadTheApp()
    {
        VoiceModelPaths? paths = _platform.SupportsNeuralVoice ? _model.Paths : null;
        bool leave = paths is not null
            && VoiceRouting.ShouldLeavePhoneVoice(_settings.VoiceId == SystemVoice.Id, true, PhoneCanReadAppLanguage());
        if (leave)
        {
            PhoneVoiceMigration.MoveToNatural(_settings, _reader, paths!);
        }
    }

    // The Phone voice row is offered unless the phone is known to have no voice for the app
    // language; a phone voice that would skip the script is never listed. An unknown phone hides nothing.
    private bool PhoneCanReadAppLanguage() =>
        _voices is null || _voices.CanPhoneRead(AppLanguages.Code(AppLanguageSetting.Current(_settings)));

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _installer.Changed -= OnPacksChanged;
        _model.ReadyChanged -= OnPacksChanged;
    }

    private void OnPacksChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => Refresh(ReferenceEquals(sender, _installer)));

    // A download progress tick changes only the status text, so the list is not rebuilt for it.
    private void Refresh(bool fromInstaller)
    {
        bool progressOnly = fromInstaller
            && _statusLabel is not null
            && _statusState == PackInstallState.Downloading
            && _installer.State == PackInstallState.Downloading;
        if (progressOnly)
        {
            TextFlow.ForUi(_statusLabel!, StatusText());
            return;
        }

        LeavePhoneVoiceIfItCannotReadTheApp();
        Rebuild();
    }

    private string StatusText() =>
        LocalizedPackStatus.Compose(_installer.State, _installer.Progress) ?? AppResources.VoicePicker_ReasonPackNotInstalled;

    // The voices on offer are the installed pack's for the user's language (Slice 51);
    // with none, only the phone voice reads and the page says why.
    private void Rebuild()
    {
        _rowsByVoiceId.Clear();
        _statusLabel = null;
        PhoneVoiceList.Children.Clear();
        MaleVoiceList.Children.Clear();
        FemaleVoiceList.Children.Clear();
        EngineVoiceList.Children.Clear();
        bool phoneRow = PhoneCanReadAppLanguage();
        PhoneSectionHeading.IsVisible = phoneRow;
        PhoneSectionCard.IsVisible = phoneRow;
        if (phoneRow)
        {
            PhoneVoiceList.Children.Add(BuildRow(new VoiceInfo(SystemVoice.Id, PhoneVoiceName, string.Empty)));
        }

        VoiceModelPaths? paths = _platform.SupportsNeuralVoice ? _model.Paths : null;
        NeuralVoiceSections.IsVisible = paths is not null;
        _listedKind = paths?.Kind; // the engine of the voices on screen, whatever the model service answers by the time one is tapped
        if (paths is null)
        {
            AddFallbackReason();
        }
        else
        {
            AddNeuralVoices(paths);
        }

        UpdateSelection(SelectedVoiceId(paths, phoneRow));
    }

    // Without a Phone voice row the stored "Phone voice" choice selects nothing on the list: the
    // natural voice that reads instead is highlighted, or none while the pack is missing.
    private string SelectedVoiceId(VoiceModelPaths? paths, bool phoneRow)
    {
        if (paths is null)
        {
            return phoneRow ? SystemVoice.Id : string.Empty;
        }

        return phoneRow && _settings.VoiceId == SystemVoice.Id
            ? SystemVoice.Id
            : NeuralVoiceMemory.Resolve(paths, _settings.VoiceId).Id;
    }

    private void AddFallbackReason()
    {
        if (!_platform.SupportsNeuralVoice)
        {
            PhoneVoiceList.Children.Add(BuildReasonLabel(AppResources.VoicePicker_ReasonNoNeuralEngine));
            return;
        }

        _statusLabel = BuildReasonLabel(StatusText());
        _statusState = _installer.State;
        PhoneVoiceList.Children.Add(_statusLabel);

        // Idle means a run ended without a state to show; offer the retry rather than a dead end.
        if (_installer.CanRetry || _installer.State == PackInstallState.Idle)
        {
            PhoneVoiceList.Children.Add(BuildRetryButton());
        }
    }

    private static Label BuildReasonLabel(string text)
    {
        var label = new Label { FontSize = 11, Margin = new Thickness(12, 0, 12, 10) };
        TextFlow.ForUi(label, text);
        return label;
    }

    private Button BuildRetryButton()
    {
        var retry = new Button
        {
            Text = AppResources.Language_Retry,
            Margin = new Thickness(12, 0, 12, 10),
            HorizontalOptions = LayoutOptions.Start,
        };
        TextFlow.ForUi(retry);
        retry.Clicked += (_, _) => _ = _installer.StartAsync();
        return retry;
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

        TextFlow.ForUi(EngineHeading, EngineHeadingText(paths.Kind));
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
                Text = VoiceLabel.Initial(voice),
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
            FontSize = 13.5,
            VerticalOptions = LayoutOptions.Center,
        };
        TextFlow.ForContent(nameLabel, VoiceLabel.For(voice));

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
        bool changed = _settings.VoiceId != voice.Id;
        _settings.VoiceId = voice.Id;
        _reader.SetVoice(voice.Id);
        if (changed)
        {
            CountVoiceChange(voice);
        }

        UpdateSelection(voice.Id);
        if (VoicePreviewPolicy.ShouldPreview(_reader.State))
        {
            _ = _reader.SpeakAsync(VoiceLabel.For(voice));
        }
    }

    // An anonymous count of an actual change (Slice 137): the language it is for and which engine speaks it (the engine of the pack the voice
    // belongs to; this page lists only the app language's pack and the phone voice), never the voice's name.
    private void CountVoiceChange(VoiceInfo voice)
    {
        string engine = UsageEvents.EngineFor(voice.Id == SystemVoice.Id, _listedKind);
        string language = AppLanguages.Code(AppLanguageSetting.Current(_settings));
        _usage.Record(context => UsageEvents.VoiceChanged(context, language, engine));
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
