using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 375 D (review round 6 #4): a state file that cannot be read is "not read" (null), never an empty state that the next write would save over everything.</summary>
public sealed class HighResStateFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rtst-hr-state-" + Guid.NewGuid().ToString("N"));

    public HighResStateFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string File_(string name) => Path.Combine(_folder, name);

    [Fact]
    public void A_missing_file_is_an_empty_map()
    {
        Dictionary<string, int>? map = HighResStateFile.ReadMap(File_("none.json"));

        Assert.NotNull(map);
        Assert.Empty(map);
    }

    [Fact]
    public void A_corrupt_file_is_an_empty_map()
    {
        string path = File_("bad.json");
        File.WriteAllText(path, "{not json");

        Assert.Empty(HighResStateFile.ReadMap(path)!);
    }

    [Fact]
    public void A_write_round_trips_and_leaves_no_part_file()
    {
        string path = File_("ok.json");

        Assert.True(HighResStateFile.WriteAtomic(path, "{\"A\":3}"));

        Assert.Equal(3, HighResStateFile.ReadMap(path)!["a"]);
        Assert.False(File.Exists(path + ".part"));
    }

    [Fact]
    public void A_file_that_cannot_be_read_is_null_not_empty_and_keeps_its_content()
    {
        string path = File_("locked.json");
        File.WriteAllText(path, "{\"A\":3}");

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Null(HighResStateFile.ReadMap(path));
            Assert.Null(HighResStateFile.ReadText(path));
        }

        Assert.Equal(3, HighResStateFile.ReadMap(path)!["A"]);
    }

    [Fact]
    public void A_write_to_a_locked_file_returns_false_and_the_old_content_stays()
    {
        string path = File_("locked-write.json");
        File.WriteAllText(path, "{\"A\":3}");

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(HighResStateFile.WriteAtomic(path, "{}"));
        }

        Assert.Equal(3, HighResStateFile.ReadMap(path)!["A"]);
    }
}
