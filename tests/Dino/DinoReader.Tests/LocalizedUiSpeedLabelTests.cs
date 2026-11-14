using System.Globalization;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 340: the slider's slow-end label names the real minimum speed in every language, so it cannot drift from <see cref="ReadingSpeed.Min"/>.</summary>
public partial class LocalizedUiTests
{
    private const string SlowEndKey = "Speed_EndSlow";

    [Fact]
    public void The_slow_end_label_of_every_language_names_the_minimum_speed()
    {
        var files = LanguageFiles().Append(NeutralFile);

        foreach (string file in files)
        {
            string label = Load(file)[SlowEndKey];
            Assert.True(
                ReadingSpeed.Min == LabelNumber(label),
                $"{file}: {SlowEndKey} \"{label}\" does not name {ReadingSpeed.Min}x");
        }
    }

    private static double LabelNumber(string label)
    {
        string number = new(label.TakeWhile(c => c != 'x').Select(LatinDigitOrPoint).ToArray());
        return double.Parse(number, CultureInfo.InvariantCulture);
    }

    private static char LatinDigitOrPoint(char c) => c switch
    {
        ',' or '٫' => '.',
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
        _ => c,
    };
}
