# Dino store film, landscape 1920 x 1080 (Slice 434)

The YouTube cut (Google Play) and the Microsoft Store trailer, rendered from footage of the real Windows app window.
Timeline and captions: `docs/plan/notes/2026-11-23-dino-store-screenshots-and-video.md` (Video timeline). Slice 433's
storyboard (`tools/dino/promo/store/`) replaces the placeholder timeline here once it has merged.

1. Footage: `tools/desktop-vm/Invoke-DesktopTest.ps1 -App dino -Check dino-store-shots -WithShelfPackages <folder> -CopyOut <folder>`.
   The check films the app window (exactly 1920 x 1080) at 10 frames per second; the runner encodes each clip's frames
   on the host with ffmpeg (H.264, 30 fps, one clip at a time, frames deleted) and leaves `page.mp4`, `voices.mp4`,
   `record.mp4`, `languages.mp4` and `shelf.mp4` next to the shots.
2. Copy the five mp4 files into `assets/footage/` (git ignores them, see `.gitignore`).
3. `npx hyperframes check`, `snapshot --review`, then `render` (H.264, AAC 48 kHz; the film is silent, add the audio
   track in 433's storyboard). The thumbnail is a 1920 x 1080 PNG frame (the character-voices shot).

The end frame says that some books need a subscription (Apple and store rule).
