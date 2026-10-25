using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Xunit;

namespace DinoReader.Tests;

public class AnimalSoundTests
{
    private const string Sheep = "Daddy Sheep";

    private static SplitSegment Quote(string text, int start, string? who = Sheep) =>
        new(new Segment(SegmentKind.Quote, text, start, start + text.Length, who), start);

    private static SplitSegment Narration(string text, int start) =>
        new(new Segment(SegmentKind.Narration, text, start, start + text.Length), start);

    private static IReadOnlyList<SplitSegment> Lines(int count, string who = Sheep)
    {
        var pieces = new List<SplitSegment>();
        for (int i = 0; i < count; i++)
        {
            pieces.Add(Narration("The farm woke up.", i * 100));
            pieces.Add(Quote("Good morning, everyone.", i * 100 + 50, who));
        }

        return pieces;
    }

    private static IReadOnlyDictionary<string, AnimalKind> SheepOnly => new Dictionary<string, AnimalKind> { [Sheep] = AnimalKind.Sheep };

    [Theory]
    [InlineData("Daddy Sheep", AnimalKind.Sheep)]
    [InlineData("Little Lamb", AnimalKind.Sheep)]
    [InlineData("Goat", AnimalKind.Goat)]
    [InlineData("Mrs Cow", AnimalKind.Cow)]
    [InlineData("Tom the Cat", AnimalKind.Cat)]
    [InlineData("Rex the dog", AnimalKind.Dog)]
    [InlineData("Duck", AnimalKind.Duck)]
    [InlineData("Piglet", AnimalKind.Pig)]
    [InlineData("Old Hen", AnimalKind.Hen)]
    [InlineData("Rooster", AnimalKind.Rooster)]
    [InlineData("Wise Owl", AnimalKind.Owl)]
    [InlineData("Horse", AnimalKind.Horse)]
    [InlineData("Frog", AnimalKind.Frog)]
    [InlineData("Mouse", AnimalKind.Mouse)]
    [InlineData("Lion", AnimalKind.Lion)]
    [InlineData("Bee", AnimalKind.Bee)]
    [InlineData("Wolf", AnimalKind.Wolf)]
    [InlineData("Bird", AnimalKind.Bird)]
    public void English_names_find_their_animal(string name, AnimalKind kind) =>
        Assert.Equal(kind, AnimalSounds.KindOf(name, "en"));

    [Theory]
    [InlineData("Anna")]
    [InlineData("Mr Bear")]
    [InlineData("Sheridan")]
    [InlineData("Cowan")]
    [InlineData("Billy")]
    [InlineData("Robin")]
    [InlineData("Hello Kitty")]
    [InlineData("Captain Sparrow")]
    [InlineData("Drake")]
    public void Names_without_an_animal_word_find_none(string name) => Assert.Null(AnimalSounds.KindOf(name, "en"));

    [Theory]
    [InlineData("猫", "zh", AnimalKind.Cat)]
    [InlineData("小猫", "zh", AnimalKind.Cat)]
    [InlineData("ひつじさん", "ja", AnimalKind.Sheep)]
    [InlineData("ねこ", "ja", AnimalKind.Cat)]
    public void Chinese_and_Japanese_names_match_whole_or_with_a_curated_title(string name, string language, AnimalKind kind) =>
        Assert.Equal(kind, AnimalSounds.KindOf(name, language));

    [Theory]
    [InlineData("ハチ", "ja")]
    [InlineData("たけとり", "ja")]
    [InlineData("ことりこ", "ja")]
    [InlineData("马克", "zh")]
    [InlineData("王小猫头", "zh")]
    public void Chinese_and_Japanese_names_that_only_contain_an_animal_word_find_none(string name, string language) =>
        Assert.Null(AnimalSounds.KindOf(name, language));

    [Theory]
    [InlineData("ワンピースを着た", AnimalKind.Dog, "ja", false)]
    [InlineData("ガーデンへ行く", AnimalKind.Duck, "ja", false)]
    [InlineData("ワン! 元気だよ", AnimalKind.Dog, "ja", true)]
    [InlineData("ワンワン、こんにちは", AnimalKind.Dog, "ja", true)]
    [InlineData("汪汪, 你好", AnimalKind.Dog, "zh", true)]
    public void SaysIt_does_not_find_a_short_sound_inside_an_ordinary_japanese_or_chinese_word(string text, AnimalKind kind, string language, bool says) =>
        Assert.Equal(says, AnimalSounds.SaysIt(text, kind, language));

    private static IReadOnlyList<SentenceSpan> SpansOf(params (int Start, int End)[] spans) => spans.Select(s => new SentenceSpan(s.Start, s.End)).ToList();

    [Fact]
    public void A_translated_line_gets_its_sound_once_after_its_last_piece()
    {
        // One source quote (0..40) translated into two spoken pieces that keep the same story positions.
        var pieces = new List<SplitSegment> { Quote("Good morning.", 0) with { Segment = Quote("Good morning.", 0).Segment with { SourceEnd = 40 } } };
        pieces.Add(pieces[0] with { Segment = pieces[0].Segment with { Text = "Everyone is here." } });
        pieces.Add(Narration("Then it was quiet.", 60));
        var plan = new Dictionary<int, AnimalKind> { [40] = AnimalKind.Sheep };
        IReadOnlyList<AnimalSounds.Placement> placed = AnimalSounds.Placements(pieces, plan, SpansOf((0, 40), (60, 78)));
        AnimalSounds.Placement only = Assert.Single(placed);
        Assert.Equal(1, only.After);
        Assert.Equal(1, only.Quote);
    }

    [Fact]
    public void The_sound_waits_for_the_dialogue_tag_of_the_quotes_sentence()
    {
        // "Good morning!" said Daddy Sheep.  The farm woke up.
        var pieces = new List<SplitSegment>
        {
            Quote("Good morning!", 0) with { Segment = Quote("Good morning!", 0).Segment with { SourceEnd = 15 } },
            Narration("said Daddy Sheep.", 16),
            Narration("The farm woke up.", 35),
        };
        var plan = new Dictionary<int, AnimalKind> { [15] = AnimalKind.Sheep };
        AnimalSounds.Placement placed = Assert.Single(AnimalSounds.Placements(pieces, plan, SpansOf((0, 33), (35, 52))));
        Assert.Equal(1, placed.After);
        Assert.Equal(0, placed.Quote);
    }

    [Fact]
    public void A_line_with_no_tag_gets_its_sound_right_after_it()
    {
        var pieces = new List<SplitSegment> { Quote("Good morning!", 0), Narration("The farm woke up.", 20) };
        var plan = new Dictionary<int, AnimalKind> { [13] = AnimalKind.Sheep };
        AnimalSounds.Placement placed = Assert.Single(AnimalSounds.Placements(pieces, plan, SpansOf((0, 13), (20, 37))));
        Assert.Equal(0, placed.After);
    }

    [Fact]
    public void An_animal_says_its_sound_after_its_2nd_5th_and_7th_line()
    {
        IReadOnlyList<SplitSegment> pieces = Lines(8);
        IReadOnlyDictionary<int, AnimalKind> plan = AnimalSounds.Plan(pieces, SheepOnly, "en");
        int[] expected = new[] { 1, 4, 6 }.Select(line => pieces[line * 2 + 1].Segment.SourceEnd).ToArray();
        Assert.Equal(expected, plan.Keys.OrderBy(k => k).ToArray());
        Assert.All(plan.Values, kind => Assert.Equal(AnimalKind.Sheep, kind));
    }

    [Fact]
    public void The_plan_is_the_same_every_time() =>
        Assert.Equal(
            AnimalSounds.Plan(Lines(9), SheepOnly, "en").Keys.OrderBy(k => k),
            AnimalSounds.Plan(Lines(9), SheepOnly, "en").Keys.OrderBy(k => k));

    [Fact]
    public void A_line_that_already_says_the_sound_gets_none_and_counts_as_its_sound()
    {
        var pieces = new List<SplitSegment>(Lines(1)) { Narration("It bleated.", 200), Quote("Baaaa! I am hungry.", 250) };
        pieces.AddRange(Lines(3).Select(p => p with { TextStart = p.TextStart + 1000, Segment = p.Segment with { SourceStart = p.Segment.SourceStart + 1000, SourceEnd = p.Segment.SourceEnd + 1000 } }));
        IReadOnlyDictionary<int, AnimalKind> plan = AnimalSounds.Plan(pieces, SheepOnly, "en");
        int saidEnd = 250 + "Baaaa! I am hungry.".Length;
        Assert.DoesNotContain(saidEnd, plan.Keys);
        Assert.Equal(new[] { 1250 + "Good morning, everyone.".Length }, plan.Keys.ToArray());
    }

    [Fact]
    public void Narration_and_other_characters_get_no_sound()
    {
        IReadOnlyList<SplitSegment> others = Lines(6, "Anna");
        Assert.Empty(AnimalSounds.Plan(others, SheepOnly, "en"));
        var narration = Enumerable.Range(0, 6).Select(i => Narration("Baa went the sheep.", i * 40)).ToList();
        Assert.Empty(AnimalSounds.Plan(narration, SheepOnly, "en"));
    }

    [Fact]
    public void The_sentences_of_one_quote_are_one_line()
    {
        var pieces = new List<SplitSegment> { Quote("Hello there.", 0), Quote("How are you?", 13), Narration("He smiled.", 30), Quote("Fine.", 45) };
        IReadOnlyDictionary<int, AnimalKind> plan = AnimalSounds.Plan(pieces, SheepOnly, "en");
        Assert.Equal(new[] { 45 + "Fine.".Length }, plan.Keys.ToArray());
    }

    [Theory]
    [InlineData("Baa!", true)]
    [InlineData("Baaaaa, said the sheep", true)]
    [InlineData("Bah, humbug!", false)]
    [InlineData("Good morning", false)]
    [InlineData("A bar of soap", false)]
    public void SaysIt_sees_a_stretched_sound_word(string text, bool says) =>
        Assert.Equal(says, AnimalSounds.SaysIt(text, AnimalKind.Sheep, "en"));

    [Fact]
    public void An_everyday_meh_is_not_a_bleat_but_a_stretched_one_is()
    {
        Assert.False(AnimalSounds.SaysIt("Meh, whatever", AnimalKind.Goat, "en"));
        Assert.True(AnimalSounds.SaysIt("Meeeh!", AnimalKind.Goat, "en"));
    }

    [Theory]
    [InlineData("Лев", "ru")]
    [InlineData("El león", "es")]
    [InlineData("O leão", "pt")]
    [InlineData("الأسد", "ar")]
    public void The_common_word_for_a_lion_finds_the_lion(string name, string language) =>
        Assert.Equal(AnimalKind.Lion, AnimalSounds.KindOf(name, language));

    [Fact]
    public void A_word_that_only_shrinks_to_the_sound_is_not_the_sound() =>
        Assert.False(AnimalSounds.SaysIt("A bright moon", AnimalKind.Cow, "en"));

    [Theory]
    [InlineData("fr", "Bêê !")]
    [InlineData("sv", "Bää!")]
    [InlineData("nb", "Bæ!")]
    [InlineData("fa", "بع بع!")]
    public void The_sound_is_written_for_the_language(string language, string sound) =>
        Assert.Equal(sound, AnimalSounds.SoundOf(AnimalKind.Sheep, language));

    [Fact]
    public void Every_language_has_every_animal_with_a_sound_and_a_name()
    {
        foreach (string code in RuleSets.Codes)
        {
            foreach (AnimalKind kind in Enum.GetValues<AnimalKind>())
            {
                Assert.False(string.IsNullOrWhiteSpace(AnimalSounds.SoundOf(kind, code)), $"{code} {kind} has no sound");
                Assert.NotEmpty(AnimalSoundWords.For(code).Entries.First(e => e.Kind == kind).Names);
            }
        }
    }

    [Fact]
    public void Every_sound_counts_as_said_in_its_own_language()
    {
        foreach (string code in RuleSets.Codes)
        {
            foreach (AnimalKind kind in Enum.GetValues<AnimalKind>())
            {
                string sound = AnimalSounds.SoundOf(kind, code)!;
                Assert.True(AnimalSounds.SaysIt(sound, kind, code), $"{code} {kind} '{sound}' is not seen as said");
            }
        }
    }

    [Fact]
    public void Every_animals_first_name_finds_that_animal_in_every_language()
    {
        foreach (string code in RuleSets.Codes)
        {
            foreach (AnimalSoundWords.Entry entry in AnimalSoundWords.For(code).Entries)
            {
                Assert.Equal(entry.Kind, AnimalSounds.KindOf(entry.Names[0], code));
            }
        }
    }

    [Fact]
    public void A_grown_up_cow_sounds_deeper_than_a_calf_and_a_calf_squeaks()
    {
        Assert.Equal(VoiceFlavour.Bleat, AnimalSounds.FlavourFor(AnimalKind.Sheep, VoiceFlavour.Normal));
        Assert.Equal(VoiceFlavour.BleatLamb, AnimalSounds.FlavourFor(AnimalKind.Goat, VoiceFlavour.Small));
        Assert.Equal(VoiceFlavour.Big, AnimalSounds.FlavourFor(AnimalKind.Cow, VoiceFlavour.Normal));
        Assert.Equal(VoiceFlavour.Giant, AnimalSounds.FlavourFor(AnimalKind.Cow, VoiceFlavour.Big));
        Assert.Equal(VoiceFlavour.Squeaky, AnimalSounds.FlavourFor(AnimalKind.Cow, VoiceFlavour.Small));
        Assert.Equal(VoiceFlavour.Normal, AnimalSounds.FlavourFor(AnimalKind.Cat, VoiceFlavour.Normal));
    }

    [Fact]
    public void The_switch_is_on_by_default_and_survives_a_save_when_off()
    {
        Story story = Story.NewOwnBook(Guid.NewGuid().ToString("N"), "en", DateTimeOffset.UnixEpoch, 0);
        Assert.True(StoryJson.Deserialize(StoryJson.Serialize(story)).AnimalSoundsOn);
        Assert.False(StoryJson.Deserialize(StoryJson.Serialize(story with { AnimalSoundsOn = false })).AnimalSoundsOn);
    }

    [Fact]
    public void TheDraft_CarriesTheAnimalSoundsSwitch()
    {
        Story story = Story.NewOwnBook(Guid.NewGuid().ToString("N"), "en", DateTimeOffset.UnixEpoch, 0);
        var draft = new StoryDraft(story) { AnimalSoundsOn = false };

        Assert.False(draft.Current.AnimalSoundsOn);
        Assert.True(new StoryDraft(story).Current.AnimalSoundsOn);
    }
}
