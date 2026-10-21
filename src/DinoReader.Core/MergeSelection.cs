namespace DinoReader.Core;

/// <summary>
/// Which two characters the merge sheet has ticked, and which of them keeps the name (Slice 65).
/// At most two can be ticked: a third tick replaces the oldest. The kept one defaults to the
/// first one ticked.
/// </summary>
public sealed class MergeSelection
{
    private const int Needed = 2;

    private readonly List<string> _picked = [];
    private string? _keep;

    public IReadOnlyList<string> Picked => _picked;

    public bool IsComplete => _picked.Count == Needed;

    /// <summary>The character that keeps its name and voice, once two are ticked.</summary>
    public string? KeepId => IsComplete ? _keep ?? _picked[0] : null;

    /// <summary>The character that joins the kept one, once two are ticked.</summary>
    public string? DropId => IsComplete ? _picked.First(id => id != KeepId) : null;

    public bool Contains(string characterId) => _picked.Contains(characterId);

    /// <summary>Ticks an unticked character, unticks a ticked one.</summary>
    public void Toggle(string characterId)
    {
        if (!_picked.Remove(characterId))
        {
            _picked.Add(characterId);
        }

        if (_picked.Count > Needed)
        {
            _picked.RemoveAt(0);
        }

        if (_keep is not null && !_picked.Contains(_keep))
        {
            _keep = null;
        }
    }

    /// <summary>Chooses which ticked character keeps the name; a character that is not ticked is ignored.</summary>
    public void Keep(string characterId)
    {
        if (_picked.Contains(characterId))
        {
            _keep = characterId;
        }
    }
}
