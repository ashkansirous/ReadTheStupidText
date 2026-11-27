using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 398 (mock SB7): when the calm "can't check your subscription" banner shows, and the date it names.</summary>
public class OfflineNoticeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private static SubscriptionRecord Held(SubscriptionState state, TimeSpan endsFromNow, DateTimeOffset? checkedAt = null) =>
        new(state, Now + endsFromNow, checkedAt ?? Now - TimeSpan.FromDays(30), SubscriptionProducts.Monthly);

    [Fact]
    public void PastTheEndDate_InsideTheThreeDays_Offline_ShowsAndNamesTheLockDate()
    {
        SubscriptionRecord record = Held(SubscriptionState.Active, -TimeSpan.FromHours(5));

        Assert.Equal(record.EndsAt + TimeSpan.FromDays(3), SubscriptionAccess.OfflineNotice(Now, record, offline: true));
    }

    [Fact]
    public void Online_NeverShows_TheStoreAnswerDecidesAtOnce()
    {
        Assert.Null(SubscriptionAccess.OfflineNotice(Now, Held(SubscriptionState.Active, -TimeSpan.FromHours(5)), offline: false));
    }

    [Fact]
    public void BeforeTheEndDate_NeverShows()
    {
        Assert.Null(SubscriptionAccess.OfflineNotice(Now, Held(SubscriptionState.Active, TimeSpan.FromDays(2)), offline: true));
    }

    [Fact]
    public void ExactlyAtTheEndDate_IsNotYetPast()
    {
        Assert.Null(SubscriptionAccess.OfflineNotice(Now, Held(SubscriptionState.Active, TimeSpan.Zero), offline: true));
    }

    [Fact]
    public void ExactlyAtTheLockMoment_StillShows_AndOneSecondLaterIsLocked()
    {
        SubscriptionRecord record = Held(SubscriptionState.Trial, -SubscriptionAccess.OfflineGrace);

        Assert.NotNull(SubscriptionAccess.OfflineNotice(Now, record, offline: true));
        Assert.Null(SubscriptionAccess.OfflineNotice(Now.AddSeconds(1), record, offline: true));
        Assert.False(SubscriptionAccess.Grants(record, Now.AddSeconds(1))); // the same edge as Grants: after it the books are locked, not noticed
    }

    [Theory]
    [InlineData(SubscriptionState.Expired)]
    [InlineData(SubscriptionState.Unknown)]
    public void AnEndedOrUnknownRecord_NeverShows(SubscriptionState state)
    {
        Assert.Null(SubscriptionAccess.OfflineNotice(Now, Held(state, -TimeSpan.FromHours(5)), offline: true));
    }

    [Fact]
    public void InGrace_CountsLikeAnyEntitledState()
    {
        Assert.NotNull(SubscriptionAccess.OfflineNotice(Now, Held(SubscriptionState.InGrace, -TimeSpan.FromDays(1)), offline: true));
    }

    [Fact]
    public void NoRecord_NeverShows()
    {
        Assert.Null(SubscriptionAccess.OfflineNotice(Now, null, offline: true));
    }

    [Fact]
    public void WithNoEndDate_TheReadMomentStandsInForIt()
    {
        var record = new SubscriptionRecord(SubscriptionState.Active, null, Now - TimeSpan.FromDays(1), SubscriptionProducts.Monthly);

        Assert.Equal(record.CheckedAt + TimeSpan.FromDays(3), SubscriptionAccess.OfflineNotice(Now, record, offline: true));
    }

    [Fact]
    public void WhileTheNoticeShows_TheBooksAreStillGranted()
    {
        SubscriptionRecord record = Held(SubscriptionState.Active, -TimeSpan.FromDays(2));

        Assert.NotNull(SubscriptionAccess.OfflineNotice(Now, record, offline: true));
        Assert.True(SubscriptionAccess.Grants(record, Now)); // a banner, never a lock (Slice 384 Decision 6)
    }
}

/// <summary>Slice 398 (mock SB6): the banner's count of locked books.</summary>
public class LockedCountTests
{
    [Fact]
    public void CountsOnlyTheLockedBooks()
    {
        ShelfAccess[] shelf = [ShelfAccess.Readable, ShelfAccess.LockedByLapse, ShelfAccess.LockedByLapse, ShelfAccess.Readable];

        Assert.Equal(2, BookAccess.LockedCount(shelf));
    }

    [Fact]
    public void LockDate_IsTheCulturesOwnShortDate_WithTheYear()
    {
        DateTimeOffset lockDate = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        string en = SubscriptionAccess.FormatLockDate(lockDate, new System.Globalization.CultureInfo("en-GB"));
        string ja = SubscriptionAccess.FormatLockDate(lockDate, new System.Globalization.CultureInfo("ja-JP"));
        string fa = SubscriptionAccess.FormatLockDate(lockDate, new System.Globalization.CultureInfo("fa-IR"));

        Assert.Contains("2026", en);
        Assert.StartsWith("2026", ja);
        Assert.False(string.IsNullOrWhiteSpace(fa));
        Assert.DoesNotContain("Nov", fa);
        Assert.NotEqual(en, ja);
    }

    [Fact]
    public void AnEmptyOrAllReadableShelf_HasNoBanner()
    {
        Assert.Equal(0, BookAccess.LockedCount([]));
        Assert.Equal(0, BookAccess.LockedCount([ShelfAccess.Readable]));
    }

    [Fact]
    public void ARenewedSubscription_UnlocksEveryBookAtOnce()
    {
        ShelfAccess Of(bool grants) => BookAccess.ShelfAccessOf(true, BookUnlock.Subscription, grants);

        Assert.Equal(1, BookAccess.LockedCount([Of(false)]));
        Assert.Equal(0, BookAccess.LockedCount([Of(true)]));
    }
}
