using DinoReader.BookBuilder;
using ReadTheStupidText.Domain.Reading;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class StoreStoryTests : IDisposable
{
    private const int MinPages = 8;
    private const int MaxPages = 18; // the user's limit for a store book (2026-10-26, X Marks the Treasure!)
    private const int StoryCount = 11;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-store-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void TheStore_HasEveryStory_WithFixedUniqueIds()
    {
        Assert.Equal(StoryCount, Books.Store.Count);
        Assert.Equal(StoryCount, Books.Store.Select(s => s.Id).Distinct().Count());
        Assert.Equal(StoryCount, Books.Store.Select(s => s.Stem).Distinct().Count());
        Assert.All(Books.Store, s => Assert.True(StoryIds.IsValid(s.Id), s.Title));
        Assert.Empty(Books.Store.Select(s => s.Id).Intersect(StarterBuilder.Starters.Select(s => s.Id).Append(StarterBuilder.GoodEnoughId)));
    }

    [Fact]
    public void EveryStoreStory_IsShortAndHasAMeaningfulCredit()
    {
        foreach (StoreStorySpec spec in Books.Store)
        {
            Story story = StoreBuilder.Build(spec);

            Assert.InRange(story.Pages.Count, MinPages, MaxPages);
            Assert.False(string.IsNullOrWhiteSpace(spec.Credits), spec.Title);
            Assert.InRange(spec.AgeMin, 0, spec.AgeMax);
            Assert.InRange(spec.AgeMax, spec.AgeMin, 18);
            Assert.False(string.IsNullOrWhiteSpace(spec.Author), spec.Title);
            Assert.True(story.TranslateToMyLanguage, spec.Title);
        }
    }

    // Slice 159: the store shows each title in the app language, so meta.json carries it for every language the book is translated into.
    [Fact]
    public void EveryStoreStory_WritesItsTitleInEveryOtherLanguage_IntoMetaJson()
    {
        foreach (StoreStorySpec spec in Books.Store)
        {
            using JsonDocument meta = JsonDocument.Parse(StoreBuilder.MetaJson(spec, StoreBuilder.Build(spec)));
            JsonElement titles = meta.RootElement.GetProperty("titles");

            string[] expected = UiLanguage.Supported.Where(l => l != UiLanguage.English).Order().ToArray();
            Assert.Equal(expected, titles.EnumerateObject().Select(t => t.Name).Order().ToArray());
            Assert.All(titles.EnumerateObject(), t => Assert.False(string.IsNullOrWhiteSpace(t.Value.GetString()), $"{spec.Title}: {t.Name}"));
        }
    }

    [Fact]
    public void EveryShippedBook_HasATitleInEveryOtherLanguage()
    {
        string[] others = UiLanguage.Supported.Where(l => l != UiLanguage.English).Order().ToArray();
        IEnumerable<string> titles = Books.Store.Select(s => s.Title).Concat(StarterBuilder.Starters.Select(s => s.Title)).Append(StarterBuilder.GoodEnoughTitle);

        foreach (string title in titles)
        {
            Assert.Equal(others, ShippedTitles.AllFor(title).Keys.Order().ToArray());
            Assert.All(ShippedTitles.AllFor(title).Values, v => Assert.False(string.IsNullOrWhiteSpace(v), title));
        }

        Assert.Equal(titles.Count(), ShippedTitles.Titles.Count);
    }

    [Fact]
    public void EveryStoreStory_CastsItsNamedCharacters_EachWithAVoiceOfTheirOwn()
    {
        foreach (StoreStorySpec spec in Books.Store)
        {
            Story story = StoreBuilder.Build(spec);
            IReadOnlyList<string> found = story.Cast.Select(c => c.Name).ToList();

            foreach (string expected in spec.Characters)
            {
                Assert.True(found.Contains(expected), $"{spec.Title}: no character {expected}; found {string.Join(", ", found)}");
            }

            Assert.True(
                story.Cast.Count == story.Cast.Select(c => (c.VoiceId, c.Flavour)).Distinct().Count(),
                $"{spec.Title}: two characters share one (voice, flavour): {string.Join(", ", story.Cast.Select(c => $"{c.Name}={c.VoiceId}/{c.Flavour}"))}");
            Assert.DoesNotContain(story.Cast, c => c.VoiceId == SupertonicVoiceTable.Default.Id);
        }
    }

    public static TheoryData<string> Languages() => new(UiLanguage.Supported);

    // The Lion and the Mouse (Slice 156): the book suggests the Friendly Giant for the Lion and the Squeaky Mouse for the Mouse ("her supper"). The mouse voice is a boy in
    // English and Farsi, so there the suggestion clashes on gender and her traits pick a girl's young, squeaky voice instead.
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheLionAndTheMouse_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        AssertEachIsPlayedByTheirGender(StoreBuilder.Build(Books.SpecOf("lion-and-mouse")), language, new Dictionary<string, Gender>
        {
            ["Lion"] = Gender.Male, ["Mouse"] = Gender.Female,
        });
    }

    // The roster's duckling voices are one gender per language, so the book chooses each voice (StarterVoices, Slice 156: mummy, piglet, bunny, bee, knight) with the gender, and Duckling
    // and Dilly are ducks again. The app casts a language other than the story's afresh, carrying the animal settings and the book's choices; the story's language keeps its cast.
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheUglyDuckling_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        AssertEachIsPlayedByTheirGender(StoreBuilder.Build(Books.SpecOf("ugly-duckling")), language, new Dictionary<string, Gender>
        {
            ["Mother Duck"] = Gender.Female, ["Dilly"] = Gender.Female, ["Hen"] = Gender.Female, ["Duckling"] = Gender.Male, ["Swan"] = Gender.Male,
        });
    }

    // Every roster's bear voices (bear, big bear, cub) are men's, so the builder stores Mama Bear as no animal (StarterAnimals): she is cast by gender, a woman's voice.
    // Papa Bear and Baby Bear keep their bear voices and growl; Baby Bear is a boy cub ("he forgot to shut the door").
    // Mum has no line, so the cast is exactly these four (the helper fails on any other character).
    [Theory]
    [MemberData(nameof(Languages))]
    public void Goldilocks_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        AssertEachIsPlayedByTheirGender(StoreBuilder.Build(Books.SpecOf("goldilocks")), language, new Dictionary<string, Gender>
        {
            ["Goldilocks"] = Gender.Female, ["Mama Bear"] = Gender.Female, ["Papa Bear"] = Gender.Male, ["Baby Bear"] = Gender.Male,
        });
    }

    // The Little Red Hen (Slices 121, 156): the roster's kitten and duckling voices are girls in most languages but boys in Farsi, Spanish and Portuguese. The book
    // chooses each voice (StarterVoices: mummy, puppy, kitten, jester) with the gender, so Cat and Duck keep their animals and a suggestion of the wrong gender falls to the traits.
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheLittleRedHen_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        var expected = new Dictionary<string, Gender>
        {
            ["Hen"] = Gender.Female, ["Dog"] = Gender.Male, ["Cat"] = Gender.Female, ["Duck"] = Gender.Male,
        };

        Story story = StoreBuilder.Build(Books.SpecOf("little-red-hen"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // The Town Mouse and the Country Mouse (Slice 121): Town Mouse is "she" and Country Mouse "he", so the two friends sound different in every language (the
    // stored English genders are checked too, since every other language is cast from them). Both are mice again since Slice 156: the book suggests bee and piglet.
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheTownMouseAndTheCountryMouse_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        Story story = StoreBuilder.Build(Books.SpecOf("town-mouse-country-mouse"));
        var expected = new Dictionary<string, Gender>
        {
            ["Town Mouse"] = Gender.Female, ["Country Mouse"] = Gender.Male,
        };

        Assert.Equal(expected.Keys.Order(), story.Cast.Select(m => m.Name).Order());
        Assert.All(story.Cast, m => Assert.True(m.Gender == expected[m.Name], $"{m.Name} is stored as {m.Gender}"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // The Tale of Peter Rabbit (Slice 121): Mrs. Rabbit and Flopsy are "she", Peter, Mr. McGregor and Robin "he". The roster's bunny voice is a girl in most
    // languages, so Peter's book suggests a boy's voice (piglet) and Flopsy's a girl's (bee); both are rabbits again (Slice 156). Mrs. Rabbit suggests the mummy voice.
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheTaleOfPeterRabbit_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        var expected = new Dictionary<string, Gender>
        {
            ["Mrs. Rabbit"] = Gender.Female, ["Flopsy"] = Gender.Female, ["Peter"] = Gender.Male, ["Mr. McGregor"] = Gender.Male, ["Robin"] = Gender.Male,
        };

        Story story = StoreBuilder.Build(Books.SpecOf("peter-rabbit"));
        Assert.Equal(expected.Keys.Order(), story.Cast.Select(m => m.Name).Order());
        Assert.All(story.Cast, m => Assert.True(m.Gender == expected[m.Name], $"{m.Name} is stored as {m.Gender}"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // X Marks the Treasure! (Slice 121): Kathryn, Izzy and Pancake Polly are girls, the Seagull is a "she" with no animal word the guesser knows, Billy is a boy.
    [Theory]
    [MemberData(nameof(Languages))]
    public void XMarksTheTreasure_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        var expected = new Dictionary<string, Gender>
        {
            ["Kathryn"] = Gender.Female, ["Izzy"] = Gender.Female, ["Pancake Polly"] = Gender.Female, ["Seagull"] = Gender.Female, ["Billy"] = Gender.Male,
        };

        Story story = StoreBuilder.Build(Books.SpecOf("x-marks-the-treasure"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // The Elves and the Shoemaker (Slice 121): two and two, the shoemaker Hans and the elf Nob are "he", his wife Greta and the elf Nib "she"; none is an
    // animal. The book suggests knight, mummy, bee and piglet (Slice 156), which every language's roster has, so each keeps a voice of their gender (the stored
    // English genders are checked too, since every other language is cast from them).
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheElvesAndTheShoemaker_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        var expected = new Dictionary<string, Gender>
        {
            ["Hans"] = Gender.Male, ["Greta"] = Gender.Female, ["Nib"] = Gender.Female, ["Nob"] = Gender.Male,
        };

        Story story = StoreBuilder.Build(Books.SpecOf("elves-and-shoemaker"));
        Assert.Equal(expected.Keys.Order(), story.Cast.Select(m => m.Name).Order());
        Assert.All(story.Cast, m => Assert.True(m.Gender == expected[m.Name], $"{m.Name} is stored as {m.Gender}"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // The Three Billy Goats Gruff (Slice 156): the three brothers and the Troll are all "he", so each gets a man's or boy's voice in every language (the book suggests
    // piglet, knight, giant and troll; Farsi has no troll, so its Troll is matched by traits). The stored English genders are checked too.
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheThreeBillyGoatsGruff_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        var expected = new Dictionary<string, Gender>
        {
            ["Little Billy"] = Gender.Male, ["Middle Billy"] = Gender.Male, ["Big Billy"] = Gender.Male, ["Troll"] = Gender.Male,
        };

        Story story = StoreBuilder.Build(Books.SpecOf("billy-goats-gruff"));
        Assert.Equal(expected.Keys.Order(), story.Cast.Select(m => m.Name).Order());
        Assert.All(story.Cast, m => Assert.True(m.Gender == expected[m.Name], $"{m.Name} is stored as {m.Gender}"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // The Gingerbread Man starter (also a store story) is a boy, so he must get a male voice in every language; Granny, the Cow and the Horse are "she",
    // Grandpa and the Fox "he" (the stored English genders are checked too, since every other language is cast from them).
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheGingerbreadMan_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        Story story = StarterBuilder.Build(Books.IdOf("gingerbread-man"), "The Gingerbread Man", Books.Folder("gingerbread-man").Text!, 1);
        var expected = new Dictionary<string, Gender>
        {
            ["Granny"] = Gender.Female, ["Grandpa"] = Gender.Male, ["Gingerbread Man"] = Gender.Male, ["Cow"] = Gender.Female, ["Horse"] = Gender.Female, ["Fox"] = Gender.Male,
        };

        Assert.Equal(expected.Keys.Order(), story.Cast.Select(m => m.Name).Order());
        Assert.All(story.Cast, m => Assert.True(m.Gender == expected[m.Name], $"{m.Name} is stored as {m.Gender}"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // The Tortoise and the Hare starter (also a store story): the Hare, Owl and Hedgehog are "he", the Tortoise, Squirrel and Frog "she", three of each, and each
    // must get a voice of that gender in every language (the stored English genders are checked too, since every other language is cast from them).
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheTortoiseAndTheHare_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        Story story = StarterBuilder.Build(Books.IdOf("tortoise-and-hare"), "The Tortoise and the Hare", Books.Folder("tortoise-and-hare").Text!, 0);
        var expected = new Dictionary<string, Gender>
        {
            ["Hare"] = Gender.Male, ["Tortoise"] = Gender.Female, ["Owl"] = Gender.Male, ["Squirrel"] = Gender.Female, ["Hedgehog"] = Gender.Male, ["Frog"] = Gender.Female,
        };

        Assert.Equal(expected.Keys.Order(), story.Cast.Select(m => m.Name).Order());
        Assert.All(story.Cast, m => Assert.True(m.Gender == expected[m.Name], $"{m.Name} is stored as {m.Gender}"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // Casts the story the way the app does in that language (a language other than the story's is cast afresh, carrying the animal settings, edits and the book's voice choices; the story's
    // language keeps its cast) and checks that each character's roster character has the expected gender.
    private static void AssertEachIsPlayedByTheirGender(Story story, string language, IReadOnlyDictionary<string, Gender> expected)
    {
        IReadOnlyList<CastMember> stored = language == story.Language
            ? story.Cast
            : StoryCast.SeedForOtherLanguage(story.Cast);
        List<Character> characters = story.Cast.Select(m => new Character(m.CharacterId, m.Name, m.Gender, 1)).ToList();
        IReadOnlyDictionary<string, AnimalKind> animals = AnimalSounds.Resolve(characters, story.Language, stored, story.Text);
        IReadOnlyList<StorytellerCharacter> speakers = CharacterRoster.Speakers(language);
        string narrator = CharacterRoster.Narrators(language)[0].VoiceId;

        VoiceCast cast = CastCharacters.Cast(
            characters, CharacterRoster.Choices(speakers, narrator), CharacterRoster.For(language), narrator, c => FlavourGuess.Guess(c.Name, LanguageRules.English),
            StoryCast.PinnedCharacters(stored), animalOf: c => animals.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, story.Language) : null);

        foreach (Character character in characters)
        {
            Assert.True(expected.ContainsKey(character.Name), $"{language}: the cast has {character.Name}, which this test has no expected gender for");
            StorytellerCharacter? played = CharacterRoster.Nearest(speakers, cast.VoiceFor(character.Id)!, cast.Flavours![character.Id], character.Gender);
            Assert.True(played?.Gender == expected[character.Name], $"{language}: {character.Name} is played by {played?.Name} ({played?.Gender})");
        }
    }

    [Fact]
    public void EveryStoreStory_HasAllTheOtherLanguagesTranslated_ForEveryPiece()
    {
        foreach (StoreStorySpec spec in Books.Store)
        {
            Story story = StoreBuilder.Build(spec);
            IReadOnlyList<string> pieces = StoryPieces.TranslatablePieceTexts(story);

            foreach (string language in UiLanguage.Supported.Where(l => l != UiLanguage.Resolve(story.Language)))
            {
                IEnumerable<string> missing = pieces.Where(p => string.IsNullOrWhiteSpace(BundledTranslations.Lookup(story, language, p)));
                Assert.True(!missing.Any(), $"{spec.Stem} has no {language} translation for: {string.Join(" | ", missing.Take(3))}");
            }
        }
    }

    [Fact]
    public async Task APackedStoreStory_ImportsWithItsStoreId_AndItsTranslations()
    {
        StoreStorySpec spec = Books.Store[0];
        var library = new FileStoryLibrary(_root);
        Story built = StoreBuilder.Build(spec);
        await library.AddAsync(built);
        using var package = new MemoryStream();
        await StoryPackageWriter.WriteAsync(built, library, package);
        package.Position = 0;

        Story imported = await StoryPackageReader.ImportAsync(package, new FileStoryLibrary(Path.Combine(_root, "other")));

        Assert.Equal(spec.Id, imported.SourceId);
        Assert.Equal(spec.Title, imported.Title);
        Assert.Equal(built.Cast, imported.Cast);
        Assert.True(imported.TranslateToMyLanguage);
        Assert.Equal(UiLanguage.Supported.Count() - 1, imported.Translations!.Count);
    }

    [Fact]
    public void TheMeta_HoldsWhatTheWorkerChecks_AndTheCredit()
    {
        StoreStorySpec spec = Books.Store[0];
        Story story = StoreBuilder.Build(spec);

        using JsonDocument meta = JsonDocument.Parse(StoreBuilder.MetaJson(spec, story));

        JsonElement root = meta.RootElement;
        Assert.Equal(StoryPackageManifest.CurrentSchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(spec.Title, root.GetProperty("title").GetString());
        Assert.Equal("en", root.GetProperty("language").GetString());
        Assert.Equal(spec.Version, root.GetProperty("version").GetInt32());
        Assert.Equal(story.Pages.Count, root.GetProperty("pageCount").GetInt32());
        Assert.True(root.GetProperty("minutes").GetInt32() >= 1);
        Assert.Equal(spec.Added, root.GetProperty("added").GetString());
        Assert.Equal(spec.Credits, root.GetProperty("credits").GetString());
        Assert.Equal(spec.AgeMin, root.GetProperty("ageMin").GetInt32());
        Assert.Equal(spec.AgeMax, root.GetProperty("ageMax").GetInt32());
        Assert.Equal(spec.Author, root.GetProperty("author").GetString());
    }

    [Theory]
    [InlineData("A", 1)]
    [InlineData("The Town Mouse and the Country Mouse", 5)]
    [InlineData("Goldilocks and the Three Bears", 4)]
    public void TheCoverTitle_WrapsOnWholeWords_AtTenLettersAtMost(string title, int lines)
    {
        IReadOnlyList<string> wrapped = StoreCover.Wrap(title);

        Assert.Equal(lines, wrapped.Count);
        Assert.All(wrapped, l => Assert.True(l.Length <= 10, l));
    }

    [Fact]
    public void EveryStoreTitle_FitsTheCoverFont()
    {
        Assert.All(Books.Store, s => Assert.All(StoreCover.Wrap(s.Title), l => Assert.True(l.Length <= 10 && l.All(c => c is ' ' or '!' or (>= 'A' and <= 'Z')), l)));
    }

    [Fact]
    public void TheCover_RendersATitleWithAnExclamationMark()
    {
        IReadOnlyList<string> wrapped = StoreCover.Wrap("X Marks the Treasure!");
        byte[] jpeg = StoreCover.Render("X Marks the Treasure!", CoverPalette.ColourAt(0));

        Assert.Contains(wrapped, l => l.EndsWith('!'));
        Assert.Equal([0xFF, 0xD8], jpeg[..2]);
        Assert.Equal([0xFF, 0xD9], jpeg[^2..]);
    }

    [Fact]
    public void TheCover_IsAJpegOfTheListSize_WithItsSegmentsInPlace()
    {
        byte[] jpeg = StoreCover.Render("The Lion and the Mouse", CoverPalette.ColourAt(1));

        Assert.Equal([0xFF, 0xD8], jpeg[..2]);
        Assert.Equal([0xFF, 0xD9], jpeg[^2..]);
        Dictionary<byte, int> segments = JpegSegments(jpeg);
        foreach (byte marker in new byte[] { 0xDB, 0xC0, 0xC4, 0xDA })
        {
            Assert.True(segments.ContainsKey(marker), $"missing marker 0xFF{marker:X2}");
        }

        int frame = segments[0xC0];
        Assert.Equal(StoreCover.Height, (jpeg[frame + 5] << 8) | jpeg[frame + 6]);
        Assert.Equal(StoreCover.Width, (jpeg[frame + 7] << 8) | jpeg[frame + 8]);
    }

    [Fact]
    public void EveryBundledStarter_HasAStoreEntry_WithTheSameIdAsTheBundledFile()
    {
        Assert.Equal(StarterBuilder.AllFileNames.Order(), StoreBuilder.StarterEntries.Keys.Order());
        var ids = StarterBuilder.Starters.ToDictionary(s => s.FileName, s => s.Id);
        ids[StarterBuilder.GoodEnoughFile] = StarterBuilder.GoodEnoughId;
        foreach (var (fileName, entry) in StoreBuilder.StarterEntries)
        {
            Assert.Equal(ids[fileName], entry.Id);
            Assert.InRange(entry.AgeMin, 0, entry.AgeMax);
            Assert.InRange(entry.AgeMax, entry.AgeMin, 18);
            Assert.False(string.IsNullOrWhiteSpace(entry.Credits) || string.IsNullOrWhiteSpace(entry.Author), entry.Title);
        }
    }

    [Fact]
    public async Task TheStarterStoreBuild_KeepsTheBundledIds_AndWritesMetaTheWorkerChecks()
    {
        var library = new FileStoryLibrary(_root);

        var built = await StarterBuilder.BuildAllAsync(library);

        Assert.Equal(StarterBuilder.AllFileNames.Count, built.Count);
        foreach (var (fileName, story) in built)
        {
            StoreStorySpec spec = StoreBuilder.StarterEntries[fileName].ToSpec(story, StarterBuilder.StarterVersions[fileName]);
            Assert.Equal(story.Id, spec.Id);
            using JsonDocument meta = JsonDocument.Parse(StoreBuilder.MetaJson(spec, story));
            Assert.Equal(story.Pages.Count, meta.RootElement.GetProperty("pageCount").GetInt32());
            Assert.Equal(StarterBuilder.StarterVersions[fileName], meta.RootElement.GetProperty("version").GetInt32());
        }
    }

    [Fact]
    public async Task APictureCover_IsStoredAsTheCoverPhoto_AndBecomesTheStoreCover()
    {
        var library = new FileStoryLibrary(_root);
        StoreStorySpec spec = Books.Store[0];
        Story story = StoreBuilder.Build(spec);
        await library.AddAsync(story);
        string source = Path.Combine(_root, "source-cover.jpg");
        byte[] picture = [0xFF, 0xD8, 1, 2, 3, 0xFF, 0xD9];
        await File.WriteAllBytesAsync(source, picture);

        Story covered = await StarterBuilder.WithPictureCoverAsync(story, source, library);

        Assert.Equal(CoverStyle.Picture, covered.CoverStyle);
        Assert.Throws<InvalidOperationException>(() => StoreBuilder.CoverBytes(covered, spec, library));
        string thumb = Path.Combine(_root, "thumb.jpg");
        await File.WriteAllBytesAsync(thumb, [0xFF, 0xD8, 9, 0xFF, 0xD9]);
        Assert.Equal(await File.ReadAllBytesAsync(thumb), StoreBuilder.CoverBytes(covered, spec, library, thumb));
        Assert.Same(story, await StarterBuilder.WithPictureCoverAsync(story, null, library));
        Assert.Equal(CoverStyle.Colour, story.CoverStyle);
    }

    [Fact]
    public void PicturesOfTheWrongCount_FailTheBuild()
    {
        Story story = StoreBuilder.Build(Books.Store[0]);
        string[] tooFew = Enumerable.Range(1, story.Pages.Count - 1).Select(i => $"{i}.jpg").ToArray();

        Assert.Throws<InvalidOperationException>(() => StarterBuilder.WithPictures(story, tooFew));
        Assert.All(StarterBuilder.WithPictures(story, Enumerable.Range(1, story.Pages.Count).Select(i => $"{i}.jpg").ToArray()).Pages, p => Assert.NotNull(p.ImageFile));
    }

    /// <summary>The first offset of each marker segment before the scan data (<c>FF xx</c> then a two-byte length), keyed by marker.</summary>
    private static Dictionary<byte, int> JpegSegments(byte[] jpeg)
    {
        var found = new Dictionary<byte, int>();
        int at = 2;
        while (at + 4 <= jpeg.Length && jpeg[at] == 0xFF)
        {
            byte marker = jpeg[at + 1];
            found.TryAdd(marker, at);
            if (marker == 0xDA)
            {
                break;
            }

            at += 2 + ((jpeg[at + 2] << 8) | jpeg[at + 3]);
        }

        return found;
    }
}
