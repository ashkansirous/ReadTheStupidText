using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Xunit;

namespace DinoReader.Tests;

/// <summary>The book chooses each character's voice, not the app (Slice 156, Decision 161).</summary>
public class AuthoredVoiceTests
{
    private static readonly CharacterTraits SeagullTraits = new(CharacterAge.Adult, null, [VoiceQuality.Squawky, VoiceQuality.Bouncy]);

    public static TheoryData<string> Languages() => new(UiLanguage.Supported);

    private static Character C(string id, string name, Gender gender = Gender.Unknown) => new(id, name, gender, 10);

    private static VoiceCast Cast(
        string language, IReadOnlyList<Character> characters, IReadOnlyDictionary<string, PinnedCharacter>? pinned, Func<Character, AnimalHint?>? animalOf = null)
    {
        IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.For(language);
        string narrator = roster.First(c => c.Kind == CharacterKind.Narrator).VoiceId;
        var choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), narrator);
        return CastCharacters.Cast(characters, choices, roster, narrator, _ => VoiceFlavour.Normal, pinned, animalOf: animalOf);
    }

    private static StorytellerCharacter Played(string language, VoiceCast cast, string characterId) =>
        CharacterRoster.Speakers(language).First(c => c.VoiceId == cast.VoiceFor(characterId) && c.Flavour == cast.Flavours![characterId]);

    private static Dictionary<string, PinnedCharacter> Book(string characterId, string? suggested, CharacterTraits? traits = null, string voice = "", bool edited = false) =>
        new() { [characterId] = new PinnedCharacter(voice, null, edited, suggested, traits) };

    private static Func<Character, AnimalHint?> Bird => _ => new AnimalHint(AnimalKind.Bird, null);

    // ---- the book's voice stands ----

    [Fact]
    public void A_character_gets_the_roster_character_its_book_suggests()
    {
        VoiceCast cast = Cast("en", [C("gull", "Seagull", Gender.Female)], Book("gull", "duckling2", SeagullTraits), Bird);

        Assert.Equal("duckling2", Played("en", cast, "gull").Id);
    }

    [Fact]
    public void A_book_voice_survives_a_recast_and_a_narrator_change()
    {
        var gull = C("gull", "Seagull", Gender.Female);
        VoiceCast first = Cast("en", [gull], Book("gull", "duckling2", SeagullTraits), Bird);
        StorytellerCharacter shown = Played("en", first, "gull");
        var stored = new Dictionary<string, PinnedCharacter> { ["gull"] = new(shown.VoiceId, shown.Flavour, false, "duckling2", SeagullTraits) };
        IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.For("en");
        string otherNarrator = roster.First(c => c.Kind == CharacterKind.Narrator && c.VoiceId != "kokoro:af_heart").VoiceId;
        var choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), otherNarrator);

        VoiceCast again = CastCharacters.Cast([gull], choices, roster, otherNarrator, _ => VoiceFlavour.Normal, stored, animalOf: Bird);

        Assert.Equal("duckling2", Played("en", again, "gull").Id);
    }

    [Fact]
    public void A_grown_ups_pick_beats_the_book()
    {
        StorytellerCharacter owl = CharacterRoster.Speakers("en").First(c => c.Id == "owl");
        var pinned = Book("gull", "duckling2", SeagullTraits, owl.VoiceId, edited: true);
        pinned["gull"] = pinned["gull"] with { Flavour = owl.Flavour };

        VoiceCast cast = Cast("en", [C("gull", "Seagull")], pinned, Bird);

        Assert.Equal("owl", Played("en", cast, "gull").Id);
    }

    [Fact]
    public void A_suggestion_for_the_opposite_gender_is_not_used()
    {
        StorytellerCharacter girl = CharacterRoster.Speakers("en").First(c => c.Id == "duckling2");
        Gender opposite = girl.Gender == Gender.Female ? Gender.Male : Gender.Female;

        VoiceCast cast = Cast("en", [C("x", "Pete", opposite)], Book("x", "duckling2", new CharacterTraits(CharacterAge.Young)));

        Assert.NotEqual("duckling2", Played("en", cast, "x").Id);
        Assert.Equal(opposite, Played("en", cast, "x").Gender);
    }

    // ---- the trait fallback ----

    [Theory]
    [InlineData("nonexistent-id")]
    [InlineData("duckling22")]
    public void A_missing_or_misspelt_suggestion_is_matched_by_traits(string suggestion)
    {
        VoiceCast cast = Cast("en", [C("gull", "Seagull", Gender.Female)], Book("gull", suggestion, SeagullTraits), Bird);

        Assert.Contains(AnimalCharacters.Of(Played("en", cast, "gull").Id)!.Value.Kinds, k => k is AnimalKind.Duck or AnimalKind.Hen or AnimalKind.Owl);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Three_pigs_matched_by_traits_never_sound_alike_in_any_language(string language)
    {
        var pigTraits = new CharacterTraits(CharacterAge.Young, null, [VoiceQuality.Bouncy]);
        var pigs = new[] { C("p1", "Pip", Gender.Male), C("p2", "Pop", Gender.Male), C("p3", "Pup", Gender.Male) };
        var pinned = pigs.ToDictionary(p => p.Id, p => new PinnedCharacter(string.Empty, null, false, "no-such-voice", pigTraits));

        VoiceCast cast = Cast(language, pigs, pinned, _ => new AnimalHint(AnimalKind.Pig, null));

        var sounds = pigs.Select(p => (cast.VoiceFor(p.Id), cast.Flavours![p.Id])).ToList();
        Assert.Equal(3, sounds.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void The_book_of_treasure_opens_in_every_language_with_voices_for_everyone(string language)
    {
        var people = new[] { C("k", "Kathryn", Gender.Female), C("i", "Izzy", Gender.Female), C("p", "Polly", Gender.Female), C("b", "Billy", Gender.Male), C("gull", "Seagull", Gender.Female) };
        var pinned = StarterVoices.For(StoreTexts.TreasureId, people.Select(p => p with { Name = p.Name == "Polly" ? "Pancake Polly" : p.Name }).ToList())
            .ToDictionary(a => a.Key, a => new PinnedCharacter(string.Empty, null, false, a.Value.SuggestedVoice, a.Value.Traits));

        VoiceCast cast = Cast(language, people, pinned, c => c.Id == "gull" ? new AnimalHint(AnimalKind.Bird, null) : null);

        Assert.All(people, p => Assert.False(string.IsNullOrEmpty(cast.VoiceFor(p.Id))));
    }

    [Fact]
    public void The_treasure_books_seagull_and_billy_speak_as_the_duckling_and_the_puppy()
    {
        var people = new[] { C("b", "Billy", Gender.Male), C("gull", "Seagull", Gender.Female) };
        var pinned = StarterVoices.For(StoreTexts.TreasureId, people)
            .ToDictionary(a => a.Key, a => new PinnedCharacter(string.Empty, null, false, a.Value.SuggestedVoice, a.Value.Traits));

        VoiceCast cast = Cast("en", people, pinned, c => c.Id == "gull" ? new AnimalHint(AnimalKind.Bird, null) : null);

        Assert.Equal("duckling2", Played("en", cast, "gull").Id);
        Assert.Equal("puppy2", Played("en", cast, "b").Id);
    }

    // ---- the roster ----

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_roster_character_has_traits(string language)
    {
        Assert.All(CharacterRoster.For(language), c => Assert.True(CharacterTraitMatch.Has(c.Id), $"{language}: {c.Id} has no traits"));
    }

    // ---- the saved cast ----

    [Fact]
    public void The_suggestion_and_traits_are_saved_and_read_back()
    {
        var member = new CastMember("gull", "Seagull", Gender.Female, "kokoro:x", SuggestedVoice: "duckling2", Traits: SeagullTraits);
        Story story = Story.NewOwnBook(Guid.NewGuid().ToString("N"), "en", DateTimeOffset.UnixEpoch, 0) with { Cast = [member] };

        CastMember back = StoryJson.Deserialize(StoryJson.Serialize(story)).Cast.Single();

        Assert.Equal(member, back);
        Assert.Equal(VoiceSource.Book, back.Source);
    }

    [Fact]
    public void A_cast_without_the_new_fields_reads_as_before_and_edited_means_a_grown_up()
    {
        Story story = Story.NewOwnBook(Guid.NewGuid().ToString("N"), "en", DateTimeOffset.UnixEpoch, 0) with
        {
            Cast = [new CastMember("a", "A", Gender.Male, "v"), new CastMember("b", "B", Gender.Male, "v", Edited: true)],
        };
        string json = StoryJson.Serialize(story);

        Story back = StoryJson.Deserialize(json);

        Assert.DoesNotContain("SuggestedVoice", json);
        Assert.Equal([VoiceSource.App, VoiceSource.Grownup], back.Cast.Select(m => m.Source));
    }

    [Fact]
    public void A_trait_this_build_does_not_know_reads_as_absent()
    {
        var member = new CastMember("a", "A", Gender.Male, "v", SuggestedVoice: "owl", Traits: new CharacterTraits(CharacterAge.Old, null, [VoiceQuality.Gruff]));
        string json = StoryJson.Serialize(Story.NewOwnBook(Guid.NewGuid().ToString("N"), "en", DateTimeOffset.UnixEpoch, 0) with { Cast = [member] }).Replace("\"Old\"", "\"Ancient\"").Replace("\"Gruff\"", "\"Fuzzy\"");

        CastMember back = StoryJson.Deserialize(json).Cast.Single();

        Assert.Null(back.Traits);
        Assert.Equal("owl", back.SuggestedVoice);
    }

    // ---- the grown-up's control ----

    [Fact]
    public void Back_to_the_books_voice_makes_the_character_the_books_again()
    {
        var picked = new CastMember("gull", "Seagull", Gender.Female, "v", Edited: true, SuggestedVoice: "duckling2", Traits: SeagullTraits);

        CastMember back = CastEditor.UseBookVoice(new CastEdit([picked]), "gull").Cast.Single();

        Assert.Equal(VoiceSource.Book, back.Source);
    }

    [Fact]
    public void Back_to_the_books_voice_leaves_a_character_the_book_did_not_choose()
    {
        var picked = new CastMember("x", "X", Gender.Female, "v", Edited: true);

        Assert.True(CastEditor.UseBookVoice(new CastEdit([picked]), "x").Cast.Single().Edited);
    }

    [Fact]
    public void A_picked_character_keeps_the_books_suggestion_through_a_recast()
    {
        var characters = new[] { C("gull", "Seagull", Gender.Female) };
        StorytellerCharacter owl = CharacterRoster.Speakers("en").First(c => c.Id == "owl");
        var stored = new[] { new CastMember("gull", "Seagull", Gender.Female, owl.VoiceId, Edited: true, Flavour: owl.Flavour, SuggestedVoice: "duckling2", Traits: SeagullTraits) };
        VoiceCast cast = Cast("en", characters, StoryCast.PinnedCharacters(stored), Bird);

        CastMember member = StoryCast.ToMembers(characters, cast, LanguageRules.English, stored).Single();

        Assert.Equal("duckling2", member.SuggestedVoice);
        Assert.Equal(VoiceSource.Grownup, member.Source);
    }

    // ---- the books ----

    [Fact]
    public void Every_character_of_an_authored_book_has_traits_and_a_known_suggestion_that_no_one_else_in_the_book_shares()
    {
        var known = CharacterRoster.Speakers("en").Select(c => c.Id).ToHashSet();
        foreach (Story book in AuthoredBooks())
        {
            Assert.All(book.Cast, m => Assert.True(m.Traits is not null && m.SuggestedVoice is not null && known.Contains(m.SuggestedVoice), $"{book.Title}: {m.Name} has no valid voice choice"));
            Assert.Equal(book.Cast.Count, book.Cast.Select(m => m.SuggestedVoice).Distinct().Count());
        }
    }

    [Fact]
    public void An_animal_has_no_second_kind_in_its_traits()
    {
        foreach (CastMember member in AuthoredBooks().SelectMany(b => b.Cast).Where(m => m.Animal is not null))
        {
            Assert.Null(member.Traits!.Being);
        }
    }

    [Fact]
    public void A_character_that_is_no_animal_has_a_person_or_fantasy_kind_in_its_traits()
    {
        var missing = AuthoredBooks().SelectMany(b => b.Cast.Where(m => m.Animal is null && m.Traits!.Being is null).Select(m => $"{b.Title}/{m.Name}")).ToList();

        // These are animals that have no AnimalKind (no sound word), so they have no second kind to state either; everyone else must be a person or a fantasy kind.
        string[] animalsWithoutKind = ["Swan", "Robin", "Flopsy", "Seagull", "Hare", "Tortoise", "Squirrel", "Hedgehog"];
        Assert.All(missing, m => Assert.Contains(m[(m.IndexOf('/') + 1)..], animalsWithoutKind));
    }

    [Fact]
    public void A_suggestion_on_the_narrators_voice_is_not_kept_for_a_character()
    {
        IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.For("en");
        StorytellerCharacter onNarrator = roster.First(c => c.Kind == CharacterKind.Speaker && c.Id == "knight");
        string narrator = onNarrator.VoiceId;
        var choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), narrator);

        VoiceCast cast = CastCharacters.Cast([C("k", "Knight", Gender.Male)], choices, roster, narrator, _ => VoiceFlavour.Normal, Book("k", "knight", new CharacterTraits(CharacterAge.Adult, CharacterBeing.Knight)));

        Assert.NotEqual(narrator, cast.VoiceFor("k"));
    }

    [Fact]
    public void A_character_with_traits_that_is_no_animal_is_never_matched_to_a_narrator_voice_animal()
    {
        IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.For("fa");
        string narrator = roster.First(c => c.Kind == CharacterKind.Narrator).VoiceId;
        var choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), narrator);

        VoiceCast cast = CastCharacters.Cast(
            [C("p", "Pirate", Gender.Male)], choices, roster, narrator, _ => VoiceFlavour.Normal, Book("p", "missing", new CharacterTraits(CharacterAge.Adult, CharacterBeing.Pirate)));

        Assert.NotEqual(narrator, cast.VoiceFor("p"));
    }

    [Fact]
    public void A_pick_replaced_by_the_books_voice_is_no_longer_the_grown_ups()
    {
        var characters = new[] { C("gull", "Seagull", Gender.Female) };
        var stored = new[] { new CastMember("gull", "Seagull", Gender.Female, "voice-that-left", Edited: true, SuggestedVoice: "duckling2", Traits: SeagullTraits) };
        VoiceCast cast = Cast("en", characters, StoryCast.PinnedCharacters(stored), Bird);

        CastMember member = StoryCast.ToMembers(characters, cast, LanguageRules.English, stored).Single();

        Assert.Equal(VoiceSource.Book, member.Source);
    }

    [Fact]
    public void Back_to_the_books_voice_is_offered_only_when_the_book_would_sound_different()
    {
        var speakers = CharacterRoster.Speakers("en");
        StorytellerCharacter duckling = speakers.First(c => c.Id == "duckling2");
        var picked = new CastMember("gull", "Seagull", duckling.Gender, "other", Edited: true, SuggestedVoice: "duckling2", Traits: SeagullTraits);
        var same = picked with { VoiceId = duckling.VoiceId, Flavour = duckling.Flavour };

        Assert.True(CastEditor.OffersBookVoice(picked, VoiceFlavour.Normal, speakers));
        Assert.False(CastEditor.OffersBookVoice(same, duckling.Flavour, speakers));
        Assert.True(CastEditor.OffersBookVoice(picked with { SuggestedVoice = null }, VoiceFlavour.Normal, speakers));
        Assert.False(CastEditor.OffersBookVoice(picked with { Traits = null, SuggestedVoice = "nonexistent" }, VoiceFlavour.Normal, speakers));
    }

    [Fact]
    public void Two_characters_that_suggest_the_same_voice_do_not_sound_alike()
    {
        var pinned = new Dictionary<string, PinnedCharacter>
        {
            ["a"] = new(string.Empty, null, false, "owl", new CharacterTraits(CharacterAge.Old)),
            ["b"] = new(string.Empty, null, false, "owl", new CharacterTraits(CharacterAge.Old)),
        };

        VoiceCast cast = Cast("en", [C("a", "A", Gender.Male), C("b", "B", Gender.Male)], pinned);

        Assert.Equal("owl", Played("en", cast, "a").Id);
        Assert.NotEqual(Played("en", cast, "a").Id, Played("en", cast, "b").Id);
    }

    [Fact]
    public void Back_to_the_books_voice_is_not_offered_for_a_suggestion_on_the_narrators_voice()
    {
        IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.For("en");
        StorytellerCharacter onNarrator = roster.First(c => c.Kind == CharacterKind.Speaker && c.Id == "knight");

        var bookable = CastCharacters.BookableSpeakers(roster, onNarrator.VoiceId);
        var picked = new CastMember("k", "Knight", Gender.Male, "other", Edited: true, SuggestedVoice: "knight");

        Assert.DoesNotContain(bookable, c => c.Id == "knight");
        Assert.False(CastEditor.OffersBookVoice(picked, VoiceFlavour.Normal, bookable));
    }

    [Fact]
    public void A_refresh_that_drops_a_books_voice_choice_takes_the_stale_one_away()
    {
        var old = new CastMember("a", "Pip", Gender.Male, "v", SuggestedVoice: "troll", Traits: new CharacterTraits(CharacterAge.Adult));
        Story stored = Story.NewOwnBook(Guid.NewGuid().ToString("N"), "en", DateTimeOffset.UnixEpoch, 0) with { Cast = [old] };
        Story bundled = stored with { Cast = [old with { SuggestedVoice = null, Traits = null }] };

        CastMember refreshed = StarterUpdate.Refresh(stored, bundled).Cast.Single();

        Assert.Null(refreshed.SuggestedVoice);
        Assert.Null(refreshed.Traits);
    }

    [Fact]
    public void Only_a_described_character_with_a_known_gender_overrides_the_gender_read_from_the_story()
    {
        AttributionResult raw = new([], [C("a", "A", Gender.Female), C("b", "B", Gender.Female), C("c", "C", Gender.Female)]);
        var stored = new[]
        {
            new CastMember("a", "A", Gender.Male, "v", SuggestedVoice: "owl"),
            new CastMember("b", "B", Gender.Male, "v", SuggestedVoice: "owl", Traits: new CharacterTraits(CharacterAge.Old)),
            new CastMember("c", "C", Gender.Unknown, "v", SuggestedVoice: "owl", Traits: new CharacterTraits(CharacterAge.Old)),
        };

        Assert.Equal([Gender.Female, Gender.Male, Gender.Female], StoryCast.Resolve(raw, stored).Characters.Select(c => c.Gender));
    }

    [Fact]
    public void Only_the_authored_books_have_traits()
    {
        Assert.Contains(StoreTexts.TreasureId, StarterVoices.AuthoredBooks);
        Assert.Contains(StarterTexts.PigsId, StarterVoices.AuthoredBooks);
        Assert.All(StoreTexts.All.Where(s => !StarterVoices.AuthoredBooks.Contains(s.Id)).Select(StoreBuilder.Build).SelectMany(b => b.Cast), m => Assert.Null(m.Traits));
    }

    [Fact]
    public void A_kept_member_whose_book_data_changed_in_a_starter_refresh_is_recast()
    {
        var old = new CastMember("a", "Pip", Gender.Male, "v", Flavour: VoiceFlavour.Normal);
        Story stored = Story.NewOwnBook(Guid.NewGuid().ToString("N"), "en", DateTimeOffset.UnixEpoch, 0) with { Cast = [old] };
        Story bundled = stored with { Cast = [old with { SuggestedVoice = "piglet", Traits = new CharacterTraits(CharacterAge.Young) }] };

        var recast = StarterUpdate.AnimalVoicesToRecast(stored, StarterUpdate.Refresh(stored, bundled), _ => CharacterRoster.Speakers("en"));

        Assert.Contains(("en", "a"), recast);
    }

    [Fact]
    public void Another_language_seed_carries_the_books_voice_choice_of_a_character_that_is_no_animal()
    {
        var plain = new CastMember("g", "Goldilocks", Gender.Female, "supertonic:F1", SuggestedVoice: "bee", Traits: new CharacterTraits(CharacterAge.Young));

        CastMember seeded = StoryCast.SeedForOtherLanguage([plain]).Single();

        Assert.Equal("bee", seeded.SuggestedVoice);
        Assert.NotNull(seeded.Traits);
        Assert.Equal(string.Empty, seeded.VoiceId);
    }

    [Fact]
    public void An_authored_book_stores_the_voice_its_suggestion_resolves_to()
    {
        var bookable = CastCharacters.BookableSpeakers(CharacterRoster.Speakers("en"), StarterBuilder.NarratorVoice);
        foreach (Story book in AuthoredBooks())
        {
            var sounds = new HashSet<(string, VoiceFlavour)>();
            int checks = 0;
            foreach (CastMember m in book.Cast)
            {
                StorytellerCharacter? chosen = bookable.FirstOrDefault(s => s.Id == m.SuggestedVoice && !CharacterRoster.OppositeGender(s.Gender, m.Gender) && !CastCharacters.SwapsInWhisper(s, m.Traits));
                if (chosen is null || !sounds.Add((chosen.VoiceId, chosen.Flavour)))
                {
                    continue;
                }

                checks++;
                Assert.True(m.VoiceId == chosen.VoiceId && m.Flavour == chosen.Flavour, $"{book.Title}/{m.Name} stores {m.VoiceId} {m.Flavour}, the book chose {chosen.Id} ({chosen.VoiceId} {chosen.Flavour})");
            }

            Assert.True(checks > 0, $"{book.Title}: no member's suggestion resolved, so nothing was checked");
        }
    }

    [Fact]
    public void An_authored_book_stores_no_two_characters_with_the_same_sound()
    {
        foreach (Story book in AuthoredBooks())
        {
            var clashes = book.Cast.Where(m => m.VoiceId != StarterBuilder.NarratorVoice).GroupBy(m => (m.VoiceId, m.Flavour)).Where(g => g.Count() > 1).Select(g => string.Join("+", g.Select(m => m.Name))).ToList();
            Assert.True(clashes.Count == 0, $"{book.Title}: same stored sound for {string.Join(", ", clashes)}");
        }
    }

    private static IEnumerable<Story> AuthoredBooks() =>
        StoreTexts.All.Where(s => StarterVoices.AuthoredBooks.Contains(s.Id)).Select(StoreBuilder.Build)
            .Concat(StarterBuilder.Starters.Where(s => StarterVoices.AuthoredBooks.Contains(s.Id)).Select(s => StarterBuilder.Build(s.Id, s.Title, s.Text, s.ColourIndex)));

    private static IEnumerable<Story> AllBooks() =>
        StoreTexts.All.Select(StoreBuilder.Build)
            .Concat(StarterBuilder.Starters.Select(s => StarterBuilder.Build(s.Id, s.Title, s.Text, s.ColourIndex)));
}
