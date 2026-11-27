# Dino: Kids Books & Stories: subscription listings in 15 more languages (Slice 384)

Machine drafts (no native read yet), written 2026-11-15 from the app's own Sub_Title/Sub_Line strings (src/Dino/DinoReader.App/Resources/Strings/AppResources.*.resx). Google Play: pushed to subscription uk.sirous.dinoreader.sub (listings, regionsVersion 2026/01), read back identical. App Store Connect: NOT pushed yet, Apple refused while the subscriptions were Waiting for Review (409 UNMODIFIABLE); they are APPROVED now and `store-products.mjs` (Slice 454) adds the 15 Apple locales to the group and both subscriptions from this file (the script reads the `## <lang> (Apple <locale>, ...)` headings and the `Apple name` and `Description` lines, so keep that shape; a name over 30 or a description over 45 characters fails the run). Apple limits: subscription name <= 30, description <= 45, group display name = app name. Play limits: title <= 55, description <= 80, benefits <= 40 each.

Slice 412 (2026-11-18): the app name no longer sits in the subscription title (the store shows it above the title, and Apple allows 30 characters); the Play titles below are the Apple names. Pushed to Google Play 2026-11-19: the 17 subscription listings carry these titles (no app name), read back equal; the 12 one-time book products say "in Dino: Kids Books & Stories". English: name/title "Every book" (it carried the app name before), description "Opens every book while you subscribe.", benefits "Every book opens" / "New books included".

Group localization for every Apple language: name and app name "Dino: Kids Books & Stories" (26 characters).

## de (Apple de-DE, Play de-DE)
- Apple name: Alle Bücher
- Play title: Alle Bücher
- Description: Öffnet alle Bücher, solange du abonnierst.
- Benefits: Alle Bücher öffnen sich / Neue Bücher inklusive

## fr (Apple fr-FR, Play fr-FR)
- Apple name: Tous les livres
- Play title: Tous les livres
- Description: Ouvre tous les livres pendant l'abonnement.
- Benefits: Tous les livres s'ouvrent / Nouveaux livres inclus

## es (Apple es-ES, Play es-ES)
- Apple name: Todos los libros
- Play title: Todos los libros
- Description: Abre todos los libros con la suscripción.
- Benefits: Todos los libros abiertos / Libros nuevos incluidos

## es-419 (Apple es-MX, Play es-419)
- Apple name: Todos los libros
- Play title: Todos los libros
- Description: Abre todos los libros con la suscripción.
- Benefits: Todos los libros abiertos / Libros nuevos incluidos

## nl (Apple nl-NL, Play nl-NL)
- Apple name: Alle boeken
- Play title: Alle boeken
- Description: Opent alle boeken zolang je geabonneerd bent.
- Benefits: Alle boeken open / Nieuwe boeken inbegrepen

## da (Apple da, Play da-DK)
- Apple name: Alle bøger
- Play title: Alle bøger
- Description: Åbner alle bøger, mens du abonnerer.
- Benefits: Alle bøger åbnes / Nye bøger er med

## sv (Apple sv, Play sv-SE)
- Apple name: Alla böcker
- Play title: Alla böcker
- Description: Öppnar alla böcker så länge du prenumererar.
- Benefits: Alla böcker öppnas / Nya böcker ingår

## nb (Apple no, Play no-NO)
- Apple name: Alle bøker
- Play title: Alle bøker
- Description: Åpner alle bøker så lenge du abonnerer.
- Benefits: Alle bøker åpnes / Nye bøker er med

## fi (Apple fi, Play fi-FI)
- Apple name: Kaikki kirjat
- Play title: Kaikki kirjat
- Description: Avaa kaikki kirjat tilauksen ajan.
- Benefits: Kaikki kirjat aukeavat / Uudet kirjat mukana

## pt-BR (Apple pt-BR, Play pt-BR)
- Apple name: Todos os livros
- Play title: Todos os livros
- Description: Abre todos os livros enquanto você assina.
- Benefits: Todos os livros abertos / Livros novos incluídos

## pt-PT (Apple pt-PT, Play pt-PT)
- Apple name: Todos os livros
- Play title: Todos os livros
- Description: Abre todos os livros enquanto assina.
- Benefits: Todos os livros abertos / Livros novos incluídos

## ru (Apple ru, Play ru-RU)
- Apple name: Все книги
- Play title: Все книги
- Description: Все книги открыты, пока есть подписка.
- Benefits: Открыты все книги / Новые книги включены

## ja (Apple ja, Play ja-JP)
- Apple name: すべての本
- Play title: すべての本
- Description: 定期購入中は、すべての本が読めます。
- Benefits: すべての本が読める / 新しい本も含まれます

## zh (Apple zh-Hans, Play zh-CN)
- Apple name: 所有书
- Play title: 所有书
- Description: 订阅期间可阅读所有书。
- Benefits: 所有书都可阅读 / 新书也包含在内

## ar (Apple ar-SA, Play ar)
- Apple name: كل الكتب
- Play title: كل الكتب
- Description: يفتح كل الكتب ما دام اشتراكك قائمًا.
- Benefits: كل الكتب مفتوحة / الكتب الجديدة مشمولة

## fa (Apple no App Store listing, Play fa)
- Apple name: همهٔ کتاب‌ها
- Play title: همهٔ کتاب‌ها
- Description: تا وقتی مشترک هستید همهٔ کتاب‌ها باز است.
- Benefits: همهٔ کتاب‌ها باز است / کتاب‌های تازه هم شامل است

