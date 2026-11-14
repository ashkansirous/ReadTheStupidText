using System.Buffers.Binary;
using System.Text.Json;
using Sirous.Reading.Application.Crashes;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

// Slice 243 items 2 and 6: the report facts (build, UUID, memory, state) and the Mach-O UUID reader.
public class CrashFactsTests
{
    private const long Mb = 1024L * 1024;
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0L, "<128")]
    [InlineData(127 * Mb, "<128")]
    [InlineData(128 * Mb, "128-256")]
    [InlineData(300 * Mb, "256-512")]
    [InlineData(900 * Mb, "512-1024")]
    [InlineData(1500 * Mb, "1024-2048")]
    [InlineData(5000 * Mb, "2048+")]
    public void Memory_sizes_fall_into_their_bucket(long bytes, string bucket) => Assert.Equal(bucket, CrashMemory.Bucket(bytes));

    [Fact]
    public void An_unreadable_memory_size_has_no_bucket() => Assert.Null(CrashMemory.Bucket(-1));

    [Fact]
    public void A_snapshot_adds_memory_and_state_and_keeps_the_build()
    {
        CrashFacts facts = CrashFacts.Snapshot(new CrashFacts("20261114.101", "ABCD"), 300 * Mb, 90 * Mb, inForeground: false);

        Assert.Equal(new CrashFacts("20261114.101", "ABCD", "256-512", "<128", CrashAppState.Background), facts);
    }

    [Fact]
    public void A_snapshot_with_nothing_readable_leaves_the_fields_null()
    {
        CrashFacts facts = CrashFacts.Snapshot(null, -1, -1, null);

        Assert.Equal(new CrashFacts(), facts);
    }

    [Fact]
    public void The_facts_reach_the_report_and_survive_json()
    {
        var facts = new CrashFacts("20261114.101", "0123456789ABCDEF0123456789ABCDEF", "256-512", "128-256", CrashAppState.Foreground);
        var context = new CrashContext(CrashApp.Dino, "ios", "1.90.0", InstallChannel.TestFlight, null, null, facts);

        CrashReport? report = CrashReportBuilder.FromDescription(context, CrashKind.Native, "t", "m", "", "", Now);
        string json = CrashReportJson.ToJson(report!);
        using JsonDocument doc = JsonDocument.Parse(json);

        Assert.Equal(facts, report!.Facts);
        Assert.Equal("0123456789ABCDEF0123456789ABCDEF", doc.RootElement.GetProperty("binaryUuid").GetString());
        Assert.Equal("20261114.101", doc.RootElement.GetProperty("build").GetString());
        Assert.Equal("foreground", doc.RootElement.GetProperty("appState").GetString());
        Assert.Equal(report, CrashReportJson.Parse(json));
    }

    [Fact]
    public void A_report_without_facts_writes_none_of_the_fields()
    {
        var report = new CrashReport("dino", "ios", "1.90.0", "store", "managed", "T", "m", "", "", "2026-11-14T10:00Z");

        string json = CrashReportJson.ToJson(report);

        Assert.DoesNotContain("binaryUuid", json);
        Assert.Null(CrashReportJson.Parse(json)!.Facts);
    }

    [Fact]
    public void The_uuid_is_read_from_a_thin_macho()
    {
        byte[] uuid = Enumerable.Range(1, 16).Select(i => (byte)(i * 11)).ToArray();

        Assert.Equal(Convert.ToHexString(uuid), MachOUuid.Read(new MemoryStream(Thin(uuid))));
    }

    [Fact]
    public void The_uuid_is_read_from_the_first_slice_of_a_fat_macho()
    {
        byte[] uuid = Enumerable.Range(1, 16).Select(i => (byte)(i * 7)).ToArray();
        byte[] thin = Thin(uuid);
        byte[] fat = new byte[4096 + thin.Length];
        BinaryPrimitives.WriteUInt32BigEndian(fat, 0xCAFEBABE);
        BinaryPrimitives.WriteUInt32BigEndian(fat.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(fat.AsSpan(8 + 8), 4096);
        thin.CopyTo(fat, 4096);

        Assert.Equal(Convert.ToHexString(uuid), MachOUuid.Read(new MemoryStream(fat)));
    }

    [Fact]
    public void A_file_that_is_not_a_macho_has_no_uuid()
    {
        Assert.Null(MachOUuid.Read(new MemoryStream([1, 2, 3])));
        Assert.Null(MachOUuid.Read(new MemoryStream(new byte[64])));
        Assert.Null(MachOUuid.Read(new MemoryStream([])));
    }

    [Fact]
    public void A_user_report_carries_the_session_and_the_facts_of_the_running_launch()
    {
        var facts = new CrashFacts("7", "ABCD", "256-512", "<128", CrashAppState.Foreground);
        var context = new CrashContext(CrashApp.Dino, "ios", "1.90.0", InstallChannel.Store, null, "SESSION", facts);

        CrashReport? report = Sirous.Reading.Mobile.Shared.Crashes.UserReportBuilder.Build(context, "K7QX2MRD", string.Empty, Now);

        Assert.Equal(facts, report!.Facts);
        Assert.Equal("SESSION", report.Session);
    }

    [Fact]
    public void A_report_about_an_earlier_launch_takes_the_facts_of_that_launch_and_none_of_the_running_ones()
    {
        var now = new CrashContext(CrashApp.Dino, "ios", "1.1.0", InstallChannel.Store, null, "SESSION", new CrashFacts("110", "BBBB", "256-512", "<128", CrashAppState.Foreground));

        CrashContext about = LaunchFacts.About(now, new LaunchFacts("1.0.0", "100", "AAAA")); // crashed on A, delivered on B

        Assert.Equal(("1.0.0", new CrashFacts("100", "AAAA"), null), (about.Version, about.Facts, about.Session));
    }

    [Fact]
    public void A_report_about_a_launch_with_no_known_facts_has_no_version_build_or_uuid()
    {
        var now = new CrashContext(CrashApp.Dino, "ios", "1.2.0", InstallChannel.Store, null, "SESSION", new CrashFacts("120", "CCCC", "256-512", "<128", CrashAppState.Foreground));

        CrashContext about = LaunchFacts.About(now, null);

        Assert.Equal((null, null, null), (about.Version, about.Facts, about.Session));
    }

    [Fact]
    public void The_run_marker_round_trips_a_launchs_facts_and_a_missing_field_stays_null()
    {
        Assert.Equal(new LaunchFacts("1.0.0", "100", "AAAA"), RunMarker.Parse(RunMarker.Format(new LaunchFacts("1.0.0", "100", "AAAA"))));
        Assert.Equal(new LaunchFacts("1.0.0", "100", null), RunMarker.Parse(RunMarker.Format(new LaunchFacts("1.0.0", "100", null))));
        Assert.Equal(new LaunchFacts("1.0.0", null, null), RunMarker.Parse(RunMarker.Format(new LaunchFacts("1.0.0", null, null)) + "\r\n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t\t")]
    [InlineData("just one field")]
    [InlineData("1.0\t2\t3\t4")]
    public void An_empty_or_damaged_run_marker_has_no_facts(string? text) => Assert.Null(RunMarker.Parse(text));

    [Fact]
    public void An_unclean_exit_carries_the_build_its_own_marker_was_written_with()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"marker-{Guid.NewGuid():N}");
        try
        {
            var store = new PendingCrashStore(folder);
            store.MarkRunning(new LaunchFacts("1.0.0", "100", "AAAA")); // launch A, which then crashes

            UncleanExit? exit = store.TakeUncleanExit(); // read by launch B, which is a different build

            Assert.Equal(new LaunchFacts("1.0.0", "100", "AAAA"), exit!.Launch);
            var nowB = new CrashContext(CrashApp.Dino, "ios", "1.1.0", InstallChannel.Store, null, null, new CrashFacts("110", "BBBB"));
            Assert.Equal(("1.0.0", new CrashFacts("100", "AAAA")), (LaunchFacts.About(nowB, exit.Launch).Version, LaunchFacts.About(nowB, exit.Launch).Facts));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void An_unclean_exit_whose_marker_held_no_facts_has_none_rather_than_the_current_ones()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"marker-{Guid.NewGuid():N}");
        try
        {
            var store = new PendingCrashStore(folder);
            store.MarkRunning();

            UncleanExit? exit = store.TakeUncleanExit();

            Assert.NotNull(exit);
            Assert.Null(exit.Launch);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private const string MetricKitTree = """
        {"callStackTree":{"callStackPerThread":true,"callStacks":[{"threadAttributed":true,"callStackRootFrames":[
        {"binaryUUID":"77A62F2E-8212-30F3-84C1-E8497440ACF8","binaryName":"libdyld.dylib","address":1,"subFrames":[
        {"binaryUUID":"70b89f27-1634-3580-a695-57cdb41d7743","binaryName":"DinoReader","address":2,"subFrames":[]}]}]}]}}
        """;

    [Fact]
    public void A_metrickit_diagnostics_own_call_stack_names_the_uuid_of_the_app_binary()
    {
        Assert.Equal("70B89F2716343580A69557CDB41D7743",MetricKitBinaries.UuidOf(MetricKitTree, "DinoReader"));
        Assert.Equal("77A62F2E821230F384C1E8497440ACF8", MetricKitBinaries.UuidOf(MetricKitTree, "libdyld.dylib"));
    }

    [Theory]
    [InlineData(null, "DinoReader")]
    [InlineData("", "DinoReader")]
    [InlineData("not json", "DinoReader")]
    [InlineData("{\"callStackTree\":{\"callStacks\":[]}}", "DinoReader")]
    [InlineData("{\"binaryName\":\"DinoReader\",\"binaryUUID\":\"short\"}", "DinoReader")]
    public void A_diagnostic_without_the_app_binary_gives_no_uuid(string? json, string name) => Assert.Null(MetricKitBinaries.UuidOf(json, name));

    [Fact]
    public void A_diagnostic_is_not_given_another_binarys_uuid_when_the_name_is_unknown() => Assert.Null(MetricKitBinaries.UuidOf(MetricKitTree, null));

    // A 64-bit little-endian header, one other load command (LC_SEGMENT_64-sized filler) and then LC_UUID.
    private static byte[] Thin(byte[] uuid)
    {
        const int filler = 24;
        const int uuidCommand = 24;
        byte[] file = new byte[32 + filler + uuidCommand];
        BinaryPrimitives.WriteUInt32LittleEndian(file, 0xFEEDFACF);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(20), filler + uuidCommand);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(32), 0x19);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(36), filler);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(32 + filler), 0x1B);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(32 + filler + 4), uuidCommand);
        uuid.CopyTo(file, 32 + filler + 8);
        return file;
    }
}
