using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class LineReflowTests
{
    private static ReflowLine Line(string text, float top, float height) =>
        new(text, new OcrBox(0, top, 100, top + height));

    [Fact]
    public void Ebooks_page_reads_the_heading_and_each_bullet_as_its_own_unit()
    {
        const string text = "Where to Find and Buy Ebooks\n" +
            "• Major Retailers: You can buy ebooks from large stores such as Amazon, Kobo and Apple Books.\n" +
            "• Libraries: Borrow ebooks for free with a library card through apps such as Libby and Hoopla.";

        IReadOnlyList<ReflowUnit> units = LineReflow.FromPlainText(text);

        Assert.Equal(3, units.Count);
        Assert.Equal(new ReflowUnit("Where to Find and Buy Ebooks", UnitKind.Heading), units[0]);
        Assert.Equal(UnitKind.ListItem, units[1].Kind);
        Assert.StartsWith("• Major Retailers:", units[1].Text);
        Assert.StartsWith("• Libraries:", units[2].Text);
    }

    [Fact]
    public void Bright_stadium_page_flags_the_larger_title_as_a_heading_by_geometry()
    {
        var lines = new List<ReflowLine>
        {
            Line("BRIGHT STADIUM", 0, 40),
            Line("Now they have a good view, but", 80, 20),
            Line("Carrot wants to get even closer.", 105, 20),
        };

        IReadOnlyList<ReflowUnit> units = LineReflow.Reflow(lines);

        Assert.Equal(2, units.Count);
        Assert.Equal(new ReflowUnit("BRIGHT STADIUM", UnitKind.Heading), units[0]);
        Assert.Equal("Now they have a good view, but Carrot wants to get even closer.", units[1].Text);
    }

    [Fact]
    public void Geometry_flags_a_line_followed_by_a_large_gap_as_a_heading()
    {
        var lines = new List<ReflowLine>
        {
            Line("Chapter One", 0, 20),
            Line("It was a dark and stormy night, and", 60, 20),
            Line("the rain fell in torrents all night.", 85, 20),
            Line("Nobody slept until the sun rose again.", 110, 20),
        };

        IReadOnlyList<ReflowUnit> units = LineReflow.Reflow(lines);

        Assert.Equal(UnitKind.Heading, units[0].Kind);
        Assert.Equal(2, units.Count);
    }

    [Fact]
    public void Hyphen_wrapped_words_are_rejoined_and_compounds_keep_their_hyphen()
    {
        IReadOnlyList<ReflowUnit> units = LineReflow.FromPlainText(
            "They played foot-\nball with the Anglo-\nSaxon children all day long, and then they\nwent home.");

        Assert.Single(units);
        Assert.Equal("They played football with the Anglo-Saxon children all day long, and then they went home.", units[0].Text);
    }

    [Fact]
    public void A_poem_with_terminal_punctuation_is_not_glued_into_headings()
    {
        IReadOnlyList<ReflowUnit> units = LineReflow.FromPlainText(
            "Roses are red.\nViolets are blue.\nSugar is sweet.\nAnd so are you.");

        Assert.Single(units);
        Assert.Equal(UnitKind.Paragraph, units[0].Kind);
    }

    [Fact]
    public void A_short_last_line_ending_in_a_full_stop_is_not_a_heading()
    {
        IReadOnlyList<ReflowUnit> units = LineReflow.FromPlainText(
            "The quick brown fox jumped over the lazy dog and\nkept running through the forest until it was\nDone.\n\nThe next paragraph begins here.");

        Assert.Equal(2, units.Count);
        Assert.All(units, u => Assert.Equal(UnitKind.Paragraph, u.Kind));
        Assert.EndsWith("Done.", units[0].Text);
    }

    [Fact]
    public void Blank_lines_are_paragraph_breaks()
    {
        IReadOnlyList<ReflowUnit> units = LineReflow.FromPlainText("One line\ncontinues here.\n\nSecond paragraph.");

        Assert.Equal(["One line continues here.", "Second paragraph."], units.Select(u => u.Text));
    }

    [Fact]
    public void BreakMap_marks_only_real_breaks()
    {
        const string text = "It rained all\nnight long.\nNext sentence.\n\nNew paragraph.";

        LineBreakMap map = LineReflow.BreakMap(text);

        Assert.DoesNotContain(text.IndexOf('\n'), map.SentenceBreaks);
        Assert.Contains(text.IndexOf("\n\n", StringComparison.Ordinal), map.SentenceBreaks);
    }

    [Fact]
    public void Ocr_reading_reflows_with_its_boxes()
    {
        var reading = new OcrReading(
            [new OcrLine("Now they have a good view, but", new OcrBox(0, 0, 100, 20)),
             new OcrLine("Carrot wants to get closer.", new OcrBox(0, 25, 100, 45))],
            200, 200);

        Assert.Equal("Now they have a good view, but Carrot wants to get closer.", reading.ReflowedText());
    }

    [Fact]
    public void A_tall_box_on_a_mid_sentence_wrap_is_not_a_heading()
    {
        var lines = new List<ReflowLine>
        {
            Line("The knight rode out (quickly, with a jolt) and", 0, 20),
            Line("jumped over the wall at dawn", 22, 26),
            Line("then he went home.", 50, 20),
        };

        Assert.Single(LineReflow.Reflow(lines));
    }

    [Fact]
    public void A_small_gap_difference_is_not_a_heading_gap()
    {
        var lines = new List<ReflowLine>
        {
            Line("The knight rode out and", 0, 20),
            Line("jumped over the wall at", 21, 20),
            Line("dawn then went home", 44, 20),
            Line("again in the evening.", 65, 20),
        };

        Assert.Single(LineReflow.Reflow(lines));
    }

    [Fact]
    public void Hyphen_rule_drops_before_lowercase_and_keeps_before_uppercase()
    {
        Assert.Equal("a football fan", LineReflow.ReflowText("a foot-\nball fan"));
        Assert.Equal("an Anglo-Saxon fan", LineReflow.ReflowText("an Anglo-\nSaxon fan"));
    }

    [Fact]
    public void A_wrapped_proper_noun_line_is_not_a_heading()
    {
        IReadOnlyList<ReflowUnit> units = LineReflow.FromPlainText(
            "The old man handed the keys to\nMary Jane\nWilson who said thank you.");

        Assert.Single(units);
    }

    [Fact]
    public void A_wrapped_numeral_or_dash_is_not_a_list_item()
    {
        Assert.Single(LineReflow.FromPlainText("It is explained on page\n10. Then we left."));
        Assert.Single(LineReflow.FromPlainText("He stopped and waited\n- and then he left."));
    }

    [Fact]
    public void A_dash_item_after_a_finished_sentence_is_a_list_item()
    {
        IReadOnlyList<ReflowUnit> units = LineReflow.FromPlainText("Buy these.\n- Milk\n- Eggs");

        Assert.Equal(3, units.Count);
    }

    [Fact]
    public void Two_consecutive_headings_are_separate_sentences_with_no_heading_break_between_them()
    {
        const string text = "THE GREAT STORY\nA Tale Of Two Cats\nOnce upon a time there were two cats.\nThey lived in a house.";
        var boxes = new OcrBox?[]
        {
            new OcrBox(0, 0, 100, 40), new OcrBox(0, 45, 100, 85),
            new OcrBox(0, 130, 100, 150), new OcrBox(0, 155, 100, 175),
        };

        IReadOnlyList<ReflowUnit> units = LineReflow.Reflow(
            text.Split('\n').Select((l, n) => new ReflowLine(l, boxes[n])).ToList());
        LineBreakMap map = LineReflow.BreakMap(text, boxes);

        Assert.Equal(new ReflowUnit("THE GREAT STORY", UnitKind.Heading), units[0]);
        Assert.Equal(new ReflowUnit("A Tale Of Two Cats", UnitKind.Heading), units[1]);
        Assert.Contains(text.IndexOf('\n'), map.SentenceBreaks);
        Assert.DoesNotContain(text.IndexOf('\n'), map.HeadingBreaks);
        Assert.Contains(text.IndexOf('\n', text.IndexOf('\n') + 1), map.HeadingBreaks);
    }

    [Fact]
    public void JoinUnits_separates_units_with_a_blank_line()
    {
        string joined = LineReflow.JoinUnits([new ReflowUnit("A", UnitKind.Heading), new ReflowUnit("B", UnitKind.Paragraph)]);

        Assert.Equal("A\n\nB", joined);
    }

    [Fact]
    public void A_long_unblanked_text_is_analysed_quickly()
    {
        string text = string.Join("\n", Enumerable.Repeat("a wrapped line of ordinary text that goes on", 20000));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        LineBreakMap map = LineReflow.BreakMap(text);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
        Assert.Empty(map.SentenceBreaks);
    }

    [Fact]
    public void MedianWithout_leaves_one_value_out()
    {
        Assert.Equal(3, LineStats.MedianWithout([1, 3, 3, 9], 3));
        Assert.Equal(7, LineStats.MedianWithout([1, 5, 9], 1));
        Assert.Equal(1.5, LineStats.MedianWithout([1, 2, 100], 100));
    }

    [Fact]
    public void BreakMap_ignores_boxes_that_do_not_match_the_line_count()
    {
        var boxes = new OcrBox?[] { new OcrBox(0, 0, 10, 10) };

        LineBreakMap plain = LineReflow.BreakMap("one\n\ntwo");
        LineBreakMap mismatched = LineReflow.BreakMap("one\n\ntwo", boxes);

        Assert.True(plain.SentenceBreaks.SetEquals(mismatched.SentenceBreaks));
        Assert.True(plain.HeadingBreaks.SetEquals(mismatched.HeadingBreaks));
    }

    [Fact]
    public void Dialogue_dashes_are_not_list_items()
    {
        Assert.False(LineClass.IsListItem("— Hello there"));
        Assert.False(LineClass.IsListItem("– Hello there"));
        Assert.Single(LineReflow.FromPlainText("He left.\n— Hello there, he said."));
        Assert.True(LineClass.IsListItem("- Milk"));
    }

    [Fact]
    public void An_unpunctuated_intro_does_not_swallow_the_first_list_item()
    {
        IReadOnlyList<ReflowUnit> units = LineReflow.FromPlainText("Buy these\n- milk\n- eggs\n- bread");

        Assert.Equal(4, units.Count);
        Assert.Equal("Buy these", units[0].Text);
    }

    [Fact]
    public void The_break_map_marks_dropped_hyphen_wraps()
    {
        const string text = "a foot-\nball fan and pre-\n123 too";

        LineBreakMap map = LineReflow.BreakMap(text);

        Assert.Equal([text.IndexOf('\n')], map.DroppedHyphenBreaks.ToArray());
    }

    [Fact]
    public void Geometry_does_not_make_a_lowercase_or_long_line_a_heading()
    {
        var lines = new List<ReflowLine>
        {
            Line("and then the rain fell", 0, 40),
            Line("It was a dark and stormy night, and the rain fell in torrents all night long.", 60, 20),
            Line("Nobody slept until the sun rose again, and the morning was calm.", 85, 20),
        };

        Assert.DoesNotContain(LineReflow.Reflow(lines), u => u.Kind == UnitKind.Heading);
    }

    [Theory]
    [InlineData(LineClass.MaxHeadingLength, UnitKind.Heading)]
    [InlineData(LineClass.MaxHeadingLength + 1, UnitKind.Paragraph)]
    public void A_tall_spaced_line_is_a_heading_only_up_to_the_length_cutoff(int length, UnitKind expected)
    {
        var lines = new List<ReflowLine>
        {
            Line(new string('A', length), 0, 40),
            Line("Now they have a good view, but", 80, 20),
            Line("Carrot wants to get even closer.", 105, 20),
        };

        Assert.Equal(expected, LineReflow.Reflow(lines)[0].Kind);
    }
}
