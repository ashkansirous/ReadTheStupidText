using System.Text;
using ReadTheStupidText.Infrastructure.Translation;

namespace ReadTheStupidText.Tests;

public class MarianTokenizerTests
{
    private const char Marker = '\u2581';

    private static readonly Dictionary<string, int> Vocab = new()
    {
        ["</s>"] = 0,
        ["<unk>"] = 1,
        [Marker + "hello"] = 2,
        [Marker + "world"] = 3,
        [">>pt<<"] = 4,
        ["!"] = 5,
    };

    [Fact]
    public void Known_words_become_vocabulary_ids_followed_by_the_end_id()
    {
        MarianTokenizer tokenizer = Create();

        Assert.Equal([2, 3, 0], tokenizer.Encode("hello world", null));
    }

    [Fact]
    public void A_target_token_leads_the_ids()
    {
        MarianTokenizer tokenizer = Create();

        Assert.Equal([4, 2, 0], tokenizer.Encode("hello", ">>pt<<"));
    }

    [Fact]
    public void Decoding_round_trips_the_text_and_drops_special_tokens()
    {
        MarianTokenizer tokenizer = Create();

        int[] ids = tokenizer.Encode("hello world", null);

        Assert.Equal("hello world", tokenizer.Decode(ids));
        Assert.Equal("hello world", tokenizer.Decode([1, .. ids, 99999]));
    }

    [Fact]
    public void A_piece_missing_from_the_vocabulary_becomes_the_unknown_id()
    {
        MarianTokenizer tokenizer = Create();

        Assert.Contains(1, tokenizer.Encode("hello rld", null));
    }

    [Fact]
    public void The_vocabulary_json_parses()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"</s>\":0,\"\\u2581the\":4}"));

        Dictionary<string, int> vocab = MarianTokenizer.ParseVocabulary(stream);

        Assert.Equal(4, vocab[Marker + "the"]);
    }

    private static MarianTokenizer Create() => new(new MemoryStream(TinyUnigramModel()), Vocab);

    // A hand-built SentencePiece ModelProto (unigram): the word pieces above plus single letters so any
    // lowercase text segments, and the three special pieces. Field numbers are from sentencepiece_model.proto.
    private static byte[] TinyUnigramModel()
    {
        using var model = new MemoryStream();
        foreach ((string piece, float score, int type) in Pieces())
        {
            WriteField(model, 1, PieceMessage(piece, score, type));
        }

        WriteField(model, 2, [.. Tag(3, 0), 1]); // trainer_spec.model_type = UNIGRAM
        WriteField(model, 3, [.. Tag(1, 2), 8, .. Encoding.UTF8.GetBytes("identity")]); // normalizer_spec.name
        return model.ToArray();
    }

    private static IEnumerable<(string Piece, float Score, int Type)> Pieces()
    {
        yield return ("<unk>", 0f, 2);
        yield return ("<s>", 0f, 3);
        yield return ("</s>", 0f, 3);
        yield return (Marker + "hello", -1f, 1);
        yield return (Marker + "world", -1f, 1);
        yield return (Marker.ToString(), -4f, 1);
        foreach (char letter in "helowrd!")
        {
            yield return (letter.ToString(), -5f, 1);
        }
    }

    private static byte[] PieceMessage(string piece, float score, int type)
    {
        byte[] text = Encoding.UTF8.GetBytes(piece);
        return
        [
            .. Tag(1, 2), .. Varint(text.Length), .. text,
            .. Tag(2, 5), .. BitConverter.GetBytes(score),
            .. Tag(3, 0), .. Varint(type),
        ];
    }

    private static void WriteField(Stream stream, int field, byte[] payload)
    {
        stream.Write([.. Tag(field, 2), .. Varint(payload.Length), .. payload]);
    }

    private static byte[] Tag(int field, int wireType) => Varint((field << 3) | wireType);

    private static byte[] Varint(int value)
    {
        var bytes = new List<byte>();
        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }

        bytes.Add((byte)value);
        return bytes.ToArray();
    }
}
