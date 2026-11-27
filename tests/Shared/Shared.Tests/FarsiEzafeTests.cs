using Sirous.Reading.Domain.Reading;
using Sirous.Reading.Application.Reading;

namespace ReadTheStupidText.Tests;

// Slice 342: the ezafe override table. The kasra on "حال" makes espeak say "hale shoma" (probe, Slice 342).
public class FarsiEzafeTests
{
    private const string Kasra = "ِ";

    [Fact]
    public void Forced_pair_gets_the_kasra()
    {
        Assert.Equal("سلام، حال" + Kasra + " شما چطور است؟", FarsiEzafe.Apply("سلام، حال شما چطور است؟"));
    }

    [Fact]
    public void Every_forced_occurrence_is_marked()
    {
        Assert.Equal("حال" + Kasra + " شما و حال" + Kasra + " شما", FarsiEzafe.Apply("حال شما و حال شما"));
    }

    [Fact]
    public void Already_marked_text_is_left_alone()
    {
        string marked = "حال" + Kasra + " شما";
        Assert.Same(marked, FarsiEzafe.Apply(marked));
    }

    [Theory]
    [InlineData("من هر صبح چای می‌نوشم و کتاب می‌خوانم.")]
    [InlineData("علی ساعت هفت و نیم به مدرسه رفت.")]
    [InlineData("حال خوب است")]
    [InlineData("شما حال")]
    [InlineData("")]
    public void Text_with_no_pair_is_the_same_instance(string text)
    {
        Assert.Same(text, FarsiEzafe.Apply(text));
    }

    [Theory]
    [InlineData("بیحال شما")]
    [InlineData("حالا شما")]
    [InlineData("حال‌شما")]
    public void Only_whole_words_match(string text)
    {
        Assert.Same(text, FarsiEzafe.Apply(text));
    }

    [Fact]
    public void Punctuation_between_the_words_stops_the_match()
    {
        const string text = "حال، شما";
        Assert.Same(text, FarsiEzafe.Apply(text));
    }

    // Slice 343: the extra-ezafe pairs get an Arabic comma after the first word (third listen).
    [Theory]
    [InlineData("آن صبح یک فنجان", "آن صبح، یک فنجان")]
    [InlineData("بیست و پنج دفتر خرید.", "بیست و پنج دفتر، خرید.")]
    [InlineData("روز صبح بود", "روز، صبح بود")]
    [InlineData("صبح یک", "صبح، یک")]
    [InlineData("علی دفتر خرید", "علی دفتر، خرید")]
    [InlineData("او دفتر خرید !", "او دفتر، خرید !")]
    [InlineData("او دفتر خرید؟ بله", "او دفتر، خرید؟ بله")]
    [InlineData("او دفتر خرید، و رفت", "او دفتر، خرید، و رفت")]
    [InlineData("دفتر خرید: سه", "دفتر، خرید: سه")]
    [InlineData("دفتر خرید؛ سه", "دفتر، خرید؛ سه")]
    [InlineData("دفتر خرید   ", "دفتر، خرید   ")]
    [InlineData("دفتر خرید\nسه کتاب", "دفتر، خرید\nسه کتاب")]
    [InlineData("دفتر خرید\r\nسه کتاب", "دفتر، خرید\r\nسه کتاب")]
    [InlineData("او گفت: «بیست و پنج دفتر خرید»", "او گفت: «بیست و پنج دفتر، خرید»")]
    [InlineData("(دفتر خرید) ، سه", "(دفتر، خرید) ، سه")]
    [InlineData("\"دفتر خرید\".", "\"دفتر، خرید\".")]
    [InlineData("دفتر خرید» ", "دفتر، خرید» ")]
    [InlineData("دفتر خرید… سه", "دفتر، خرید… سه")]
    [InlineData("دفتر خرید»… سه", "دفتر، خرید»… سه")]
    public void Comma_pairs_get_the_comma_after_the_first_word(string text, string expected)
    {
        Assert.Equal(expected, FarsiEzafe.Apply(text));
    }

    [Fact]
    public void A_chain_of_comma_pairs_gets_each_comma()
    {
        Assert.Equal("من هر روز، صبح، یک فنجان", FarsiEzafe.Apply("من هر روز صبح یک فنجان"));
    }

    [Theory]
    [InlineData("صبح دو")]
    [InlineData("یک صبح")]
    [InlineData("صبح خرید")]
    [InlineData("روز یک")]
    [InlineData("دفتر یک")]
    [InlineData("خرید دفتر")]
    [InlineData("صبحانه یک")]
    [InlineData("صبح یکشنبه")]
    [InlineData("دفترچه خرید")]
    [InlineData("صبح‌یک")]
    [InlineData("صبح، یک")]
    [InlineData("صبح. یک")]
    [InlineData("صبح یَک")]
    [InlineData("دفتر خرید را باز کرد.")]
    [InlineData("دفتر خرید و رفت")]
    [InlineData("«دفتر خرید» را باز کرد")]
    [InlineData("او دفتر خرید جدید داشت")]
    [InlineData("دفتر\nخرید")]
    [InlineData("دفتر\r\nخرید")]
    public void Near_misses_of_the_comma_pairs_are_the_same_instance(string text)
    {
        Assert.Same(text, FarsiEzafe.Apply(text));
    }

    [Fact]
    public void Comma_and_kasra_pairs_in_one_text_are_both_applied_and_nothing_else_changes()
    {
        Assert.Equal("حال" + Kasra + " شما، صبح، یک و خوب است", FarsiEzafe.Apply("حال شما، صبح یک و خوب است"));
    }

    [Fact]
    public void The_comma_pairs_only_apply_for_the_Farsi_Piper_model()
    {
        const string text = "صبح یک";
        var farsi = new VoiceModelPaths("x", VoiceEngineKind.Vits, VoiceChoices.PersianLanguageCode);
        var norwegian = new VoiceModelPaths("x", VoiceEngineKind.Vits, "nb");

        Assert.Equal("صبح، یک", FarsiEzafe.ForModel(farsi, text));
        Assert.Same(text, FarsiEzafe.ForModel(norwegian, text));
    }

    [Fact]
    public void Non_Farsi_text_is_untouched()
    {
        const string text = "How is your hal shoma today?";
        Assert.Same(text, FarsiEzafe.Apply(text));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("  ")]
    public void Any_whitespace_between_the_words_counts_and_is_kept(string separator)
    {
        Assert.Equal("حال" + Kasra + separator + "شما", FarsiEzafe.Apply("حال" + separator + "شما"));
    }

    [Fact]
    public void Pair_at_the_start_and_end_of_the_string_is_marked()
    {
        Assert.Equal("حال" + Kasra + " شما", FarsiEzafe.Apply("حال شما"));
        Assert.Equal("خوب حال" + Kasra + " شما", FarsiEzafe.Apply("خوب حال شما"));
        Assert.Equal("حال" + Kasra + " شما خوب", FarsiEzafe.Apply("حال شما خوب"));
    }

    [Fact]
    public void A_word_with_another_diacritic_is_not_the_table_word()
    {
        const string text = "حالَ شما";
        Assert.Same(text, FarsiEzafe.Apply(text));
    }

    [Fact]
    public void The_cache_key_text_is_the_processed_text_and_unchanged_sentences_keep_their_text()
    {
        var farsi = new VoiceModelPaths("x", VoiceEngineKind.Vits, VoiceChoices.PersianLanguageCode);
        const string changed = "سلام، حال شما";
        const string unchanged = "سلام، خوب هستید";

        Assert.NotEqual(changed, FarsiEzafe.ForModel(farsi, changed));
        Assert.Same(unchanged, FarsiEzafe.ForModel(farsi, unchanged));
    }

    [Fact]
    public void Only_the_Farsi_Piper_model_is_steered()
    {
        const string text = "حال شما";
        var farsi = new VoiceModelPaths("x", VoiceEngineKind.Vits, VoiceChoices.PersianLanguageCode);
        var norwegian = new VoiceModelPaths("x", VoiceEngineKind.Vits, "nb");
        var supertonic = new VoiceModelPaths("x", VoiceEngineKind.Supertonic, VoiceChoices.PersianLanguageCode);

        Assert.Equal("حال" + Kasra + " شما", FarsiEzafe.ForModel(farsi, text));
        Assert.Same(text, FarsiEzafe.ForModel(norwegian, text));
        Assert.Same(text, FarsiEzafe.ForModel(supertonic, text));
    }
}
