namespace ReadTheStupidText.Mobile;

/// <summary>
/// The order rule for opening the reader page (Slice 304): the wait view must be set up before the first text is
/// shown, because setting the text fires TextChanged, which renders the player and reads the wait parts. The page
/// passes both steps here, so the order lives in one place. Pure BCL, no MAUI types, so the unit tests compile
/// this file directly.
/// </summary>
public static class ReaderOpenOrder
{
    public static void Open(Action initWait, Action showText)
    {
        initWait();
        showText();
    }
}
