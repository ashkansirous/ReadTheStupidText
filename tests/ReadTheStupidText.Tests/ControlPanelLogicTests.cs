using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

// Slice 164: decisions that used to live in code-behinds (control panel, reader and voice picker).
public class ControlPanelLogicTests
{
    [Theory]
    [InlineData(PlaybackState.Idle, ReadStatusKind.Ready)]
    [InlineData(PlaybackState.Paused, ReadStatusKind.Paused)]
    public void Status_for_a_state_that_is_not_playing(PlaybackState state, ReadStatusKind expected)
    {
        Assert.Equal(expected, ReadStatusLine.For(state, true, "Word").Kind);
    }

    [Fact]
    public void Status_playing_names_the_clipboard_or_the_app()
    {
        Assert.Equal(ReadStatusKind.ReadingClipboard, ReadStatusLine.For(PlaybackState.Playing, true, "Word").Kind);
        Assert.Equal(ReadStatusKind.ReadingSelection, ReadStatusLine.For(PlaybackState.Playing, false, " ").Kind);
        Assert.Equal(ReadStatusKind.ReadingSelection, ReadStatusLine.For(PlaybackState.Playing, false, null).Kind);

        var from = ReadStatusLine.For(PlaybackState.Playing, false, "Word");
        Assert.Equal(ReadStatusKind.ReadingSelectionFrom, from.Kind);
        Assert.Equal("Word", from.App);
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(false, true, false, true, false)]
    [InlineData(false, true, true, true, true)]
    [InlineData(true, true, true, true, true)]
    public void Play_button_face(bool generating, bool waiting, bool canCancel, bool spinner, bool cancel)
    {
        var face = PlayButtonFace.For(generating, waiting, canCancel);

        Assert.Equal(spinner, face.ShowSpinner);
        Assert.Equal(cancel, face.ShowCancel);
        Assert.Equal(!spinner, face.ShowPlayPause);
    }

    [Fact]
    public void Cancel_never_shows_without_a_wait()
    {
        Assert.False(PlayButtonFace.For(true, false, true).ShowCancel);
    }

    [Fact]
    public void Panel_is_clamped_into_the_work_area()
    {
        var work = new WorkArea(100, 50, 1000, 800);

        Assert.Equal((100, 50), PanelPlacement.ClampToWorkArea(0, 0, 300, 400, work));
        Assert.Equal((800, 450), PanelPlacement.ClampToWorkArea(5000, 5000, 300, 400, work));
        Assert.Equal((400, 300), PanelPlacement.ClampToWorkArea(400, 300, 300, 400, work));
    }

    [Fact]
    public void A_panel_bigger_than_the_work_area_pins_to_its_corner()
    {
        Assert.Equal((10, 20), PanelPlacement.ClampToWorkArea(500, 500, 900, 900, new WorkArea(10, 20, 400, 400)));
    }

    [Theory]
    [InlineData(96u, 1.0)]
    [InlineData(144u, 1.5)]
    [InlineData(0u, 1.0)]
    public void Scale_comes_from_dpi(uint dpi, double scale)
    {
        Assert.Equal(scale, PanelPlacement.ScaleForDpi(dpi));
    }

    [Fact]
    public void Device_size_rounds_up_and_guards_the_width()
    {
        Assert.Equal((564, 780), PanelPlacement.DeviceSize(376, 520, 376, 1.5));
        Assert.Equal((470, 8), PanelPlacement.DeviceSize(376, 6.2, 376, 1.25));
        Assert.Equal((376, 520), PanelPlacement.DeviceSize(double.NaN, 520, 376, 1.0));
        Assert.Equal((376, 520), PanelPlacement.DeviceSize(0, 520, 376, 1.0));
        Assert.Equal((376, 520), PanelPlacement.DeviceSize(double.PositiveInfinity, 520, 376, 1.0));
    }

    [Fact]
    public void Corner_pin_insets_by_the_margin_and_stays_on_screen()
    {
        Assert.Equal((680, 330), PanelPlacement.CornerPin(300, 400, 20, new WorkArea(0, 0, 1000, 750)));
        Assert.Equal((10, 20), PanelPlacement.CornerPin(900, 900, 20, new WorkArea(10, 20, 400, 400)));
    }

    [Theory]
    [InlineData(PlaybackState.Playing, 0, 0, true)]
    [InlineData(PlaybackState.Playing, 3, 9, true)]
    [InlineData(PlaybackState.Idle, 3, 3, true)]
    [InlineData(PlaybackState.Idle, 0, 0, false)]
    [InlineData(PlaybackState.Idle, 3, 2, false)]
    [InlineData(PlaybackState.Paused, 3, 3, false)]
    public void Pending_read_settles(PlaybackState state, int pending, int returned, bool done)
    {
        Assert.Equal(done, PendingReadSettle.IsDone(state, pending, returned));
    }

    [Theory]
    [InlineData(false, false, false, PickerSelection.None)]
    [InlineData(false, true, false, PickerSelection.Phone)]
    [InlineData(false, true, true, PickerSelection.Phone)]
    [InlineData(true, true, true, PickerSelection.Phone)]
    [InlineData(true, true, false, PickerSelection.Natural)]
    [InlineData(true, false, true, PickerSelection.Natural)]
    [InlineData(true, false, false, PickerSelection.Natural)]
    public void Picker_selection(bool pack, bool phoneRow, bool phoneStored, PickerSelection expected)
    {
        Assert.Equal(expected, VoiceRouting.SelectedRow(pack, phoneRow, phoneStored));
    }
}
