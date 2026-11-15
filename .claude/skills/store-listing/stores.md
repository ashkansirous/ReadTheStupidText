# Store field limits and image sizes

Read from each store's official pages on 2026-11-14 (web-research). "Not confirmed" = no official page
reached; check the live upload form before relying on it.

## Google Play
Source: https://support.google.com/googleplay/android-developer/answer/9859152 and /answer/9866151
- Title 30, short description 80, full description 4,000 characters. Repeated keywords can get the app suspended.
- Phone screenshots: 2-8, JPEG or 24-bit PNG (no alpha), each side 320-3840 px, long side at most 2x the short. 4+ at 1080x1920 for large-format eligibility.
- 7" and 10" tablet, Chromebook: 4-8 each, sides 1080-7680 px, 16:9 or 9:16.
- Icon 512x512 32-bit PNG, at most 1024 KB. Feature graphic 1024x500 JPEG or 24-bit PNG, required.
- Video: a YouTube URL (public or unlisted, embeddable, no ads).
- Families: screenshots must show the real app; no rankings or prices in images (Families policy, answer/9893335, not fetched).
- Not confirmed: the "What's new" limit.

## Apple App Store
Source: https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/ and .../platform-version-information
- Promotional text 170, description 4,000, what's new 4,000 characters, keywords 100 bytes. Name and subtitle limits not confirmed.
- iPhone 6.9": 1260x2736, 1290x2796 or 1320x2868. iPhone 6.5": 1284x2778 or 1242x2688. iPad 13": 2064x2752 or 2048x2732.
- 1-10 per size; JPEG or PNG, no alpha.
- Previews: up to 3, 15-30 s, at most 500 MB, H.264 .mov/.m4v/.mp4; iPhone 886x1920, iPad 1200x1600.
- Kids: screenshots must suit 4+ (guideline 2.3.8); "For Kids" wording is reserved for the Kids category.

## Microsoft Store (MSIX)
Source: https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-and-edit-store-listing-info and .../msix/screenshots-and-images
- Description 10,000 (plain text, no URLs), what's new 1,500, short description 1,000 (first 270 shown), up to 20 product features of 200 each, short title 50, screenshot captions 200.
- Screenshots: 1-10 desktop, PNG, at least 1366x768 (up to 3840x2160), at most 50 MB.
- Art: tile icon 300x300; box art 1:1 1080x1080; super hero art 16:9 1920x1080 with no text.
- Keywords (7 terms of 40 characters): not confirmed.

## Amazon Appstore (Fire tablets, Fire TV)
Source: https://developer.amazon.com/docs/app-submission/appstore-details.html
- Short description 2,000 bytes (Fire TV shows 200 characters), long description 4,000, 3-5 feature bullets, plain text only.
- Tablet/phone: icons 114x114 and 512x512 PNG; screenshots 3-10 at 800x480, 1024x600, 1280x720, 1280x800, 1920x1080, 1920x1200 or 2560x1600 (PNG/JPEG); promo image 1024x500 optional.
- Fire TV: icon 1280x720, screenshots 3-10 at 1920x1080, background 1920x1080, no transparency.

## Alexa skill store (a skill, not an app)
Source: https://developer.amazon.com/en-US/docs/alexa/devconsole/launch-your-skill.html
- Icons 108x108 and 512x512 PNG; exactly 3 example phrases, the first with the wake word and invocation name.
- Up to 5 detail-page images and 1 video: sizes not confirmed.

## Captions and text on screenshots (read 2026-11-14)
- **Apple** (https://developer.apple.com/app-store/review/guidelines/): 2.3.3 screenshots show the app in use, not title art or a splash screen; text and image overlays are allowed. 2.3.7 no prices, terms or descriptions that do not belong to that metadata type. 2.3.8 screenshots suit 4+; "For Kids" and "For Children" are reserved for the Kids Category. 2.3.14 no specific or discounted pricing and no unverifiable claims in creative assets. 1.3 (Kids Category) binds only apps in that category.
- **Google Play** (https://support.google.com/googleplay/android-developer/answer/9866151, answer/9898842): show the actual in-app experience; taglines only if necessary and at most 20% of the image; no "Best", "#1", "Top", "New", "Discount", "Sale", "Million Downloads", no rankings or accolades, no calls to action ("Download now", "Try now"). These are recommendations; the hard rule is at least two screenshots. The Families policy page says nothing about screenshot text (the full Designed for Families rules: search only).
- **Microsoft Store** (https://learn.microsoft.com/windows/apps/publish/store-policies, policy 7.20): metadata accurately reflects the product's features.
- So for Dino: a short benefit line in at most 20% of the image, the real screen below it; no prices and no buying terms in any image (Apple 2.3.7): the payment message lives in the text, not in a caption.

## Subscriptions (read 2026-11-14)
- **Apple** (https://developer.apple.com/app-store/subscriptions/, guidelines 3.1.2): the app **and its App Store metadata** must link to the Terms of Use (EULA) and the Privacy Policy (the description, or the EULA and privacy URL fields). The sign-up screen shows the subscription name, length, full renewal price, the trial length and the price after it, and a restore option. Duration at least 7 days. Whether the description must state each plan's price and length: not confirmed (Schedule 2 not reached).
- **Google Play** (https://support.google.com/googleplay/android-developer/answer/9900533): the disclosures (cost, billing period, renewal, trial conversion and price, how to cancel; an annual plan never led by a monthly price) are in-app, on offer and plan screens; the policy does not cover listing text. The listing must not contradict them.
- **Microsoft Store** (store policies 10.8.4, 10.8.6): the **listing** states the in-product purchase types, the price range and the trial terms.
