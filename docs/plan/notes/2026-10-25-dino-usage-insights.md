# 2026-10-25 — Dino usage insights: what the rules allow and what it costs

The user wants to know which books families like and in which language, how parents use the store,
whether they record their voice, change voices or add their own books. Researched 2026-10-25 (Batch 28).

## Today (code check on origin/main)

- Dino sends nothing besides store and voice-pack downloads (`StoreClient.cs`, `MobileVoicePackService.cs`);
  no analytics SDK; no install id. Android ML Kit sends Google diagnostics (already disclosed).
- The Worker writes nothing; only the R2 binding; `observability.enabled` is on, so story download
  URLs are already visible in Workers Logs (free signal, no code change).
- `site/dino/privacy.html`, `PRIVACY.md` and `STORE.md` promise no analytics; store forms say no data
  collected.

## Rules (sources fetched 2026-10-25; not legal advice)

- Google Play Families (support.google.com/googleplay/android-developer/answer/9893335): no AAID,
  IMEI, MAC, SSID and similar from children; first-party calls to your own server are not an SDK;
  Data safety must disclose what is collected.
- Apple 1.3 / 5.1.4: third-party analytics banned in Kids; first-party allowed; parental gate before
  links out.
- COPPA 16 CFR 312: a persistent identifier is personal information; the internal-operations exception
  excludes using it "to amass a profile on a specific individual". A per-device profile needs
  verifiable parental consent.
- UK Children's code: profiling off by default (Standard 12), data minimisation (Standard 8), high
  privacy defaults (Standard 7).
- Outcome: anonymous counts with context, no id (Decision 140).

## Cost (Cloudflare pricing pages, 2026-10-25)

- Workers Analytics Engine: not billed yet; then Free 100k points/day, Paid ($5/month) 10M/month,
  +$0.25 per extra million; 3 months retention; 250 points per invocation.
- Workers Free: 100k requests/day; Paid $5/month includes 10M requests.
- Example: 1,000 daily readers x 20 events, batched per session = about 0.6M points/month, free;
  10,000 daily readers = about 6M, inside $5.

## Turning it down

Worker switch and sampling through the R2 config (Slice 132), the grown-up off switch (Slice 133), and
the forced update for changes that need a release (Slice 132).
