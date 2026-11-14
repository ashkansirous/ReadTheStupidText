using System;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ReadTheStupidText_App;

/// <summary>
/// The panel's side of the forced update (Slice 217, mock frames W3 and W4). Required: the status line reads "Reading paused", an "Update needed"
/// card with an "Open Microsoft Store" button sits at the top of the body, and everything that reads (transport, toggles, Read a file) is grey and
/// unclickable. Available: a quiet "A new version is ready" bar with "Get it" and a close cross, shown only because the panel is open; it never opens
/// the panel and reading is normal.
/// </summary>
public sealed partial class ControlPanelWindow
{
    private const double ReadingControlsDimmedOpacity = 0.35;
    private const double CardRadius = 8;
    private const double ButtonRadius = 5;
    private const double RequiredIconSize = 32;
    private const string DownloadGlyph = "";
    private const string CloseGlyph = "";

    private static readonly Color Accent = Color.FromArgb(0xFF, 0x3B, 0x6F, 0xE3);
    private static readonly Color AccentDark = Color.FromArgb(0xFF, 0x5B, 0x8D, 0xF0);

    private WindowsUpdate? _update;

    // Runs in the constructor, after the XAML is built.
    private void InitUpdateNotice(WindowsUpdate update)
    {
        _update = update;
        _update.Changed += (_, _) => RefreshUpdateNotice();
        RootGrid.ActualThemeChanged += (_, _) => RebuildUpdateNotice();
        RefreshUpdateNotice();
    }

    // Set only when the required state flips, so a theme change or a dismissal never overwrites the status line
    // or re-enables a control that something else (a pack download, an error) disabled.
    private bool? _appliedRequired;

    /// <summary>Opens the panel if it is hidden (a refused read reopens it on the update notice); an open panel is left alone.</summary>
    public void ShowForUpdate()
    {
        if (!AppWindow.IsVisible)
        {
            ShowPanel();
        }
    }

    private bool UpdateRequired => _update?.Required == true;

    private void RefreshUpdateNotice()
    {
        if (_update is null)
        {
            return;
        }

        RebuildUpdateNotice();
        bool required = _update.Required;
        if (_appliedRequired == required)
        {
            return;
        }

        _appliedRequired = required;
        SetReadingControlsEnabled(!required);
        TextFlow.ForUi(StatusText, DescribeStatus(_readAloud.State));
    }

    // Redraws only the card or bar (it follows the light/dark theme); touches nothing else in the panel.
    private void RebuildUpdateNotice()
    {
        if (_update is null)
        {
            return;
        }

        UpdateNoticeHost.Children.Clear();
        if (_update.Required)
        {
            UpdateNoticeHost.Children.Add(RequiredCard());
        }
        else if (_update.ShowAvailableBar)
        {
            UpdateNoticeHost.Children.Add(AvailableBar());
        }

        if (RootGrid.IsLoaded)
        {
            RefitToContent();
        }
    }

    // The transport row, the auto-read and other toggles and Read a file all start or steer a read: grey while an update is required.
    private void SetReadingControlsEnabled(bool enabled)
    {
        double opacity = enabled ? 1 : ReadingControlsDimmedOpacity;
        foreach (Control control in new Control[]
        {
            SkipBackButton, PlayPauseButton, SkipForwardButton, UploadFileButton,
            AutoReadSelectionToggle, AutoReadCopyToggle, StartupToggle, ReadingTextBoxToggle, TranslateToggle,
        })
        {
            control.IsEnabled = enabled;
            control.Opacity = opacity;
        }
    }

    private bool IsDark => RootGrid.ActualTheme == ElementTheme.Dark;

    private Brush Text1 => Solid(IsDark ? Color.FromArgb(0xEB, 255, 255, 255) : Color.FromArgb(0xE3, 0, 0, 0));

    private Brush Text2 => Solid(IsDark ? Color.FromArgb(0x94, 255, 255, 255) : Color.FromArgb(0x8F, 0, 0, 0));

    private Color AccentColor => IsDark ? AccentDark : Accent;

    private static SolidColorBrush Solid(Color color) => new(color);

    private static FontFamily SymbolFont => (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"];

    // W3: a 32 px blue circle with the white arrow, the title, the text and the store button.
    private Border RequiredCard()
    {
        Border circle = RequiredIcon();
        StackPanel column = RequiredColumn();
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(column, 1);
        row.Children.Add(circle);
        row.Children.Add(column);
        return new Border
        {
            Name = "UpdateRequiredCard",
            Padding = new Thickness(14),
            Margin = new Thickness(2, 4, 2, 6),
            CornerRadius = new CornerRadius(CardRadius),
            Background = Solid(IsDark ? Color.FromArgb(0x0B, 255, 255, 255) : Color.FromArgb(0x99, 255, 255, 255)),
            Child = row,
        };
    }

    private Border RequiredIcon() => new()
    {
        Width = RequiredIconSize,
        Height = RequiredIconSize,
        CornerRadius = new CornerRadius(RequiredIconSize / 2),
        Background = Solid(Accent),
        VerticalAlignment = VerticalAlignment.Top,
        Child = new FontIcon { FontFamily = SymbolFont, Glyph = DownloadGlyph, FontSize = 16, Foreground = Solid(Colors.White) },
    };

    private StackPanel RequiredColumn()
    {
        var title = new TextBlock { Text = UiStrings.Get("Update_Title"), FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Text1 };
        var body = new TextBlock { Text = UiStrings.Get("Update_Body"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Foreground = Text2 };
        TextFlow.ForUi(title, title.Text);
        TextFlow.ForUi(body, body.Text);
        var texts = new StackPanel { Spacing = 4, Children = { title, body } };
        return new StackPanel { Spacing = 10, Children = { texts, StoreButton() } };
    }

    private Button StoreButton()
    {
        var button = new Button
        {
            Content = UiStrings.Get("Update_OpenStore"),
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(ButtonRadius),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Solid(Colors.White),
            Background = Solid(Accent),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        KeepFill(button, Accent, Colors.White);
        button.Click += async (_, _) => await WindowsUpdate.OpenStoreAsync();
        return button;
    }

    // The default button template changes fill and text on hover and press; the update buttons keep their one fill.
    private static void KeepFill(Button button, Color fill, Color text)
    {
        foreach (string state in new[] { string.Empty, "PointerOver", "Pressed" })
        {
            button.Resources["ButtonBackground" + state] = Solid(fill);
            button.Resources["ButtonForeground" + state] = Solid(text);
            button.Resources["ButtonBorderBrush" + state] = Solid(Colors.Transparent);
        }
    }

    // W4: a bar with the arrow, "A new version is ready", "Get it" and a close cross.
    private Border AvailableBar()
    {
        return new Border
        {
            Name = "UpdateAvailableBar",
            Padding = new Thickness(12, 6, 6, 6),
            Margin = new Thickness(2, 4, 2, 6),
            CornerRadius = new CornerRadius(CardRadius),
            Background = Solid(Color.FromArgb(0x14, 0x3B, 0x6F, 0xE3)),
            BorderBrush = Solid(Color.FromArgb(0x38, 0x3B, 0x6F, 0xE3)),
            BorderThickness = new Thickness(1),
            Child = AvailableRow(),
        };
    }

    private Grid AvailableRow()
    {
        var arrow = new FontIcon { FontFamily = SymbolFont, Glyph = DownloadGlyph, FontSize = 16, Foreground = Solid(AccentColor), VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Text1 };
        TextFlow.ForUi(label, UiStrings.Get("Update_Available"));
        Button get = BarButton(UiStrings.Get("Update_Get"), 12.5);
        get.Click += async (_, _) => await WindowsUpdate.OpenStoreAsync();
        Button close = CloseBarButton();
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(label, 1);
        Grid.SetColumn(get, 2);
        Grid.SetColumn(close, 3);
        row.Children.Add(arrow);
        row.Children.Add(label);
        row.Children.Add(get);
        row.Children.Add(close);
        return row;
    }

    private Button CloseBarButton()
    {
        Button close = BarButton(string.Empty, 12);
        close.Content = new FontIcon { FontFamily = SymbolFont, Glyph = CloseGlyph, FontSize = 12, Foreground = Text2 };
        close.Width = 28;
        close.Padding = new Thickness(0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, UiStrings.Get("Update_Dismiss"));
        close.Click += (_, _) => _update?.DismissAvailable();
        return close;
    }

    private Button BarButton(string text, double size)
    {
        Color fill = IsDark ? Color.FromArgb(0x14, 255, 255, 255) : Color.FromArgb(0xB8, 255, 255, 255);
        var button = new Button
        {
            Content = text,
            Height = 28,
            Padding = new Thickness(12, 0, 12, 0),
            CornerRadius = new CornerRadius(ButtonRadius),
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            Foreground = Text1,
            Background = Solid(fill),
            BorderThickness = new Thickness(0),
        };
        KeepFill(button, fill, ((SolidColorBrush)Text1).Color);
        return button;
    }
}
