# YouTube policy check for the Dino read-aloud channel (Slice 435, item 1)

Read 2026-11-27 from YouTube's own help pages (fetched live, not from memory). One question each, the answer, and what the renderer does about it.

## 1. Must we disclose AI-made content?

Source: https://support.google.com/youtube/answer/14328491

- Disclosure is required for AI-made or altered content "that seems realistic": a real person made to say or do something they did not, altered footage of a real event or place, a realistic scene that did not happen, or music that is the main focus of the video.
- "Creators don't need to disclose non-realistic content that's made with AI" (examples: fantastical scenes, fully animated videos). Using AI for an outline, script, thumbnail or title is also exempt.
- Where it is set: YouTube Studio upload, Attributes, "AI use"; in the API it is `status.containsSyntheticMedia`.

Decision: the books are painted picture-book illustrations of animals and fairy-tale people, narrated by synthetic character voices of fictional characters. Nothing is realistic and no real person is imitated, so the exemption applies and the upload script sends `containsSyntheticMedia: false` by default. The closing card's credits still say the pictures and voices were made with AI (honest, costs nothing), and the flag is the `--ai-disclosure` option of `upload.mjs` (default off) if the owner prefers "Yes".

## 2. Made-for-kids rules

Source: https://support.google.com/youtube/answer/9632097

- Made-for-kids pages lose autoplay on home, cards and end screens, watermarks, comments, notifications, personalised ads, Save to playlist / Watch Later, Miniplayer, memberships, donations, live chat, merchandise.
- Set per video (and per channel): API field `status.selfDeclaredMadeForKids: true`.

Consequence (as the note already says): the QR code inside the picture is the only way to point to the site; no clickable link or end screen exists. Note: "Save to playlist" being off is only for viewers; the channel's own playlists still work.

## 3. Quality principles for kids content ("heavily promotional")

Source: https://support.google.com/youtube/answer/10774223

- Low quality: content that "mainly features products, brands and logos" and content that "encourages excessive buying".
- Nothing about links or QR codes.

Decision: the QR code and the address appear only on the opening and closing cards (small, in a corner), never during the story; the closing line is the owner's approved wording with no "buy", "download now" or price.

## 4. Is a QR code or site address allowed?

Sources: https://support.google.com/youtube/answer/9054257 (External links policy), https://support.google.com/youtube/answer/2801999 (Child safety policy)

- The external links policy bans links to harmful destinations (porn, malware, phishing, unauthorised paid content, regulated goods, terrorism, hate and similar). It covers "showing text of URLs in videos or images" and verbal directions to other sites. It names no rule against links to a site that sells something, and it does not mention QR codes or made-for-kids videos.
- The child safety policy repeats that the external-link rules apply to "clickable URLs, verbally directing users to other sites in video, as well as other forms", and says nothing about commercial links in kids' content.

Finding: no YouTube rule found that forbids a QR code or address to a safe site of our own in a made-for-kids video. Not found is not the same as allowed in writing, so the renderer keeps the QR small and on the two cards only, and the first upload goes **unlisted** for the owner before anything is public. If YouTube removes the link or the video, the card falls back to the site address as text only (`--no-qr`, a `render.mjs` flag: the cards must be re-rendered and the videos re-uploaded, it does not change a video already up).

## 5. Monetisation ("repetitive content")

Not a rule about whether we may upload; recorded in the source note (reach, not ad income, is the goal; each language has its own title, description and thumbnail text).
