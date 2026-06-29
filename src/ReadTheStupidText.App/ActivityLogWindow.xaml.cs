using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Domain.Activity;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.UI;

namespace ReadTheStupidText_App;

/// <summary>
/// A normal, resizable window that renders the <see cref="IActivityLog"/> live:
/// each entry is one row whose state updates in place. Seeds from the existing
/// entries on open and then follows the log's add/change events. Read-only; the
/// Clear button only empties this view, not the underlying log.
/// </summary>
public sealed partial class ActivityLogWindow : Window
{
    private const int MaxRows = 200;

    private readonly IActivityLog _log;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Dictionary<int, ActivityRowVm> _byId = new();

    public ActivityLogWindow(IActivityLog log)
    {
        _log = log;
        InitializeComponent();
        Title = "Read The Stupid Text — Activity log";
        AppWindow.Resize(new SizeInt32(980, 480));
        ConfigureWindowChrome();

        foreach (ActivityEntry entry in _log.Entries)
        {
            AddRow(entry);
        }

        _log.EntryAdded += OnEntryAdded;
        _log.EntryChanged += OnEntryChanged;
        Closed += OnClosed;
    }

    public ObservableCollection<ActivityRowVm> Rows { get; } = new();

    // Brand the window chrome: the glasses mark in the title bar (replacing the
    // default placeholder logo) and a brand-coloured caption that flows into the
    // gradient header below, so the diagnostic window matches the control panel.
    private void ConfigureWindowChrome()
    {
        AppWindow.SetIcon("Assets/AppIcon.ico");

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        Color brand = ColorHelper.FromArgb(255, 0x5B, 0x57, 0xE8);
        Color brandHover = ColorHelper.FromArgb(255, 0x6E, 0x6A, 0xEE);
        Color brandPressed = ColorHelper.FromArgb(255, 0x4C, 0x48, 0xD0);
        Color inactiveText = ColorHelper.FromArgb(0xB3, 0xFF, 0xFF, 0xFF);

        AppWindowTitleBar bar = AppWindow.TitleBar;
        bar.BackgroundColor = brand;
        bar.InactiveBackgroundColor = brand;
        bar.ForegroundColor = Colors.White;
        bar.InactiveForegroundColor = inactiveText;
        bar.ButtonBackgroundColor = brand;
        bar.ButtonInactiveBackgroundColor = brand;
        bar.ButtonForegroundColor = Colors.White;
        bar.ButtonInactiveForegroundColor = inactiveText;
        bar.ButtonHoverBackgroundColor = brandHover;
        bar.ButtonHoverForegroundColor = Colors.White;
        bar.ButtonPressedBackgroundColor = brandPressed;
        bar.ButtonPressedForegroundColor = Colors.White;
    }

    private void OnEntryAdded(object? sender, ActivityEntry entry) =>
        _dispatcher.TryEnqueue(() => AddRow(entry));

    private void OnEntryChanged(object? sender, ActivityEntry entry) =>
        _dispatcher.TryEnqueue(() =>
        {
            if (_byId.TryGetValue(entry.Id, out ActivityRowVm? row))
            {
                row.State = ActivityStateText.ForDisplay(entry.State);
                row.Reason = ActivityReasonText.ForDisplay(entry.Reason);
                row.FirstAudio = TimingText.ForDisplay(entry.TimeToFirstAudio);
                row.Synth = TimingText.ForDisplay(entry.SynthesisDuration);
            }
        });

    private void AddRow(ActivityEntry entry)
    {
        var row = new ActivityRowVm(
            entry.Id,
            entry.Timestamp.ToString("HH:mm:ss"),
            entry.Trigger.ToString(),
            WindowSourceText.ForDisplay(entry.Window),
            entry.Text,
            ActivityStateText.ForDisplay(entry.State),
            ActivityReasonText.ForDisplay(entry.Reason))
        {
            FirstAudio = TimingText.ForDisplay(entry.TimeToFirstAudio),
            Synth = TimingText.ForDisplay(entry.SynthesisDuration),
        };

        Rows.Add(row);
        _byId[entry.Id] = row;

        while (Rows.Count > MaxRows)
        {
            _byId.Remove(Rows[0].Id);
            Rows.RemoveAt(0);
        }

        LogList.ScrollIntoView(row);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        Rows.Clear();
        _byId.Clear();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _log.EntryAdded -= OnEntryAdded;
        _log.EntryChanged -= OnEntryChanged;
    }
}
