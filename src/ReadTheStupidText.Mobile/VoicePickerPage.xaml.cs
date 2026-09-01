using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

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

    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly Dictionary<string, Border> _rowsByVoiceId = new();

    public VoicePickerPage(ISpeechReader reader, ISettingsStore settings)
    {
        InitializeComponent();
        _reader = reader;
        _settings = settings;

        foreach (VoiceInfo voice in SupertonicVoiceTable.Voices.Where(v => !SupertonicVoiceTable.IsFemale(v.Id)))
        {
            MaleVoiceList.Children.Add(BuildRow(voice));
        }

        foreach (VoiceInfo voice in SupertonicVoiceTable.Voices.Where(v => SupertonicVoiceTable.IsFemale(v.Id)))
        {
            FemaleVoiceList.Children.Add(BuildRow(voice));
        }

        UpdateSelection(_settings.VoiceId ?? SupertonicVoiceTable.Default.Id);
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
