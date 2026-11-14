namespace DinoReader.Core;

/// <summary>Labels for an action sheet whose buttons map back to a choice (Slice 164).</summary>
public static class ChoiceLabels
{
    /// <summary>The label for a choice: its own name, or the name with a number when that would be
    /// misread as a label already taken or reserved (such as Cancel).</summary>
    public static string Unique(string name, IReadOnlyCollection<string> taken, string reserved)
    {
        string label = name;
        for (int n = 2; taken.Contains(label) || label == reserved; n++)
        {
            label = $"{name} ({n})";
        }

        return label;
    }
}
