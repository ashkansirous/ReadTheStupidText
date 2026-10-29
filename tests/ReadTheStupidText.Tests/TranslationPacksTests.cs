using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Infrastructure.Translation;

namespace ReadTheStupidText.Tests;

public class TranslationPacksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rtst-translation-" + Guid.NewGuid().ToString("N"));

    public TranslationPacksTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("fr", "en", "translation-fr-en")]
    [InlineData("en", "pt", "translation-en-pt")]
    [InlineData("ja", "ru", "translation-ja-en,translation-en-ru")]
    public void A_pair_needs_one_pack_with_english_at_an_end_and_two_otherwise(string source, string target, string expected) =>
        Assert.Equal(expected.Split(','), TranslationPacks.RequiredPackIds(source, target));

    [Theory]
    [InlineData("fa", "en")]
    [InlineData("en", "fa")]
    [InlineData("fr", "fa")]
    [InlineData("fr", "fr")]
    public void Pairs_with_no_usable_model_are_unsupported(string source, string target)
    {
        Assert.False(TranslationPacks.IsPairSupported(source, target));
        Assert.Empty(TranslationPacks.RequiredPackIds(source, target));
    }

    [Theory]
    [InlineData("da")]
    [InlineData("fi")]
    [InlineData("sv")]
    [InlineData("nb")]
    [InlineData("nl")]
    [InlineData("de")]
    public void The_nordic_and_dutch_languages_have_a_pack_each_way(string language)
    {
        Assert.Equal([$"translation-{language}-en"], TranslationPacks.RequiredPackIds(language, "en"));
        Assert.Equal([$"translation-en-{language}"], TranslationPacks.RequiredPackIds("en", language));
        Assert.True(TranslationPacks.CanServeTarget(language));
        Assert.Contains(language, TranslationPacks.Languages);
    }

    [Theory]
    [InlineData("sv", "nl", "translation-sv-en,translation-en-nl")]
    [InlineData("nb", "fr", "translation-nb-en,translation-en-fr")]
    [InlineData("ja", "da", "translation-ja-en,translation-en-da")]
    [InlineData("de", "fr", "translation-de-en,translation-en-fr")]
    public void A_pair_of_new_and_old_languages_pivots_through_english(string source, string target, string expected) =>
        Assert.Equal(expected.Split(','), TranslationPacks.RequiredPackIds(source, target));

    [Fact]
    public void Norwegian_is_nb_and_the_old_no_code_has_no_pack()
    {
        Assert.True(TranslationPacks.IsPairSupported("nb", "en"));
        Assert.True(TranslationPacks.IsPairSupported("en", "nb"));
        Assert.Equal("nb", TranslationFlow.NormalizeTag("no-NO"));
        Assert.Equal("nb", TranslationFlow.NormalizeTag("no"));
        Assert.False(TranslationPacks.IsPairSupported("nn", "en"));
    }

    [Fact]
    public void The_packs_workflow_table_has_one_complete_row_per_shipped_pair()
    {
        const int Columns = 11;
        var workflow = ReadPacksWorkflow();
        var block = System.Text.RegularExpressions.Regex.Match(workflow, @"<<'TABLE'\r?\n(.*?)\r?\n\s*TABLE", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(block.Success);
        var rows = block.Groups[1].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        Assert.All(rows, r => Assert.Equal(Columns, r.Length));
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r[Columns - 1])));
        Assert.Equal(ShippedTranslationPairs().Order(), rows.Select(r => r[0]).Order());
    }

    [Fact]
    public void Every_leg_in_the_tables_is_a_pair_the_packs_workflow_ships_and_the_reverse()
    {
        var shipped = ShippedTranslationPairs().Order().ToArray();
        var supported = TranslationPacks.Languages
            .Where(l => l != "en")
            .SelectMany(l => new[] { (Source: l, Target: "en"), (Source: "en", Target: l) })
            .Where(p => TranslationPacks.IsLegSupported(p.Source, p.Target))
            .Select(p => $"{p.Source}-{p.Target}")
            .Order()
            .ToArray();

        Assert.Equal(shipped, supported);
    }

    private static string[] ShippedTranslationPairs()
    {
        var match = System.Text.RegularExpressions.Regex.Match(ReadPacksWorkflow(), @"TRANSLATION_PAIRS:\s*'([^']+)'");
        Assert.True(match.Success);
        return match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ReadPacksWorkflow()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".github", "workflows", "packs.yml")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "packs.yml not found above the test output; run from a full checkout");
        return File.ReadAllText(Path.Combine(dir.FullName, ".github", "workflows", "packs.yml"));
    }

    [Fact]
    public void Japanese_translates_both_into_and_out_of_english()
    {
        Assert.True(TranslationPacks.IsPairSupported("ja", "en"));
        Assert.True(TranslationPacks.IsPairSupported("en", "ja"));
        Assert.True(TranslationPacks.IsPairSupported("fr", "ja"));
        Assert.Equal(["translation-fr-en", "translation-en-ja"], TranslationPacks.RequiredPackIds("fr", "ja"));
        Assert.True(TranslationPacks.IsPairSupported("ja", "fr"));
        Assert.DoesNotContain("fa", TranslationPacks.Languages);
    }

    [Fact]
    public void Availability_needs_an_installed_pack_with_its_spec()
    {
        Assert.False(TranslationPacks.AnyInstalled(TranslationPacks.FolderLookup(() => _root)));

        Directory.CreateDirectory(Path.Combine(_root, "translation-fr-en"));
        Assert.False(TranslationPacks.AnyInstalled(TranslationPacks.FolderLookup(() => _root)));

        File.WriteAllText(Path.Combine(_root, "translation-fr-en", TranslationPackFiles.Spec), "{}");
        Assert.True(TranslationPacks.AnyInstalled(TranslationPacks.FolderLookup(() => _root)));
    }

    [Fact]
    public async Task A_pack_the_lookup_calls_incomplete_is_not_ready()
    {
        InstallPack("fr", "en");
        using var subject = new PackTranslator(() => _root, null, _ => new FakeLeg("x", []), installedPath: _ => null);

        Assert.False(await subject.IsModelReadyAsync("fr", "en"));
        await Assert.ThrowsAsync<TranslationModelUnavailableException>(
            () => subject.TranslateAsync("Un.", "fr", "en", CancellationToken.None));
    }

    [Fact]
    public async Task A_replaced_pack_is_loaded_again()
    {
        InstallPack("fr", "en");
        var disposed = new List<string>();
        using var subject = new PackTranslator(() => _root, null, folder => new FakeLeg("fr", [], disposed));
        await subject.TranslateAsync("Un.", "fr", "en", CancellationToken.None);

        File.SetLastWriteTimeUtc(Path.Combine(TranslationPacks.PackFolder(_root, "fr", "en"), TranslationPackFiles.Spec), DateTime.UtcNow.AddMinutes(5));
        await subject.TranslateAsync("Deux.", "fr", "en", CancellationToken.None);

        Assert.Single(disposed);
    }

    [Fact]
    public async Task A_pivot_read_runs_both_legs_in_order_and_joins_sentences()
    {
        InstallPack("fr", "en");
        InstallPack("en", "ru");
        var calls = new List<string>();
        using var subject = new PackTranslator(() => _root, null, folder => new FakeLeg(Path.GetFileName(folder), calls));

        string result = await subject.TranslateAsync("Un. Deux.", "fr", "ru", CancellationToken.None);

        Assert.Equal("[translation-en-ru:[translation-fr-en:Un.]] [translation-en-ru:[translation-fr-en:Deux.]]", result);
        Assert.Equal(["translation-fr-en", "translation-fr-en", "translation-en-ru", "translation-en-ru"], calls);
    }

    [Fact]
    public async Task Chinese_output_is_joined_without_spaces()
    {
        InstallPack("en", "zh");
        using var subject = new PackTranslator(() => _root, null, folder => new FakeLeg("zh", []));

        string result = await subject.TranslateAsync("One. Two.", "en", "zh", CancellationToken.None);

        Assert.Equal("[zh:One.][zh:Two.]", result);
    }

    [Fact]
    public async Task A_missing_pack_raises_model_unavailable()
    {
        InstallPack("fr", "en");
        using var subject = new PackTranslator(() => _root, null, folder => new FakeLeg("x", []));

        Assert.False(await subject.IsModelReadyAsync("fr", "ru"));
        await Assert.ThrowsAsync<TranslationModelUnavailableException>(() =>
            subject.TranslateAsync("Un.", "fr", "ru", CancellationToken.None));
    }

    [Fact]
    public async Task An_unsupported_pair_raises_model_unavailable_and_is_not_ready()
    {
        using var subject = new PackTranslator(() => _root, null, folder => new FakeLeg("x", []));

        Assert.False(subject.SupportsPair("en", "fa"));
        Assert.False(await subject.IsModelReadyAsync("en", "fa"));
        await Assert.ThrowsAsync<TranslationModelUnavailableException>(() =>
            subject.TranslateAsync("One.", "en", "fa", CancellationToken.None));
    }

    [Fact]
    public async Task A_pack_that_will_not_load_raises_model_unavailable()
    {
        InstallPack("fr", "en");
        using var subject = new PackTranslator(() => _root, null, _ => throw new FormatException("bad spec"));

        await Assert.ThrowsAsync<TranslationModelUnavailableException>(() =>
            subject.TranslateAsync("Un.", "fr", "en", CancellationToken.None));
    }

    [Fact]
    public async Task Translation_stops_between_sentences_when_cancelled()
    {
        InstallPack("fr", "en");
        using var cts = new CancellationTokenSource();
        using var subject = new PackTranslator(() => _root, null, _ => new FakeLeg("x", [], onTranslate: cts.Cancel));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            subject.TranslateAsync("Un. Deux. Trois.", "fr", "en", cts.Token));
    }

    [Fact]
    public async Task At_most_two_legs_stay_loaded()
    {
        foreach (string source in new[] { "fr", "es", "ru" })
        {
            InstallPack(source, "en");
        }

        var disposed = new List<string>();
        using var subject = new PackTranslator(() => _root, null, folder => new FakeLeg(Path.GetFileName(folder), [], disposed));

        await subject.TranslateAsync("Un.", "fr", "en", CancellationToken.None);
        await subject.TranslateAsync("Uno.", "es", "en", CancellationToken.None);
        await subject.TranslateAsync("Odin.", "ru", "en", CancellationToken.None);

        Assert.Equal(["translation-fr-en"], disposed);
    }

    private void InstallPack(string source, string target)
    {
        string folder = TranslationPacks.PackFolder(_root, source, target);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, TranslationPackFiles.Spec), "{}");
    }

    private sealed class FakeLeg(string name, List<string> calls, List<string>? disposed = null, Action? onTranslate = null) : ILegTranslator
    {
        public string Translate(string sentence, CancellationToken cancellationToken)
        {
            calls.Add(name);
            onTranslate?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return $"[{name}:{sentence}]";
        }

        public void Dispose() => disposed?.Add(name);
    }
}
