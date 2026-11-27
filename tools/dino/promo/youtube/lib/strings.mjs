// Slice 435 item 5: every piece of text the videos and their YouTube pages carry that is not the book's own text,
// in each of the app's 15 languages. The grown-ups line is the owner's approved English wording (note 2026-11-23) with
// no "buy", "download now" or price. Written by Claude, not read by a native speaker: the owner's
// native-speaker read of the first non-English video is a step in the slice's Not yet verified.

export const LANGUAGES = ['en', 'ar', 'da', 'de', 'es', 'fa', 'fi', 'fr', 'ja', 'nb', 'nl', 'pt', 'ru', 'sv', 'zh'];
export const RTL_LANGUAGES = ['ar', 'fa'];

/** Keys: name = the language in itself; grownUps = the closing line (spoken and shown); ai = credit line; kids = "read-aloud story for kids" (title suffix and thumbnail tag);
 *  intro = description first line with {title}; playlist = playlist title with {name}. */
export const STRINGS = {
  en: {
    name: 'English',
    grownUps: 'Grown-ups: more stories wait in the Dino app, where you can record your own voice for any character.',
    ai: 'Pictures and voices made with AI.',
    kids: 'Read-aloud story for kids',
    intro: 'Listen and read along: {title}. Each sentence lights up as it is read.',
    playlist: 'Dino Reader stories in {name}',
  },
  ar: {
    name: 'العربية',
    grownUps: 'للكبار: تنتظركم قصص أخرى في تطبيق Dino، حيث يمكنكم تسجيل صوتكم لأي شخصية.',
    ai: 'الصور والأصوات من صنع الذكاء الاصطناعي.',
    kids: 'قصة مقروءة للأطفال',
    intro: 'استمعوا واقرؤوا معنا: {title}. تضيء كل جملة أثناء قراءتها.',
    playlist: 'قصص دينو ريدر: {name}',
  },
  da: {
    name: 'Dansk',
    grownUps: 'Til de voksne: Flere historier venter i Dino-appen, hvor I kan indtale jeres egen stemme til en hvilken som helst figur.',
    ai: 'Billeder og stemmer er lavet med AI.',
    kids: 'Højtlæsning for børn',
    intro: 'Lyt og læs med: {title}. Hver sætning lyser op, mens den læses.',
    playlist: 'Dino Reader-historier på {name}',
  },
  de: {
    name: 'Deutsch',
    grownUps: 'Für Erwachsene: In der Dino-App warten weitere Geschichten, und dort können Sie für jede Figur Ihre eigene Stimme aufnehmen.',
    ai: 'Bilder und Stimmen wurden mit KI erstellt.',
    kids: 'Vorlesegeschichte für Kinder',
    intro: 'Zuhören und mitlesen: {title}. Jeder Satz leuchtet auf, während er vorgelesen wird.',
    playlist: 'Dino-Reader-Geschichten auf {name}',
  },
  es: {
    name: 'Español',
    grownUps: 'Para los mayores: en la app de Dino os esperan más cuentos, y allí podéis grabar vuestra propia voz para cualquier personaje.',
    ai: 'Imágenes y voces creadas con IA.',
    kids: 'Cuento leído en voz alta para niños',
    intro: 'Escucha y lee con nosotros: {title}. Cada frase se ilumina mientras se lee.',
    playlist: 'Cuentos de Dino Reader en {name}',
  },
  fa: {
    name: 'فارسی',
    grownUps: 'برای بزرگ‌ترها: قصه‌های بیشتری در برنامهٔ دینو منتظر شماست، جایی که می‌توانید صدای خودتان را برای هر شخصیت ضبط کنید.',
    ai: 'تصویرها و صداها با هوش مصنوعی ساخته شده‌اند.',
    kids: 'قصهٔ خوانده‌شده برای کودکان',
    intro: 'گوش بدهید و همراه ما بخوانید: {title}. هر جمله هنگام خوانده شدن روشن می‌شود.',
    playlist: 'قصه‌های دینو ریدر به {name}',
  },
  fi: {
    name: 'Suomi',
    grownUps: 'Aikuisille: Dino-sovelluksessa odottaa lisää satuja, ja siellä voit äänittää oman äänesi mille tahansa hahmolle.',
    ai: 'Kuvat ja äänet on tehty tekoälyllä.',
    kids: 'Satu lapsille ääneen luettuna',
    intro: 'Kuuntele ja lue mukana: {title}. Jokainen lause valaistuu, kun se luetaan.',
    playlist: 'Dino Reader -tarinat kielellä {name}',
  },
  fr: {
    name: 'Français',
    grownUps: 'Pour les grands : d’autres histoires vous attendent dans l’appli Dino, où vous pouvez enregistrer votre propre voix pour n’importe quel personnage.',
    ai: 'Images et voix créées avec l’IA.',
    kids: 'Histoire lue à voix haute pour les enfants',
    intro: 'Écoutez et lisez avec nous : {title}. Chaque phrase s’illumine pendant qu’elle est lue.',
    playlist: 'Histoires Dino Reader en {name}',
  },
  ja: {
    name: '日本語',
    grownUps: 'おとなの方へ。Dinoアプリには、ほかのおはなしもあります。どのキャラクターにも、ご自分の声を録音できます。',
    ai: '絵と声はAIで作りました。',
    kids: 'こどものための よみきかせ',
    intro: 'きいて、いっしょによもう: {title}。よまれている文が光ります。',
    playlist: '{name}のDino Readerおはなし',
  },
  nb: {
    name: 'Norsk',
    grownUps: 'Til de voksne: Flere historier venter i Dino-appen, hvor dere kan spille inn deres egen stemme til hvilken som helst figur.',
    ai: 'Bilder og stemmer er laget med KI.',
    kids: 'Høytlesning for barn',
    intro: 'Lytt og les med: {title}. Hver setning lyser opp mens den leses.',
    playlist: 'Dino Reader-historier på {name}',
  },
  nl: {
    name: 'Nederlands',
    grownUps: 'Voor de volwassenen: in de Dino-app wachten nog meer verhalen, en daar kun je je eigen stem opnemen voor elk personage.',
    ai: 'Plaatjes en stemmen zijn gemaakt met AI.',
    kids: 'Voorleesverhaal voor kinderen',
    intro: 'Luister en lees mee: {title}. Elke zin licht op terwijl hij wordt voorgelezen.',
    playlist: 'Dino Reader-verhalen in het {name}',
  },
  pt: {
    name: 'Português',
    grownUps: 'Para os adultos: mais histórias esperam por vocês na app do Dino, onde podem gravar a vossa própria voz para qualquer personagem.',
    ai: 'Imagens e vozes criadas com IA.',
    kids: 'História lida em voz alta para crianças',
    intro: 'Ouça e leia connosco: {title}. Cada frase acende-se enquanto é lida.',
    playlist: 'Histórias do Dino Reader em {name}',
  },
  ru: {
    name: 'Русский',
    grownUps: 'Для взрослых: в приложении Dino вас ждут другие истории, и там можно записать свой голос для любого героя.',
    ai: 'Картинки и голоса созданы с помощью ИИ.',
    kids: 'Сказка вслух для детей',
    intro: 'Слушайте и читайте вместе с нами: {title}. Каждое предложение подсвечивается, пока его читают.',
    playlist: 'Истории Dino Reader на языке: {name}',
  },
  sv: {
    name: 'Svenska',
    grownUps: 'Till de vuxna: Fler sagor väntar i Dino-appen, där ni kan spela in er egen röst till vilken figur som helst.',
    ai: 'Bilder och röster är skapade med AI.',
    kids: 'Sagoläsning för barn',
    intro: 'Lyssna och läs med: {title}. Varje mening lyser upp medan den läses.',
    playlist: 'Dino Reader-sagor på {name}',
  },
  zh: {
    name: '中文',
    grownUps: '写给大人：Dino应用里还有更多故事，您可以为任何角色录下自己的声音。',
    ai: '图片和声音由人工智能制作。',
    kids: '儿童故事朗读',
    intro: '一起听，一起读：{title}。每一句在朗读时都会亮起来。',
    playlist: 'Dino Reader {name}故事',
  },
};

export function stringsFor(language) {
  const strings = STRINGS[language];
  if (!strings) throw new Error(`No video strings for language '${language}'.`);
  return strings;
}

export function fill(template, values) {
  return template.replace(/\{(\w+)\}/g, (_, key) => {
    if (!(key in values)) throw new Error(`Template '${template}' needs {${key}}.`);
    return values[key];
  });
}
