namespace DinoReader.Core;

/// <summary>How the reading screen shows the story (Slice 151, Decision 156): only the picture, the flowing text with the sentence marked,
/// or the picture with the words on a panel over it. An older <c>story.json</c> that saved <c>Picture</c> meant the picture with the words,
/// so that name reads as <see cref="PictureAndText"/> (<see cref="StoryJson"/>); this member is stored as <c>PictureOnly</c>.</summary>
public enum ReadingMode
{
    Text,
    PictureOnly,
    PictureAndText,
}
