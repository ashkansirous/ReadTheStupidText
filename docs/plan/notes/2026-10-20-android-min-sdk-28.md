# Android minimum raised to API 28 (2026-10-20)

Record behind Decision 82 in `plan.md`.

## What happened

Build run 39410975418 (the merge of PR #165, the first run to upload to Play)
failed in `play-submit` after about 21 seconds. Google Play refused
`ReadTheStupidText-1.1.6.aab`:

```
Play automatic protection requires a minimum SDK version of 24 or higher.
The uploaded App Bundle has a minimum SDK version of 23.
```

The build itself passed. Every earlier `play-submit` had been skipped, so the
limit had always been there and simply hadn't been tested.

## Why it was 23

Slice 30 took the MAUI template's default of 21. Slice 31 raised it to 23,
the minimum `MediaPlayer.PlaybackParams` needs. Nobody chose a product floor;
the value was only raised as far as the build required.

## Why 28

Google's Android Studio distribution chart, as mirrored by
[Capgo](https://capgo.app/android-distribution-chart/) (data to 2025-11-30)
and reported by [heise](https://heise.de/-11162154) (2025-12-01), puts
cumulative reach at about 95.2% for API 28, 83.0% for API 30 and 69.3% for
API 31. These are third-party copies; Android Studio's New Project wizard
shows the live figures. Google advises covering about 90% of active devices. 28 (Android 9,
2018) clears Play's minimum of 24 with room to spare, and costs about 5% of
devices (Android 8 and older). No user was lost, because the app had reached
internal testing only.

## Not settled here

The target SDK is not pinned in the csproj. It follows the .NET Android
workload CI installs. A local Release build on 2026-10-20 produced
`<uses-sdk android:minSdkVersion="28" android:targetSdkVersion="36" />` in
the merged manifest. If Play's target-API rule ever
rejects a bundle, pin `TargetSdkVersion` explicitly.
