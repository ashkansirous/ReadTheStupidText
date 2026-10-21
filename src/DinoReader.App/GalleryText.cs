namespace DinoReader;

/// <summary>The gallery and grown-up screens' copy that is composed from numbers or names (Slice 61 new strings, English).</summary>
public static class GalleryText
{
	public static string Cast(int count) => $"Cast · {count}";

	public static string Characters(int count) => $"{count} characters";

	public static string PagesSummary(int count) => $"{count} · reorder, add, remove";

	public static string DeleteTitle(string title) => $"Delete “{title}”?";

	public static string PagesRead(int pages) => pages == 1 ? "1 page read" : $"{pages} pages read";

	public static string CharactersFound(int characters) => characters == 1 ? "1 character found" : $"{characters} characters found";

	public static string ImportSummary(int pages, int characters) => $"{PagesRead(pages)} · {CharactersFound(characters)}";
}
