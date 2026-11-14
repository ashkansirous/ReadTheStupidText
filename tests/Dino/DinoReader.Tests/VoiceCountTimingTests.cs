using DinoReader.BookBuilder;

namespace DinoReader.Tests;

// Slice 297. ReadingSession and IntroWizardPage need the MAUI host, so their wiring is pinned on the source (the pattern of ReadingPauseStateTests).
public class VoiceCountTimingTests
{
	private static string Source(string file) => File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "src", "Dino", "DinoReader.App", file)).ReplaceLineEndings("\n");

	private static string Body(string source, string signature)
	{
		int start = source.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0, signature);
		int end = source.IndexOf("\n\t}\n", start, StringComparison.Ordinal);
		return source[start..end];
	}

	[Fact]
	public void A_read_that_starts_counts_no_voice_until_its_first_clip_plays()
	{
		string session = Source("ReadingSession.cs");

		string start = Body(session, "private async Task StartVoicedReadAsync(");
		Assert.DoesNotContain("CountVoiceOncePerBook(", start); // starting a read only records what to count
		Assert.Contains("_voiceCount.Begin(", start);

		string play = Body(session, "private bool TryPlayPremade(");
		Assert.Contains("CountVoiceOnFirstClip(read.ReadId);", play);
		int played = play.IndexOf("_player.Play(", StringComparison.Ordinal);
		int caught = play.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
		int counted = play.IndexOf("CountVoiceOnFirstClip(", StringComparison.Ordinal);
		Assert.True(played >= 0 && caught >= 0 && counted >= 0, "the body was cut short or reformatted");
		Assert.True(played < counted, "the count follows a Play that did not throw");
		Assert.True(caught < counted, "the failure path returns before the count");
	}

	[Fact]
	public void A_read_that_never_reaches_a_clip_counts_nothing()
	{
		var count = new PendingVoiceCount<string>();
		count.Begin(7, "en", "book");
		count.Clear(); // the read failed or was stopped before any audio

		Assert.Null(count.Take(7));
	}

	[Fact]
	public void The_pending_count_is_taken_once_and_only_by_its_own_read()
	{
		var count = new PendingVoiceCount<string>();
		count.Begin(7, "fa", "book");

		Assert.Null(count.Take(6));
		PendingVoice<string>? taken = count.Take(7);
		Assert.NotNull(taken);
		Assert.Equal("fa", taken.Reading); // the language the read started with
		Assert.Equal("book", taken.Payload);
		Assert.Null(count.Take(7)); // a second clip of the same read counts nothing
	}

	[Fact]
	public void A_newer_read_replaces_the_pending_count_of_an_older_one()
	{
		var count = new PendingVoiceCount<string>();
		count.Begin(1, "en", "old");
		count.Begin(2, "en", "new");

		Assert.Null(count.Take(1));
		Assert.Equal("new", count.Take(2)?.Payload);
	}

	[Fact]
	public void A_book_is_claimed_once_per_reading_language()
	{
		var count = new PendingVoiceCount<string>();

		Assert.True(count.ClaimBook("rex", "en"));
		Assert.False(count.ClaimBook("rex", "en"));
		Assert.True(count.ClaimBook("rex", "fa")); // another language is another count
		Assert.True(count.ClaimBook("zed", "fa"));
	}
}
