using System.Security.Cryptography;
using System.Text;

namespace DinoReader.Core;

/// <summary>
/// When a bundled starter's text changes, an installed copy is replaced (Decision 130): each starter package carries a content
/// <see cref="StoryPackageManifest.Version"/>, the app remembers the version it installed, and a lower one is refreshed on launch.
/// The grown-up's own choices (cast, merges, reading mode, switches, title, cover pick) stay with the new copy; a copy whose text
/// they edited is left alone (<see cref="IsEdited"/>). A deleted starter stays deleted.
/// </summary>
public static class StarterUpdate
{
    /// <summary>The version of a starter installed before versions were recorded: the first release of every starter.</summary>
    public const int LegacyInstalledVersion = 1;

    // Hashes of each shipped starter's page text at its earlier versions (package id to the hash of the pages joined by Story.PageSeparator).
    // A stored copy whose text matches none of these (nor the bundled text) was edited by the grown-up, so a refresh would overwrite their work.
    private static readonly IReadOnlyDictionary<string, string[]> EarlierPageTextHashes = new Dictionary<string, string[]>
    {
        ["966400017c494fb5b045786f53852c9e"] = ["ABD1A8864145C962A41879303519F1BB98A1CF3E85D78CECFDBF8C299D38482B", "8C1DD87C17BFC3A1386D16CEF384F13FD3C04DEDA8FD2A7D514FC90130F1CC3D"],
        ["bee62232085d4c1ab50cdf574530f473"] = ["4884F000A3C51474F02A4B4209EF16D50FA24EFFC0EA6F682460B99CD419FA65"],
        ["0fa29ae0d09643a9a16a5e586e8c7119"] = ["86147AB77F14E5F15887D4FC67235CFF06020CD242986082122C685E78ED356C", "C0E318F0E1C12A38D918A0AE52430A2E0336D5EC57E858125F2079AF14C1405A"],
        ["61bb25b8f132413c82a4208827c7563b"] = ["F6CB92153C1C9713DBDBA7F3AFA6072A78DB84C8FE124CF005A194D5D5E10AF5"],
    };

    /// <summary>Whether the installed copy (<paramref name="installedVersion"/>, null when none was recorded) is older than the bundled one.</summary>
    public static bool NeedsUpdate(int? installedVersion, int bundledVersion) =>
        (installedVersion ?? LegacyInstalledVersion) < bundledVersion;

    /// <summary>The hash of a story's page texts, as kept in the earlier-versions table.</summary>
    public static string PageTextHash(Story story) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(Story.PageSeparator, story.Pages.Select(p => p.Text)))));

    // The colour cover index every earlier version of each starter shipped with (never changed since v1): a stored colour cover with that index is the
    // shipped one, not the grown-up's pick, so a bundled picture cover replaces it.
    private static readonly IReadOnlyDictionary<string, int> ShippedCoverColours = new Dictionary<string, int>
    {
        ["966400017c494fb5b045786f53852c9e"] = 3,
        ["bee62232085d4c1ab50cdf574530f473"] = 1,
        ["0fa29ae0d09643a9a16a5e586e8c7119"] = 0,
    };

    /// <summary>Whether the grown-up changed the stored copy's page text: it matches neither the bundled text nor any earlier shipped version of the starter.
    /// An edited copy is not refreshed, so the edits are not lost.</summary>
    public static bool IsEdited(string packageId, Story stored, Story bundled)
    {
        string hash = PageTextHash(stored);
        return hash != PageTextHash(bundled)
            && !(EarlierPageTextHashes.TryGetValue(packageId, out string[]? earlier) && earlier.Contains(hash));
    }

    /// <summary>Of several stored copies sharing one starter id (a refresh that stopped halfway), the one to keep: a copy already holding the bundled text, else the first.</summary>
    public static Story CopyToKeep(IReadOnlyList<Story> copies, Story bundled)
    {
        string bundledHash = PageTextHash(bundled);
        return copies.FirstOrDefault(c => PageTextHash(c) == bundledHash) ?? copies[0];
    }

    /// <summary>Whether the stored copy's cover is a gallery photo the grown-up chose, whose file has to move to the refreshed story: a photo on a starter that earlier
    /// shipped a colour cover (<see cref="ShippedCoverColours"/>) is always the grown-up's; otherwise it is, only while the bundled cover is not a photo.</summary>
    public static bool KeepsStoredPhoto(Story stored, Story bundled) =>
        stored.CoverStyle == CoverStyle.Picture && stored.CoverPageIndex is null && stored.CoverFile is not null
        && (!(bundled.CoverStyle == CoverStyle.Picture && bundled.CoverFile is not null)
            || (stored.SourceId is { } id && ShippedCoverColours.ContainsKey(id)));

    /// <summary>The refreshed story: the bundled text, pictures and translations, with the stored copy's id-independent choices kept (cast, merges, mode, switches,
    /// title and a cover the grown-up picked; a gallery photo cover keeps the stored file name, which the caller must copy: <see cref="KeepsStoredPhoto"/>).
    /// The cast is kept only while the bundled text still has the same characters (a changed cast would leave a voice with no one to read it);
    /// a page recording stays with its page text (<see cref="RecordingMoves"/>); only a page whose text changed loses it.
    /// <paramref name="copiedMoves"/> are the recordings whose files really moved (default: every move); only those keep a reference.</summary>
    public static Story Refresh(Story stored, Story bundled, IReadOnlyList<RecordingMove>? copiedMoves = null)
    {
        bool sameCast = SameCharacters(stored.Cast, bundled.Cast);
        IReadOnlyList<RecordingMove> moves = copiedMoves ?? RecordingMoves(stored, bundled);
        Dictionary<int, PageRecording>? recordings = moves.Count > 0 ? moves.ToDictionary(m => m.To, m => stored.Recordings![m.From]) : null;
        return KeepCover(stored, bundled with
        {
            Recordings = recordings,
            UseRecordings = recordings is not null && stored.UseRecordings,
            Created = stored.Created,
            Title = stored.Title,
            Cast = sameCast ? WithBundledAnimals(stored.Cast, bundled.Cast) : bundled.Cast,
            LanguageCasts = sameCast ? KeptLanguageCasts(stored, bundled) : bundled.LanguageCasts,
            CharacterMerges = sameCast ? stored.CharacterMerges : bundled.CharacterMerges,
            PreferredMode = stored.PreferredMode,
            TranslateToMyLanguage = stored.TranslateToMyLanguage,
            ShowOriginalText = stored.ShowOriginalText,
            AnimalSoundsOn = stored.AnimalSoundsOn,
            WordsOverPictures = stored.WordsOverPictures,
        });
    }

    // A kept cast member with no animal decided yet takes the bundled one (Slice 125), so an installed starter that gains animals still gets them;
    // an animal the grown-up set (or "not an animal") stays.
    // Matched by character id alone: a story's character ids are shared by all its language casts (the speakers come from the story's own language), so a language cast
    // that has no bundled cast of its own can take the animals of the base cast; a name check would only fail where the languages translate the names (Slice 135).
    private static IReadOnlyList<CastMember> WithBundledAnimals(IReadOnlyList<CastMember> kept, IReadOnlyList<CastMember> bundled) =>
        kept.Select(m => !m.HasAnimalSetting && bundled.FirstOrDefault(b => b.CharacterId == m.CharacterId) is { HasAnimalSetting: true } b
            ? m with { Animal = b.Animal, NotAnimal = b.NotAnimal }
            : m).ToList();

    /// <summary>The kept cast members whose voice the grown-up did not choose by hand and whose character is not the one for their animal, as (cast language, character id):
    /// each needs its voice picked again from its animal (the caller does it the way the cast screen does, <c>StoryVoicing.RecastCharacters</c>). The animal is the resolved one
    /// (stored, then the name, then the text, Slice 135), so a Wolf found by its name is recast too, and a member that is no animal but sounds like one is recast as a plain character.
    /// A member already on a character of its animal's kind (any age) stays, and so does one whose animal has no fitting character in the roster. <paramref name="speakersFor"/>
    /// gives the speakers a cast language can really be cast with (the app's adapted roster); the default is the language's whole roster.
    /// Nothing when the cast was replaced.</summary>
    public static IReadOnlyList<(string Language, string CharacterId)> AnimalVoicesToRecast(
        Story stored, Story refreshed, Func<string, IReadOnlyList<StorytellerCharacter>>? speakersFor = null)
    {
        if (!SameCharacters(stored.Cast, refreshed.Cast))
        {
            return [];
        }

        var casts = new List<(string Language, IReadOnlyList<CastMember> Members)> { (refreshed.Language, refreshed.Cast) };
        casts.AddRange((refreshed.LanguageCasts ?? new Dictionary<string, IReadOnlyList<CastMember>>()).Select(c => (c.Key, c.Value)));
        return casts
            .SelectMany(c => MembersWithOtherAnimal(c.Members, (speakersFor ?? CharacterRoster.Speakers)(c.Language), refreshed).Select(id => (c.Language, id)))
            .Distinct()
            .ToList();
    }

    private static IEnumerable<string> MembersWithOtherAnimal(IReadOnlyList<CastMember> members, IReadOnlyList<StorytellerCharacter> roster, Story story)
    {
        var characters = members.Select(m => new Character(m.CharacterId, m.Name, m.Gender, 0)).ToList();
        IReadOnlyDictionary<string, AnimalKind> wanted = AnimalSounds.Resolve(characters, story.Language, members, story.Text);
        return members
            .Where(m => !m.Edited && PlaysOtherAnimal(m, wanted.TryGetValue(m.CharacterId, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, m.Name, story.Language) : null, roster))
            .Select(m => m.CharacterId);
    }

    // The roster character the member sounds like is known only when the voice and flavour are exactly a roster character's (a voice from another engine says nothing).
    // An animal member is wrong unless it plays a character of its animal's kind, of any age; if the roster has no character that could play it (a limited pool, or a
    // grown pig, which has none) there is nothing to recast to.
    private static bool PlaysOtherAnimal(CastMember member, AnimalHint? wanted, IReadOnlyList<StorytellerCharacter> roster)
    {
        StorytellerCharacter? playing = roster.FirstOrDefault(c => c.VoiceId == member.VoiceId && c.Flavour == member.Flavour);
        IReadOnlyList<AnimalKind>? current = playing is null ? null : AnimalCharacters.Of(playing.Id)?.Kinds ?? [];
        if (playing is null)
        {
            return false;
        }

        if (wanted is null)
        {
            return current is { Count: > 0 };
        }

        bool canBeCast = roster.Any(c => AnimalCharacters.FitOf(c, wanted) != AnimalFit.None);
        return canBeCast && current?.Contains(wanted.Kind) != true;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CastMember>>? KeptLanguageCasts(Story stored, Story bundled)
    {
        if (stored.LanguageCasts is null)
        {
            return null;
        }

        // A language the bundled package has no cast for takes its animals from the bundled base cast, by character id (Slice 135: the Farsi cast kept no animals, so Pop and
        // Pim read "Not an animal").
        return stored.LanguageCasts.ToDictionary(
            c => c.Key,
            c => WithBundledAnimals(WithBundledAnimals(c.Value, bundled.LanguageCasts?.GetValueOrDefault(c.Key) ?? []), bundled.Cast));
    }

    // A grown-up's cover pick stays: a gallery photo, a page of the story that still has a picture, a first-page cover, or a colour other than the bundled one.
    private static Story KeepCover(Story stored, Story refreshed)
    {
        if (KeepsStoredPhoto(stored, refreshed))
        {
            return refreshed with { CoverStyle = CoverStyle.Picture, CoverFile = stored.CoverFile, CoverPageIndex = null };
        }

        if (!KeepsOtherCover(stored, refreshed))
        {
            return refreshed;
        }

        bool picture = stored.CoverStyle == CoverStyle.Picture;
        return refreshed with
        {
            CoverStyle = stored.CoverStyle,
            CoverColourIndex = stored.CoverColourIndex,
            CoverPageIndex = picture ? stored.CoverPageIndex : null,
            CoverFile = picture ? null : refreshed.CoverFile,
        };
    }

    private static bool KeepsOtherCover(Story stored, Story refreshed) => stored.CoverStyle switch
    {
        CoverStyle.Picture => stored.Pages.Count == refreshed.Pages.Count && stored.CoverPageIndex is { } index && index < refreshed.Pages.Count && refreshed.Pages[index].ImageFile is not null,
        CoverStyle.Colour => stored.CoverColourIndex != ShippedCoverColour(stored, refreshed),
        _ => true,
    };

    private static int ShippedCoverColour(Story stored, Story refreshed) =>
        stored.SourceId is { } id && ShippedCoverColours.TryGetValue(id, out int shipped) ? shipped : refreshed.CoverColourIndex;

    /// <summary>The story with the recordings of <paramref name="moves"/> (taken from <paramref name="source"/>) added to the pages that have none yet.</summary>
    public static Story WithMovedRecordings(Story target, Story source, IReadOnlyList<RecordingMove> moves)
    {
        var merged = new Dictionary<int, PageRecording>(target.Recordings ?? new Dictionary<int, PageRecording>());
        foreach (RecordingMove move in moves)
        {
            merged.TryAdd(move.To, source.Recordings![move.From]);
        }

        return target with { Recordings = merged.Count > 0 ? merged : null };
    }

    /// <summary>One recording carried to the refreshed story: the page it was on (<see cref="From"/>) and the page with the same text it moves to (<see cref="To"/>).</summary>
    public readonly record struct RecordingMove(int From, int To);

    /// <summary>Which of <paramref name="stored"/>'s recordings still match <paramref name="bundled"/>: each recorded page goes to the same page index when its text is identical there,
    /// else to the first not-yet-taken bundled page with exactly the same text; a page whose text changed has no move and so loses its recording.</summary>
    public static IReadOnlyList<RecordingMove> RecordingMoves(Story stored, Story bundled)
    {
        var moves = new List<RecordingMove>();
        var taken = new HashSet<int>();
        foreach (int from in StoryRecordings.RecordedPages(stored))
        {
            string text = stored.Pages[from].Text;
            bool sameIndex = from < bundled.Pages.Count && !taken.Contains(from) && string.Equals(bundled.Pages[from].Text, text, StringComparison.Ordinal);
            int to = sameIndex ? from : FirstFreePageWithText(bundled.Pages, text, taken);
            if (to >= 0)
            {
                taken.Add(to);
                moves.Add(new RecordingMove(from, to));
            }
        }

        return moves;
    }

    private static int FirstFreePageWithText(IReadOnlyList<StoryPage> pages, string text, HashSet<int> taken)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            if (!taken.Contains(i) && string.Equals(pages[i].Text, text, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool SameCharacters(IReadOnlyList<CastMember> stored, IReadOnlyList<CastMember> bundled) =>
        stored.Select(c => c.CharacterId).Order(StringComparer.Ordinal).SequenceEqual(bundled.Select(c => c.CharacterId).Order(StringComparer.Ordinal));
}
