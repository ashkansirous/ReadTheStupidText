# Slice 475: Dino: name dino.elinsoft.co.uk on the Sonos link page and in the privacy text

**Product:** Dino

*Planned 2026-11-28 by plan-cleanup, not started.*

**Depends on:** Slice 467 (merged; `dino.elinsoft.co.uk` is live).

**Touches:** `site/dino/sonos-link.html`, `site/dino/privacy.html`, `PRIVACY.md`, this file.

## Why

Slice 467 review follow-ups, skipped there as outside its Touches: the Sonos link page hard-codes `https://dinoreader.sirous.uk/` for its Home and Get-the-app links (lines 34 and 41); the privacy text names only `dinoreader.sirous.uk` as the server, though `dino.elinsoft.co.uk` is the same Worker and operator.

## Work

- [ ] 1. In `site/dino/sonos-link.html`, build the Home and Get-the-app links from `location.origin` so each host stays on itself.
- [ ] 2. Name `dino.elinsoft.co.uk` in `site/dino/privacy.html` and `PRIVACY.md` next to `dinoreader.sirous.uk`.
- [ ] 3. Not in this slice: `SonosLink.cs` `SiteHost` (the app keeps the old host by Slice 467's Decision).
