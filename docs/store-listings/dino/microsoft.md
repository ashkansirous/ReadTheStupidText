# Microsoft Store listing: Dino: Kids Books & Stories (Store ID 9N9VHRLCZXN5)

**Slice 412 (2026-11-18): new product name and search terms, text only, NOT pushed.** The old name is in git history. The name "Dino: Kids Books & Stories" must be reserved in Partner Center first (user step, Slice 412 item 7), and it ships with the next Windows submission together with the manifest `DisplayName` (Slice 412 Part B).

## Product name and search terms (en-gb / en-us)

Product name (<= 200): Dino: Kids Books & Stories

Search terms (7, each <= 30 chars): bedtime stories; kids books; stories for kids; read aloud; picture books; children's stories; storybook

The English description's opening names the same phrases ("bedtime stories for kids", "picture books read aloud", "children's stories") and says "Dino" for the app, never the old name.

## English description and features pushed 2026-11-18 (Slice 412 item 6; en-us, title unchanged)

Rewritten from the live text: the opening names the search phrases and says "Dino"; "bring your own books" and "voices are generated on your computer" are gone (the app sells store books with pre-made voices); purchase lines as in the English section below, no subscription wording (the two subscription add-ons are not in Partner Center, read 2026-11-18).

Description (<= 10000):

    Bedtime stories for kids, picture books read aloud: Dino reads children's stories to children aged 3 to 9, with a different voice for every character. The narrator tells the story, and the wolf, the pigs and the gingerbread man each speak in their own voice.

    PICK YOUR STORIES
    A grown-up chooses books from the built-in story shelf: classic tales retold gently, with a picture on every page. Four stories are free. Every book comes with its own pre-made voices, in the language you pick.

    READ TOGETHER
    - Each sentence lights up as it is read, so children can follow along.
    - Picture pages stay as they are, with the words read over them.
    - A night theme for bedtime stories.

    STORIES AND PRICES
    Four illustrated stories are free: The Lion and the Mouse, The Gingerbread Man, The Three Little Pigs and X Marks the Treasure. Every other story on the built-in story shelf is $0.99 each (the Store shows the price in your own currency). A grown-up buys a story once, behind the grown-ups gate, through the Microsoft Store, and can restore past purchases at any time. Stories you already have stay on your device.

    FIFTEEN LANGUAGES
    English, Chinese, Spanish, Arabic, French, Portuguese, Russian, German, Japanese, Farsi, Dutch, Swedish, Danish, Finnish and Norwegian.

    MADE FOR KIDS
    - No ads.
    - Stories are bought through the Microsoft Store, by a grown-up behind the grown-ups gate. There is no account to create, and we store nothing about you or your purchase; the store's proof is checked and forgotten.
    - Settings, buying and deleting stories sit behind a grown-ups gate.
    - Once a story and its language are downloaded, it reads offline.

Features (<= 200 each): Bedtime stories for kids, read aloud with a different voice for every character | Each sentence lights up as it is read | Four free stories, the rest $0.99 each | Fifteen languages | Night theme for bedtime | No ads, no account, buy a story only if you want it | Grown-ups gate for settings | Works offline once a story is downloaded

**Pushed 2026-11-13 (Slice 346 Part D): English in submission 82 (published), German (Partner Center locale `de`) in submission 1152921505702395457 (listing only, committed).** New file: the English and German texts were read from the live public pages (`apps.microsoft.com/detail/9N9VHRLCZXN5?hl=en-gb` and `?hl=de-de`, last update 2026-11-12). Only the purchase lines below differ from live; everything else stays as live. Push with the next Partner Center submission once the paid-books switch is on (Partner Center changes only with a submission; never one that discards a pending draft). The price field stays "Free" (the app is free to download; each paid story is an add-on product made by `store-products.mjs`).

## English (en-gb / en-us)

Live "MADE FOR KIDS" bullets, to replace:

    - No ads and no in-app purchases.
    - No accounts.

New:

    - No ads.
    - Stories are bought through the Microsoft Store, by a grown-up behind the grown-ups gate. There is no account to create, and we store nothing about you or your purchase; the store's proof is checked and forgotten.

Live "STORIES INCLUDED" paragraph ("The app comes with illustrated starter stories, and grown-ups can get more free stories from the built-in story shelf.") becomes:

    STORIES AND PRICES
    Four illustrated stories are free: The Lion and the Mouse, The Gingerbread Man, The Three Little Pigs and X Marks the Treasure. Every other story on the built-in story shelf is $0.99 each (the Store shows the price in your own currency). A grown-up buys a story once, behind the grown-ups gate, through the Microsoft Store, and can restore past purchases at any time. Stories you already have stay on your device.

Live feature "No ads, no in-app purchases, no accounts" becomes "No ads, no account, buy a story only if you want it". Live feature "Illustrated starter stories and a free story shelf" becomes "Four free stories, the rest $0.99 each".

## German (de-de), machine draft, needs a native read

Live bullets to replace: "Keine Werbung und keine In-App-Käufe." and "Keine Konten."

    - Keine Werbung.
    - Geschichten werden über den Microsoft Store gekauft, von einem Erwachsenen hinter der Erwachsenensperre. Es gibt kein Konto anzulegen, und wir speichern nichts über den Käufer.

Live paragraph "GESCHICHTEN INKLUSIVE" becomes:

    GESCHICHTEN UND PREISE
    Vier bebilderte Geschichten sind kostenlos: Der Löwe und die Maus, Der Lebkuchenmann, Die drei kleinen Schweinchen und X markiert die Stelle. Jede weitere Geschichte im integrierten Geschichtenregal kostet je 0,99 € bzw. der entsprechende Preis in Ihrer Landeswährung (der Store zeigt den Preis an). Ein Erwachsener kauft eine Geschichte einmal, hinter der Erwachsenensperre, über den Microsoft Store und kann frühere Käufe jederzeit wiederherstellen. Geschichten, die Sie schon haben, bleiben auf Ihrem Gerät.

Live feature "Keine Werbung, keine Käufe, keine Konten" becomes "Keine Werbung, kein Konto, Kauf nur wenn gewünscht". Live feature "Bebilderte Starter-Geschichten und kostenloses Regal" becomes "Vier Geschichten gratis, jede weitere je 0,99 € bzw. Landeswährung".

## Feature lines per language (Microsoft only, submission 108, 2026-11-24)

The 14 non-English listings keep their other five feature lines; lines 3, 4, 7 and 9 are these (record your voice; four free stories; no ads, no account, buy only if you want; offline once a story is downloaded). Machine-written by Claude, native read pending like the rest of Slice 413.

- da: Indtal din egen stemme til enhver figur | Fire gratis historier, de andre 0,99 USD stykket | Ingen reklamer, ingen konto, køb kun hvis du vil | Virker uden internet, når historien er hentet
- ar: سجّل صوتك لأي شخصية | أربع قصص مجانية، واشترِ غيرها قصة قصة | بلا إعلانات ولا حساب، والشراء فقط إن أردت | يعمل بلا إنترنت بعد تنزيل القصة
- zh-hans: 为任何角色录下你自己的声音 | 四个故事免费，其他故事可单独购买 | 无广告、无需账户，想买才买 | 故事下载后可离线使用
- nl: Spreek je eigen stem in voor elk personage | Vier gratis verhalen, de rest los te koop | Geen reclame, geen account, alleen kopen als je wilt | Werkt offline zodra een verhaal is gedownload
- fi: Äänitä oma äänesi mille tahansa hahmolle | Neljä satua ilmaiseksi, muut voi ostaa yksitellen | Ei mainoksia eikä tiliä, osta vain jos haluat | Toimii ilman nettiä, kun satu on ladattu
- fr: Enregistrez votre propre voix pour n'importe quel personnage | Quatre histoires gratuites, les autres à l’unité | Sans publicité ni compte, achat seulement si vous le voulez | Fonctionne hors ligne une fois l’histoire téléchargée
- ja: どのキャラクターにも自分の声を録音できます | 4つのお話は無料、ほかは1冊ずつ購入 | 広告なし、アカウント不要、買うのは必要なときだけ | お話をダウンロードすればオフラインで使えます
- nb: Les inn din egen stemme til hvilken som helst figur | Fire gratis historier, resten kan kjøpes enkeltvis | Ingen reklame, ingen konto, kjøp bare hvis du vil | Virker uten internett når historien er lastet ned
- fa: صدای خودتان را برای هر شخصیتی ضبط کنید | چهار قصه رایگان، بقیه را تک‌تک بخرید | بدون تبلیغ و بدون حساب کاربری، خرید فقط اگر بخواهید | پس از دانلود قصه، بدون اینترنت کار می‌کند
- pt: Grave sua própria voz para qualquer personagem | Quatro histórias grátis, as outras compradas uma a uma | Sem anúncios, sem conta, compre só se quiser | Funciona offline depois que a história é baixada
- ru: Запишите свой голос для любого персонажа | Четыре сказки бесплатно, остальные можно купить по одной | Без рекламы и без аккаунта, покупки только по желанию | Работает без интернета после загрузки сказки
- es: Graba tu propia voz para cualquier personaje | Cuatro cuentos gratis, los demás se compran por separado | Sin anuncios ni cuenta, compra solo si quieres | Funciona sin conexión una vez descargado el cuento
- sv: Spela in din egen röst till vilken figur som helst | Fyra gratis sagor, resten köps en i taget | Ingen reklam, inget konto, köp bara om du vill | Fungerar utan internet när sagan är nedladdad
- de: Eigene Stimme für jede Figur aufnehmen | Vier Geschichten gratis, jede weitere je 0,99 € bzw. Landeswährung | Keine Werbung, kein Konto, Kauf nur wenn gewünscht | Funktioniert offline, sobald eine Geschichte geladen ist
