using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public sealed class PackVersionFoldersTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rtst-folders-");

    public void Dispose() => _root.Delete(recursive: true);

    private string Payload(string marker)
    {
        string payload = Path.Combine(_root.FullName, "scratch-" + marker);
        Directory.CreateDirectory(payload);
        File.WriteAllText(Path.Combine(payload, "pack.json"), marker);
        return payload;
    }

    private string PackFolder => Path.Combine(_root.FullName, "piper-fa");

    [Fact]
    public void A_pack_with_no_active_file_is_its_own_folder()
    {
        Directory.CreateDirectory(PackFolder);

        Assert.Equal(PackFolder, PackVersionFolders.Resolve(PackFolder));
    }

    [Fact]
    public void An_install_goes_into_its_own_version_folder_and_becomes_active()
    {
        PackVersionFolders.Install(Payload("one"), PackFolder, "1");

        Assert.Equal(Path.Combine(PackFolder, "v1"), PackVersionFolders.Resolve(PackFolder));
        Assert.Equal("one", File.ReadAllText(Path.Combine(PackVersionFolders.Resolve(PackFolder), "pack.json")));
    }

    [Fact]
    public void A_newer_version_is_installed_next_to_the_old_one_which_is_left_untouched()
    {
        PackVersionFolders.Install(Payload("one"), PackFolder, "1");
        using var held = File.OpenRead(Path.Combine(PackFolder, "v1", "pack.json")); // a reader holding the old model open

        PackVersionFolders.Install(Payload("two"), PackFolder, "2");

        Assert.Equal(Path.Combine(PackFolder, "v2"), PackVersionFolders.Resolve(PackFolder));
        Assert.True(File.Exists(Path.Combine(PackFolder, "v1", "pack.json")));
    }

    [Fact]
    public void Reinstalling_the_active_version_leaves_the_active_folder_alone()
    {
        PackVersionFolders.Install(Payload("one"), PackFolder, "1");
        using var held = File.OpenRead(Path.Combine(PackFolder, "v1", "pack.json"));

        PackVersionFolders.Install(Payload("rebuilt"), PackFolder, "1");

        Assert.Equal("one", File.ReadAllText(Path.Combine(PackFolder, "v1", "pack.json"), System.Text.Encoding.UTF8));
    }

    [Fact]
    public void A_complete_folder_newer_than_the_active_one_survives_the_clean_up()
    {
        PackVersionFolders.Install(Payload("one"), PackFolder, "1");
        Directory.Move(Payload("two"), Path.Combine(PackFolder, "v2")); // killed before the pointer was written

        PackVersionFolders.DeleteOldVersions(_root.FullName);

        Assert.True(Directory.Exists(Path.Combine(PackFolder, "v2")));
    }

    [Fact]
    public void Old_versions_and_unversioned_files_are_deleted_at_the_next_start()
    {
        Directory.CreateDirectory(PackFolder);
        File.WriteAllText(Path.Combine(PackFolder, "model.onnx"), "unversioned layout");
        PackVersionFolders.Install(Payload("one"), PackFolder, "1");
        PackVersionFolders.Install(Payload("two"), PackFolder, "2");

        PackVersionFolders.DeleteOldVersions(_root.FullName);

        Assert.Equal(["active", "v2"], Directory.EnumerateFileSystemEntries(PackFolder).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void An_old_version_still_held_open_is_kept_for_the_start_after()
    {
        PackVersionFolders.Install(Payload("one"), PackFolder, "1");
        PackVersionFolders.Install(Payload("two"), PackFolder, "2");
        using (File.OpenRead(Path.Combine(PackFolder, "v1", "pack.json")))
        {
            PackVersionFolders.DeleteOldVersions(_root.FullName);
            Assert.Equal(Path.Combine(PackFolder, "v2"), PackVersionFolders.Resolve(PackFolder));
        }

        PackVersionFolders.DeleteOldVersions(_root.FullName);

        Assert.False(Directory.Exists(Path.Combine(PackFolder, "v1")));
    }

    [Fact]
    public void A_pack_with_no_active_version_keeps_everything()
    {
        Directory.CreateDirectory(PackFolder);
        File.WriteAllText(Path.Combine(PackFolder, "model.onnx"), "unversioned layout");

        PackVersionFolders.DeleteOldVersions(_root.FullName);

        Assert.True(File.Exists(Path.Combine(PackFolder, "model.onnx")));
    }

    [Fact]
    public void An_active_file_naming_something_else_than_a_version_folder_is_ignored()
    {
        Directory.CreateDirectory(PackFolder);
        File.WriteAllText(Path.Combine(PackFolder, "active"), "../elsewhere");

        Assert.Equal(PackFolder, PackVersionFolders.Resolve(PackFolder));
    }

    [Theory]
    [InlineData(PackDownloadResult.HashMismatch, true)]
    [InlineData(PackDownloadResult.InvalidPack, true)]
    [InlineData(PackDownloadResult.Offline, false)]
    [InlineData(PackDownloadResult.Failed, false)]
    public void Only_a_rejected_download_is_remembered_as_failed(PackDownloadResult result, bool permanent) =>
        Assert.Equal(permanent, PackUpdateKey.IsPermanentFailure(result));

    [Fact]
    public void The_update_policy_reads_the_connections_once()
    {
        int enumerations = 0;
        IEnumerable<NetworkProfile> Profiles()
        {
            enumerations++;
            yield return NetworkProfile.WiFi;
        }

        Assert.True(PackUpdatePolicy.AllowsAutomaticUpdate(Profiles()));
        Assert.Equal(1, enumerations);
    }
}
