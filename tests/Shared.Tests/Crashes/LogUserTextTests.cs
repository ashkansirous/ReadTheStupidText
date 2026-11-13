using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

/// <summary>Slice 227: a known user string sent through the read paths' logging never reaches the rolling log file, and so never a crash report's tail.</summary>
public sealed class LogUserTextTests : IDisposable
{
    private const string Secret = "my secret diary entry about Grandma";
    private const string SecretFile = "Grandma Secret Recipes.pdf";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rtst-usertext-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // a temp folder left behind is harmless
        }
    }

    [Fact]
    public async Task The_sentence_cache_path_logs_hashes_and_lengths_but_never_the_sentence_or_a_path_in_an_error()
    {
        var file = new RollingLogFile(_folder);
        var log = new RollingFileSystemLog(() => file);
        var source = new SentenceAudioSource(new SentenceAudioCache(new FailingStore()), log);
        SentenceCacheKey key = SentenceCacheTestData.Key(Secret);

        await source.GetAsync(key, _ => Task.FromResult(SentenceCacheTestData.Wav()), CancellationToken.None);

        string tail = file.Tail();
        Assert.Contains("cache miss", tail);
        Assert.Contains("cache store failed", tail);
        Assert.DoesNotContain(Secret, tail);
        Assert.DoesNotContain("diary", tail);
        Assert.DoesNotContain("Grandma", tail);
        Assert.DoesNotContain("/private/var", tail);
    }

    [Fact]
    public void A_file_is_logged_by_its_kind_never_its_name_and_an_error_message_loses_its_paths()
    {
        var file = new RollingLogFile(_folder);
        var log = new RollingFileSystemLog(() => file);

        log.Error($"failed to extract text from a {LogLineFormatter.FileKind($"/storage/emulated/0/{SecretFile}")} file", null,
            new FileNotFoundException($"Could not find file '/storage/emulated/0/{SecretFile}'."));

        string tail = file.Tail();
        Assert.Contains("a .pdf file", tail);
        Assert.Contains("System.IO.FileNotFoundException", tail);
        Assert.DoesNotContain("Grandma", tail);
        Assert.DoesNotContain("Recipes", tail);
    }

    [Theory]
    [InlineData("C:\\Users\\Me\\Documents\\Private Letter.docx", ".docx")]
    [InlineData("/tmp/NOTES", "no extension")]
    [InlineData(null, "no extension")]
    public void FileKind_is_the_extension_alone(string? path, string expected) => Assert.Equal(expected, LogLineFormatter.FileKind(path));

    [Fact]
    public void A_logged_line_is_one_line_and_never_longer_than_the_cap()
    {
        string line = LogLineFormatter.Format(DateTimeOffset.UtcNow, 'W', "a\r\nb\nc", 7, new InvalidOperationException(new string('z', 5000)));

        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.True(line.Length <= LogLineFormatter.MaxLineChars);
        Assert.Contains("[7]", line);
    }

    // A store whose write fails with a message that carries a path holding the user's sentence, as a real IOException can.
    private sealed class FailingStore : ISentenceStore
    {
        public IReadOnlyList<StoredSentence> List() => [];

        public Task<byte[]?> ReadAsync(string id) => Task.FromResult<byte[]?>(null);

        public Task<StoredSentence> WriteAsync(string id, byte[] wav) =>
            throw new IOException($"The disk is full writing '/private/var/mobile/{Secret}/{id}.wav'");

        public void Touch(string id)
        {
        }

        public void Delete(string id)
        {
        }
    }
}
