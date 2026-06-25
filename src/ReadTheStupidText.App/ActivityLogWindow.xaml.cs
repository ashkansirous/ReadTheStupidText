using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Domain.Activity;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Graphics;

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
        Title = "ReadTheStupidText — Activity log";
        AppWindow.Resize(new SizeInt32(640, 480));

        foreach (ActivityEntry entry in _log.Entries)
        {
            AddRow(entry);
        }

        _log.EntryAdded += OnEntryAdded;
        _log.EntryChanged += OnEntryChanged;
        Closed += OnClosed;
    }

    public ObservableCollection<ActivityRowVm> Rows { get; } = new();

    private void OnEntryAdded(object? sender, ActivityEntry entry) =>
        _dispatcher.TryEnqueue(() => AddRow(entry));

    private void OnEntryChanged(object? sender, ActivityEntry entry) =>
        _dispatcher.TryEnqueue(() =>
        {
            if (_byId.TryGetValue(entry.Id, out ActivityRowVm? row))
            {
                row.State = entry.State.ToString();
            }
        });

    private void AddRow(ActivityEntry entry)
    {
        var row = new ActivityRowVm(
            entry.Id,
            entry.Timestamp.ToString("HH:mm:ss"),
            entry.Source.ToString(),
            entry.Text,
            entry.State.ToString());

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
