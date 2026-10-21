using System.Text;
using System.Text.Json;
using Microsoft.ML.Tokenizers;

namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>
/// The text side of a Marian model (Slice 75): SentencePiece splits the source into pieces
/// (<see cref="SentencePieceTokenizer"/> from Microsoft.ML.Tokenizers, MIT), and the model's shared
/// <c>vocab.json</c> maps each piece to its id, exactly as the Hugging Face <c>MarianTokenizer</c> does.
/// Decoding is the reverse lookup with the SentencePiece word marker turned back into a space.
/// </summary>
public sealed class MarianTokenizer
{
    private const char WordMarker = '▁';
    private const string UnknownPiece = "<unk>";
    private const string EndPiece = "</s>";

    private readonly Tokenizer _sentencePiece;
    private readonly Dictionary<string, int> _vocab;
    private readonly string[] _pieces;
    private readonly int _unknownId;

    public MarianTokenizer(Stream sentencePieceModel, IReadOnlyDictionary<string, int> vocabulary)
    {
        _sentencePiece = SentencePieceTokenizer.Create(sentencePieceModel, addBeginningOfSentence: false, addEndOfSentence: false);
        _vocab = new Dictionary<string, int>(vocabulary);
        _pieces = new string[vocabulary.Values.Max() + 1];
        Array.Fill(_pieces, string.Empty);
        foreach ((string piece, int id) in vocabulary)
        {
            _pieces[id] = piece;
        }

        _unknownId = _vocab.GetValueOrDefault(UnknownPiece);
        EndId = _vocab.GetValueOrDefault(EndPiece);
    }

    /// <summary>The id that ends every encoded source (and every generated sentence).</summary>
    public int EndId { get; }

    /// <summary>Parses a <c>vocab.json</c> (piece to id).</summary>
    public static Dictionary<string, int> ParseVocabulary(Stream json) =>
        JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? throw new FormatException("Empty vocab.json.");

    /// <summary>The model input ids for <paramref name="text"/>: the optional target token, the pieces, then the end id.</summary>
    public int[] Encode(string text, string? targetToken)
    {
        var ids = new List<int>();
        if (targetToken is not null && _vocab.TryGetValue(targetToken, out int tokenId))
        {
            ids.Add(tokenId);
        }

        foreach (EncodedToken token in _sentencePiece.EncodeToTokens(text, out _))
        {
            ids.Add(_vocab.GetValueOrDefault(token.Value, _unknownId));
        }

        ids.Add(EndId);
        return ids.ToArray();
    }

    /// <summary>The text of generated <paramref name="ids"/>: pieces joined, word markers as spaces, special tokens dropped.</summary>
    public string Decode(IEnumerable<int> ids)
    {
        var text = new StringBuilder();
        foreach (int id in ids)
        {
            if (id == EndId || id == _unknownId || (uint)id >= (uint)_pieces.Length)
            {
                continue;
            }

            text.Append(_pieces[id].Replace(WordMarker, ' '));
        }

        return text.ToString().Trim();
    }
}
