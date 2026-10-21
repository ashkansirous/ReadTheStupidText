namespace ReadTheStupidText.Documents;

/// <summary>A word with its box in page units, origin top-left (y grows downward).</summary>
public readonly record struct PositionedWord(string Text, double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public double CenterY => Top + Height / 2;
}
