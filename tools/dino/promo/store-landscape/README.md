# Dino store film, landscape 1920 x 1080 (Slice 434)

The YouTube cut (Google Play) and the Microsoft Store trailer, rendered from footage of the real Windows app window.
Timeline and captions: `docs/plan/notes/2026-11-23-dino-store-screenshots-and-video.md` (Video timeline). Slice 433's
storyboard (`tools/dino/promo/store/`) uses the same note timeline (same six beats, times and captions), so this film is
that storyboard in landscape on the Windows footage, not a placeholder: it is what ships as the Microsoft Store trailer
and the YouTube cut (checked against `tools/dino/promo/store/index.html`, Slice 445). Only the portrait and iPad cuts
add music, grading and the split language clips.

1. Footage: `tools/desktop-vm/Invoke-DesktopTest.ps1 -App dino -Check dino-store-shots -WithShelfPackages <folder> -CopyOut <folder>`.
   The check films the app window (exactly 1920 x 1080) at 10 frames per second; the runner encodes each clip's frames
   on the host with ffmpeg (H.264, 30 fps, one clip at a time, frames deleted) and leaves `page.mp4`, `voices.mp4`,
   `record.mp4`, `languages.mp4` and `shelf.mp4` next to the shots.
2. Copy the five mp4 files into `assets/footage/` (git ignores them, see `.gitignore`).
3. `npx hyperframes check`, `snapshot --review`, then `render` (H.264, AAC 48 kHz; the film is silent, add the audio
   track in 433's storyboard). The thumbnail is a 1920 x 1080 PNG frame (the character-voices shot).

The end frame says that some books need a subscription (Apple and store rule).

## Render and final encode (Slice 445)

4. `npm run render -- --output renders/landscape.mp4 --fps 30 --quality high --workers 1` (one worker keeps memory low; the 20 s film takes about 90 s).
5. The render is H.264 High, yuv420p, 30 fps, 1920 x 1080, with no audio track (`ffprobe` on the 2026-11-28 render, Slice 445); the Microsoft Store trailer needs AAC-LC 48 kHz, so copy the video and add a silent track, and cut the thumbnail from the character-voices shot:
   `ffmpeg -i renders/landscape.mp4 -f lavfi -i anullsrc=channel_layout=stereo:sample_rate=48000 -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -profile:a aac_low -shortest -movflags +faststart renders/dino-microsoft-store-trailer.mp4`,
   `ffmpeg -ss 4.5 -i renders/dino-microsoft-store-trailer.mp4 -frames:v 1 renders/dino-microsoft-store-trailer-thumbnail.png`.
   `ffprobe` must show exactly one video and one audio stream and a 20.0 s length. If a later render is not High or not yuv420p (step 6 then fails on the profile), replace `-c:v copy` with `-c:v libx264 -profile:v high -pix_fmt yuv420p -crf 14`.
6. Check with `tools/emulator/store-size-check.ps1`: `-Target windows-trailer`, `-Target windows-thumbnail` (the two files), `-Target windows-screenshot` (`docs/store-listings/screenshots/dino/windows`).
   `renders/` is git-ignored (no large binaries in git; the repo commits only small site clips), so the files stay local until the store push.
