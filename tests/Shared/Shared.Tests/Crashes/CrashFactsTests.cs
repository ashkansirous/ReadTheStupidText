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

    private static readonly DateTimeOffset LaunchA = new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaunchB = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static void WithFolder(Action<string> test)
    {
        string folder = Path.Combine(Path.GetTempPath(), $"facts-{Guid.NewGuid():N}");
        try
        {
            test(folder);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Fact]
    public void The_facts_file_keeps_the_previous_launch_as_prev_and_the_new_one_as_current()
    {
        WithFolder(folder =>
        {
            var file = new LaunchFactsFile(folder);
            file.Begin(new LaunchFacts("1.0.0", "100", null), LaunchA); // launch A starts, its UUID arrives later
            file.Write(new LaunchFacts("1.0.0", "100", "AAAA", LaunchA));

            file.Begin(new LaunchFacts("1.1.0", "110", null), LaunchB); // launch B starts

            Assert.Equal(new LaunchFacts("1.0.0", "100", "AAAA", LaunchA), file.ReadPrevious());
            Assert.Equal(new LaunchFacts("1.1.0", "110", null, LaunchB), file.ReadCurrent());
        });
    }

    [Fact]
    public void A_background_only_launch_does_not_push_the_last_foreground_launchs_facts_out_of_prev()
    {
        WithFolder(folder =>
        {
            var file = new LaunchFactsFile(folder);
            file.Begin(new LaunchFacts("1.0.0", "100", "AAAA"), LaunchA);
            file.Begin(new LaunchFacts("1.0.1", "101", "BBBB"), LaunchA.AddHours(1)); // A becomes prev (no prev yet)
            file.Write(new LaunchFacts("1.0.1", "101", "BBBB", LaunchA.AddHours(1), true)); // B reached the foreground, then crashed

            file.Begin(new LaunchFacts("1.0.2", "102", "CCCC"), LaunchA.AddHours(2)); // C: background only, B is prev
            file.Begin(new LaunchFacts("1.1.0", "110", "DDDD"), LaunchA.AddHours(3)); // D: C never reached the foreground, B stays prev

            Assert.Equal("101", file.ReadPrevious()!.Build);
            Assert.Equal("110", file.ReadCurrent()!.Build);
        });
    }

    [Fact]
    public void A_diagnostic_with_only_a_period_end_and_no_facts_gets_no_previous_launch_facts()
    {
        var previous = new LaunchFacts("1.0.0", "100", "AAAA", LaunchA);

        Assert.Null(LaunchFactsChoice.ForNativeExit(null, null, previous, null));
    }

    [Fact]
    public void A_uuid_write_changes_only_the_current_file()
    {
        WithFolder(folder =>
        {
            var file = new LaunchFactsFile(folder);
            file.Begin(new LaunchFacts("1.0.0", "100", "AAAA"), LaunchA);
            file.Begin(new LaunchFacts("1.1.0", "110", null), LaunchB);

            file.Write(new LaunchFacts("1.1.0", "110", "BBBB", LaunchB));

            Assert.Equal("AAAA", file.ReadPrevious()!.BinaryUuid);
            Assert.Equal("BBBB", file.ReadCurrent()!.BinaryUuid);
        });
    }

    [Fact]
    public void A_missing_or_cleared_facts_file_has_no_facts_and_clear_removes_both()
    {
        WithFolder(folder =>
        {
            var file = new LaunchFactsFile(folder);
            Assert.Null(file.ReadPrevious());
            file.Begin(new LaunchFacts("1.0.0", "100", null), LaunchA);
            file.Begin(new LaunchFacts("1.1.0", "110", null), LaunchB);

            file.Clear();

            Assert.Null(file.ReadPrevious());
            Assert.Null(file.ReadCurrent());
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[1,2]")]
    public void An_empty_or_damaged_facts_file_has_no_facts(string? text) => Assert.Null(LaunchFactsFile.Parse(text));

    [Fact]
    public void The_run_marker_is_unchanged_by_the_facts_file()
    {
        WithFolder(folder =>
        {
            var store = new PendingCrashStore(folder);
            new LaunchFactsFile(folder).Begin(new LaunchFacts("1.0.0", "100", "AAAA"), LaunchA);

            Assert.Null(store.TakeUncleanExit());
            Assert.Empty(store.List());
            store.MarkRunning();
            Assert.NotNull(store.TakeUncleanExit());
        });
    }

    [Fact]
    public void An_exit_during_the_previous_launch_takes_the_previous_launchs_facts_not_the_running_ones()
    {
        var previous = new LaunchFacts("1.0.0", "100", "AAAA", LaunchA);
        var running = new LaunchFacts("1.1.0", "110", "BBBB", LaunchB);

        LaunchFacts? facts = LaunchFactsChoice.ForNativeExit(LaunchA.AddMinutes(30), null, previous, running);

        Assert.Null(LaunchFactsChoice.ForNativeExit(LaunchB, null, previous, running)); // at or after the running launch's start: not the previous launch's
        Assert.Null(LaunchFactsChoice.ForNativeExit(LaunchB.AddMinutes(5), null, previous, running));

        Assert.Equal(previous, facts);
    }

    [Fact]
    public void An_exit_older_than_the_previous_launch_belongs_to_an_unknown_launch_and_gets_no_facts()
    {
        var previous = new LaunchFacts("1.0.0", "100", "AAAA", LaunchA);

        Assert.Null(LaunchFactsChoice.ForNativeExit(LaunchA.AddHours(-3), null, previous, new LaunchFacts("1.1.0", "110", "BBBB", LaunchB)));
        Assert.Null(LaunchFactsChoice.ForNativeExit(LaunchA.AddMinutes(1), null, null, null));
    }

    [Fact]
    public void A_report_with_a_uuid_gap_and_the_running_build_takes_the_running_binarys_uuid()
    {
        var running = new LaunchFacts("1.1.0", "110", "BBBB", LaunchB);

        LaunchFacts? fromSystem = LaunchFactsChoice.ForNativeExit(LaunchB, new LaunchFacts("1.1.0", "110", null), null, running);
        LaunchFacts? fromFile = LaunchFactsChoice.ForUncleanExit(new LaunchFacts("1.1.0", "110", null, LaunchA), running);

        Assert.Equal("BBBB", fromSystem!.BinaryUuid);
        Assert.Equal("BBBB", fromFile!.BinaryUuid);
    }

    [Fact]
    public void A_uuid_gap_on_another_build_stays_a_gap()
    {
        var running = new LaunchFacts("1.1.0", "110", "BBBB", LaunchB);

        LaunchFacts? facts = LaunchFactsChoice.ForUncleanExit(new LaunchFacts("1.0.0", "100", null, LaunchA), running);

        Assert.Null(facts!.BinaryUuid);
        Assert.Equal("100", facts.Build);
    }

    [Fact]
    public void A_diagnostic_that_names_its_own_build_wins_over_the_previous_launchs_file()
    {
        var reported = new LaunchFacts("0.9.0", "90", "ZZZZ");

        LaunchFacts? facts = LaunchFactsChoice.ForNativeExit(LaunchB, reported, new LaunchFacts("1.0.0", "100", "AAAA", LaunchA), new LaunchFacts("1.1.0", "110", "BBBB", LaunchB));

        Assert.Equal(reported, facts);
    }

    [Fact]
    public void An_unclean_exit_with_no_previous_facts_has_none()
    {
        Assert.Null(LaunchFactsChoice.ForUncleanExit(null, new LaunchFacts("1.1.0", "110", "BBBB", LaunchB)));
    }

    [Fact]
    public void A_call_stack_tree_deeper_than_the_default_json_depth_is_still_searched_and_one_too_deep_gives_null()
    {
        string Nest(int depth) => string.Concat(Enumerable.Repeat("{\"subFrames\":[", depth)) + "{\"binaryName\":\"App\",\"binaryUUID\":\"70b89f27-1634-3580-a695-57cdb41d7743\"}" + string.Concat(Enumerable.Repeat("]}", depth));

        Assert.Equal("70B89F2716343580A69557CDB41D7743", MetricKitBinaries.UuidOf(Nest(50), "App")); // 101 levels
        Assert.Null(MetricKitBinaries.UuidOf(Nest(200), "App")); // 401 levels: JsonException, caught
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
