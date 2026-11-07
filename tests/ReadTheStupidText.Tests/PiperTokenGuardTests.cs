using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

// Slice 303 item 4: the engine-build guard gates on the Piper pack ids, not on the VITS kind.
public class PiperTokenGuardTests : IDisposable
{
    private const string BadTokens = "_ 0\n^ 1\n$ 2\naɪ 3\n";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rtst-piper-guard-" + Guid.NewGuid().ToString("N"));

    public PiperTokenGuardTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(PackConstants.NorwegianPackId)]
    [InlineData(PackConstants.PersianPackId)]
    public void A_piper_pack_with_a_bad_tokens_file_is_refused(string packId)
    {
        VoiceModelPaths paths = WithTokens(VoiceEngineKind.Vits, packId, BadTokens);

        Assert.True(PiperTokenGuard.AppliesTo(paths));
        Assert.NotNull(PiperTokenGuard.Problem(paths));
        Assert.Throws<InvalidDataException>(() => PiperTokenGuard.Require(paths));
    }

    [Fact]
    public void A_non_piper_vits_model_needs_no_tokens_file()
    {
        VoiceModelPaths withBad = WithTokens(VoiceEngineKind.Vits, "other-vits", BadTokens);
        var noFile = new VoiceModelPaths(Path.Combine(_root, "empty"), VoiceEngineKind.Vits, PackId: "other-vits");

        Assert.False(PiperTokenGuard.AppliesTo(withBad));
        PiperTokenGuard.Require(withBad);
        PiperTokenGuard.Require(noFile);
    }

    [Fact]
    public void A_vits_voice_with_no_pack_id_is_still_checked()
    {
        VoiceModelPaths paths = WithTokens(VoiceEngineKind.Vits, "", BadTokens);

        Assert.True(PiperTokenGuard.AppliesTo(paths));
        Assert.Throws<InvalidDataException>(() => PiperTokenGuard.Require(paths));
    }

    [Fact]
    public void A_tokens_report_is_not_hidden_by_an_earlier_report_for_the_same_folder()
    {
        string folder = Path.Combine(_root, "shared-folder");

        Assert.True(PackVersionFolders.FirstReportFor(folder));
        Assert.True(PackVersionFolders.FirstTokensReportFor(folder));
        Assert.False(PackVersionFolders.FirstTokensReportFor(folder));
    }

    [Fact]
    public void A_piper_pack_with_a_sound_tokens_file_passes()
    {
        VoiceModelPaths paths = WithTokens(VoiceEngineKind.Vits, PackConstants.NorwegianPackId, "_ 0\n^ 1\n$ 2\na 3\n");

        Assert.Null(PiperTokenGuard.Problem(paths));
        PiperTokenGuard.Require(paths);
    }

    [Fact]
    public void Other_engine_kinds_are_never_checked()
    {
        VoiceModelPaths paths = WithTokens(VoiceEngineKind.Melo, PackConstants.NorwegianPackId, BadTokens);

        Assert.False(PiperTokenGuard.AppliesTo(paths));
        PiperTokenGuard.Require(paths);
    }

    [Fact]
    public void The_installed_catalog_stamps_the_pack_id_on_the_paths()
    {
        var manifest = new PackManifest(
            PackConstants.NorwegianPackId, PackKind.Voice, "nb", "2", "sherpa-onnx", ["voices/nvcc/tokens.txt"],
            VoiceEngineKind.Vits, [new PackVoice("piper:nb-knn", "KNN", 0, "voices/nvcc")]);

        VoiceModelPaths? paths = InstalledVoiceCatalog.Resolve([new InstalledVoicePack(_root, manifest)], "nb");

        Assert.Equal(PackConstants.NorwegianPackId, paths?.PackId);
    }

    private VoiceModelPaths WithTokens(VoiceEngineKind kind, string packId, string tokens)
    {
        File.WriteAllText(Path.Combine(_root, VitsFiles.Tokens), tokens);
        return new VoiceModelPaths(_root, kind, PackId: packId);
    }
}
