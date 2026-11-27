namespace DinoReader.Core;

/// <summary>Which copy of a book is installed (Slice 463): the small quick package of a slow first run, the standard one, or the high-res one. The wire names are <see cref="FirstBookSwitch.VariantName"/>.</summary>
public enum BookVariant
{
    Standard,
    High,
    Quick,
}

/// <summary>What the slow-link rule says about the standard download of the first book.</summary>
public enum FirstBookChoice
{
    /// <summary>Carry on with the standard package (too early to judge, fast enough, or nothing to predict from).</summary>
    Keep,

    /// <summary>Drop the standard download and fetch the quick package.</summary>
    Switch,
}

/// <summary>
/// Slice 463: the one pure rule of the first book's slow-link switch. The standard download of the first book is judged from <see cref="CheckAfter"/> on (at every progress tick): when the speed measured so far predicts more than
/// <see cref="MaxRemaining"/> left, it is cancelled and the quick package is fetched. Nothing else decides it; <c>StoreClient</c> only feeds it the bytes, the clock and the listed size.
/// </summary>
public static class FirstBookSwitch
{
    /// <summary>How long the standard download runs before it is judged.</summary>
    public static readonly TimeSpan CheckAfter = TimeSpan.FromSeconds(3);

    /// <summary>The longest the rest of the standard download may be predicted to take before the switch fires.</summary>
    public static readonly TimeSpan MaxRemaining = TimeSpan.FromSeconds(10);

    private const string QuickName = "quick";
    private const string StandardName = "standard";
    private const string HighName = "hd";

    /// <summary>
    /// Keep or switch, from the bytes received so far, the time since the download started and the listed size of the package. Before <see cref="CheckAfter"/>, with no listed size, or with every byte already
    /// here: keep. After it, no bytes at all means no measurable speed (the rest is unbounded): switch.
    /// </summary>
    public static FirstBookChoice Decide(long bytesSoFar, TimeSpan elapsed, long? totalBytes, TimeSpan? checkAfter = null)
    {
        if (elapsed < (checkAfter ?? CheckAfter) || totalBytes is not > 0 || bytesSoFar >= totalBytes)
        {
            return FirstBookChoice.Keep;
        }

        if (bytesSoFar <= 0)
        {
            return FirstBookChoice.Switch;
        }

        double bytesPerSecond = bytesSoFar / elapsed.TotalSeconds;
        double secondsLeft = (totalBytes.Value - bytesSoFar) / bytesPerSecond;
        return secondsLeft > MaxRemaining.TotalSeconds ? FirstBookChoice.Switch : FirstBookChoice.Keep;
    }

    /// <summary>The name a variant is stored and logged under.</summary>
    public static string VariantName(BookVariant variant) => variant switch
    {
        BookVariant.Quick => QuickName,
        BookVariant.High => HighName,
        _ => StandardName,
    };
}

/// <summary>
/// Slice 463: the clock and the byte count of the first book's standard download, judged by <see cref="FirstBookSwitch.Decide"/> at every progress tick from <see cref="FirstBookSwitch.CheckAfter"/>. A "switch" cancels
/// <see cref="Token"/> (linked to the caller's own) and sets <see cref="Switched"/>; the caller tells that apart from the grown-up's cancel by the caller's own token. It owns nothing but the timer.
/// </summary>
public sealed class SlowLinkWatch : IDisposable
{
    private readonly CancellationTokenSource _cancel;
    private readonly long? _total;
    private readonly TimeSpan _checkAfter;
    private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    private readonly Func<TimeSpan> _clock;
    private TimeSpan _clockStart;
    private Timer _timer;
    private int _generation;
    private long _bytes;
    private int _switched;
    private string _judgement = "not judged";

    /// <param name="caller">The grown-up's own token: its cancel is never a switch.</param>
    /// <param name="totalBytes">The listed size of the package being fetched.</param>
    /// <param name="checkAfter">Tests only: a shorter wait than <see cref="FirstBookSwitch.CheckAfter"/>.</param>
    /// <param name="clock">Tests only: the time since the watch started (a clock that reads a little early).</param>
    public SlowLinkWatch(CancellationToken caller, long? totalBytes, TimeSpan? checkAfter = null, Func<TimeSpan>? clock = null)
    {
        _cancel = CancellationTokenSource.CreateLinkedTokenSource(caller);
        _total = totalBytes;
        _checkAfter = checkAfter ?? FirstBookSwitch.CheckAfter;
        _clock = clock ?? (() => _stopwatch.Elapsed);
        _clockStart = _clock();
        _timer = StartTimer(0);
    }

    /// <summary>Tests only: the number of the current fetch; a <see cref="Restart"/> makes a new one.</summary>
    public int Generation => Volatile.Read(ref _generation);

    private Timer StartTimer(int generation) => new(_ => TimerFired(generation), null, _checkAfter, Timeout.InfiniteTimeSpan);

    /// <summary>The check time of fetch <paramref name="generation"/> came. A callback of an earlier fetch (already queued when <see cref="Restart"/> ran) is ignored: it must not judge the fresh fetch as if it had run the check time.</summary>
    public void TimerFired(int generation)
    {
        if (generation == Generation)
        {
            Judge(fromTimer: true);
        }
    }

    public CancellationToken Token => _cancel.Token;

    /// <summary>True once the rule said "switch" (and the download was cancelled for it).</summary>
    public bool Switched => Volatile.Read(ref _switched) == 1;

    /// <summary>What the rule saw when it judged, for the Debug log.</summary>
    public string Judgement => Volatile.Read(ref _judgement);

    /// <summary>The bytes of the package received so far (called from the download's own thread).</summary>
    public void Observe(long done)
    {
        Interlocked.Exchange(ref _bytes, done);
        if (!Switched && _clock() - _clockStart >= _checkAfter)
        {
            Judge(fromTimer: false); // review: judged on every progress tick from the check time on, so a timer that fires a few ms early cannot leave a slow download unjudged for good
        }
    }

    /// <summary>The fetch started over with another package (the high-res one fell back to standard): the clock and the byte count begin again, and the check comes after <see cref="FirstBookSwitch.CheckAfter"/> of the new fetch.</summary>
    public void Restart()
    {
        int generation = Interlocked.Increment(ref _generation);
        _clockStart = _clock();
        Interlocked.Exchange(ref _bytes, 0);
        Timer old = _timer;
        _timer = StartTimer(generation);
        old.Dispose();
    }

    private void Judge(bool fromTimer)
    {
        if (Switched)
        {
            return;
        }

        long bytes = Interlocked.Read(ref _bytes);
        TimeSpan elapsed = _clock() - _clockStart;
        if (fromTimer && elapsed < _checkAfter)
        {
            elapsed = _checkAfter; // the timer fired a few ms before the clock reads the check time; it is the check time
        }

        FirstBookChoice choice = FirstBookSwitch.Decide(bytes, elapsed, _total, _checkAfter);
        Volatile.Write(ref _judgement, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{choice}: {bytes} of {_total?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"} bytes in {elapsed.TotalSeconds:0.0} s"));
        if (choice != FirstBookChoice.Switch)
        {
            return;
        }

        Volatile.Write(ref _switched, 1);
        try
        {
            _cancel.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // no-fallback: the download ended and disposed the watch just before the judgement; there is nothing left to cancel
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _cancel.Dispose();
    }
}

/// <summary>
/// Slice 463: the pure rules of the background upgrade from a quick first book to the standard package, the mirror of <see cref="HighResUpgradePlan"/>. A pass DERIVES the books to upgrade from facts that exist
/// anyway; nothing is kept in a list of waiting books. The failures are counted in the high-res ledger under <see cref="LedgerKey"/>, so a package that never verifies is given up after
/// <see cref="HighResLedger.MaxFailures"/> tries.
/// </summary>
public static class QuickUpgradePlan
{
    private const string LedgerPrefix = "quick:";

    /// <summary>The ledger entry of a book's quick upgrade (apart from its high-res upgrade, which uses the plain id).</summary>
    public static string LedgerKey(string id) => LedgerPrefix + id;

    /// <summary>The ledger entry of one quick audio pack's replacement (apart from its book's), so a pack whose standard copy never installs is given up after <see cref="HighResLedger.MaxFailures"/> tries.</summary>
    public static string PackLedgerKey(string id, string language) => $"quick-pack:{id}|{language}";

    /// <summary>
    /// The quick records whose book is not on the shelf and that an EARLIER launch wrote (a kill between the record and the import left them): to be dropped at the start of a pass, so they never steer a later install.
    /// A record this launch wrote (<paramref name="writtenThisLaunch"/>) is never dropped: its import may still be in flight (check pass 1, rule 2).
    /// </summary>
    public static IReadOnlyList<string> OrphanedQuickRecords(IReadOnlyDictionary<string, int> installedQuick, IReadOnlySet<string> onShelf, IReadOnlySet<string> writtenThisLaunch) =>
        installedQuick.Keys.Where(id => !onShelf.Contains(id) && !writtenThisLaunch.Contains(id)).ToList();

    /// <summary>
    /// Check pass 2 #1 (rule 2): a quick PACK record is forgotten only when the book is absent from a complete shelf read, or the listing moved on, AND the record was not written this launch
    /// (its install may still be in flight). The caller reads the shelf with the complete-or-null read and does not call this when that read is null.
    /// </summary>
    public static bool ShouldForgetQuickPack(bool bookOnShelf, bool listingMatches, bool writtenThisLaunch) => (!bookOnShelf || !listingMatches) && !writtenThisLaunch;

    /// <summary>Check pass 1, rule 3: a quick record goes only when the import did not succeed AND the book is not on the shelf. A cancel after the book landed keeps it.</summary>
    public static bool ShouldForgetQuickRecord(bool importSucceeded, bool bookOnShelf) => !importSucceeded && !bookOnShelf;

    /// <summary>Check pass 1, rule 4: whether a failed pack install counts toward the ledger. Only a bad file does (wrong size, a broken zip or clip, a 404); a network, timeout or disk fault counts nothing and the pass retries later.</summary>
    public static bool CountsAsBadPack(PackFailureKind kind) => kind is PackFailureKind.Size or PackFailureKind.Unzip or PackFailureKind.Clip or PackFailureKind.Missing;

    /// <summary>Check pass 2 #5 (rule 4): whether a failed swap of a staged standard book package is the package's fault: only a confirmed bad file (broken zip or manifest content). Every other exception is transient and counts nothing.</summary>
    public static bool IsBadStagedPackage(Exception ex) => ex is InvalidDataException or StoryPackageException or System.Text.Json.JsonException;

    /// <summary>Whether the standard copy of a quick pack may be fetched again: the same back-off the high-res upgrades use.</summary>
    public static bool PackMayRetry(HighResLedger ledger, string id, string language, int version) => ledger.Allows(PackLedgerKey(id, language), version);

    /// <summary>
    /// The books to upgrade now, in id order: on the shelf at the catalogue's version, recorded as quick at that version, fetchable by this account, no standard package already staged for that version,
    /// and the ledger allowing another attempt. A book that cannot be fetched right now is simply not in the result: nothing is counted for it.
    /// </summary>
    public static IReadOnlyList<StoreItem> Derive(
        IReadOnlyDictionary<string, int?> shelfVersions,
        IReadOnlyList<StoreItem> listed,
        IReadOnlySet<string> fetchableIds,
        IReadOnlyDictionary<string, int> installedQuick,
        IReadOnlyDictionary<string, int> stagedStandard,
        HighResLedger ledger) =>
        listed
            .Where(item => fetchableIds.Contains(item.Id) && IsDue(item, shelfVersions, installedQuick, stagedStandard, ledger))
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsDue(StoreItem item, IReadOnlyDictionary<string, int?> shelfVersions, IReadOnlyDictionary<string, int> installedQuick, IReadOnlyDictionary<string, int> stagedStandard, HighResLedger ledger) =>
        shelfVersions.TryGetValue(item.Id, out int? installed) && installed == item.Version
        && installedQuick.TryGetValue(item.Id, out int quick) && quick == item.Version
        && !(stagedStandard.TryGetValue(item.Id, out int staged) && staged == item.Version)
        && ledger.Allows(LedgerKey(item.Id), item.Version);

    /// <summary>
    /// What a staged standard package at apply time means (null: swap it in): the same rule as the high-res one (<see cref="HighResUpgradePlan.OutcomeBeforeSwap"/>), where "already the target" means the installed
    /// copy is no longer quick. A quick copy that is older than the package, or whose version differs, is dropped: the ordinary update brings a standard copy of the newer version anyway.
    /// </summary>
    public static HighResOutcome? OutcomeBeforeSwap(int? installed, int packageVersion, int? stagedUpdateVersion, bool onShelf, bool stillQuick) =>
        HighResUpgradePlan.OutcomeBeforeSwap(installed, packageVersion, stagedUpdateVersion, onShelf, installedIsHigh: !stillQuick);
}
