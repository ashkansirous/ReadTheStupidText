// Slice 433 item 10: the App Store preview captions per listing language (preview-captions.json), the font each language's captions use
// (the way tools/emulator/store-captions.ps1 picks them: Fredoka; Andika for Cyrillic, since Fredoka has none; Vazirmatn for Arabic, right to left;
// the Windows UI fonts Yu Gothic UI Semibold and Microsoft YaHei UI for Japanese and Chinese), a glyph check of every line against that font's cmap
// (a missing glyph fails, never a silent fallback font), and the HyperFrames variable rows that render one preview per language.
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
export const CAPTIONS_FILE = join(HERE, 'preview-captions.json');
export const CAPTION_KEYS = ['cap1', 'cap2', 'cap3', 'cap4', 'cap5', 'cap6', 'endSub'];
const MAX_CAPTION_CHARS = 45;
const WINDOWS_FONTS = join(process.env.WINDIR ?? 'C:\\Windows', 'Fonts');

// The caption font of each language: the CSS family the composition's @font-face declares and the file its glyphs are checked in
// (a Windows font is found by its full name inside a .ttc collection).
const FONTS = {
    fredoka: { family: 'Fredoka', file: join(HERE, 'assets', 'fonts', 'Fredoka.ttf') },
    andika: { family: 'Andika', file: join(HERE, 'assets', 'fonts', 'Andika-Regular.ttf') },
    vazirmatn: { family: 'Vazirmatn', file: join(HERE, 'assets', 'fonts', 'Vazirmatn.ttf') },
    yugothic: { family: 'CapJa', file: join(WINDOWS_FONTS, 'YuGothB.ttc'), fullName: 'Yu Gothic UI Semibold' },
    yahei: { family: 'CapZh', file: join(WINDOWS_FONTS, 'msyh.ttc'), fullName: 'Microsoft YaHei UI' },
};
const LANGUAGE_FONT = { ru: 'andika', ar: 'vazirmatn', ja: 'yugothic', zh: 'yahei' };
const RIGHT_TO_LEFT = new Set(['ar']);

export function loadPreviewCaptions(file = CAPTIONS_FILE) {
    return JSON.parse(readFileSync(file, 'utf8'));
}

// Pure: the font key, CSS family and text direction of a language's captions.
export function captionStyle(lang) {
    const key = LANGUAGE_FONT[lang] ?? 'fredoka';
    return { fontKey: key, family: FONTS[key].family, dir: RIGHT_TO_LEFT.has(lang) ? 'rtl' : 'ltr' };
}

// Pure: why a language's lines cannot be used ('' = fine): every key present, non-empty, one line, at most 45 characters (the screenshot captions' limit).
export function linesProblem(lines) {
    for (const key of CAPTION_KEYS) {
        const text = lines?.[key];
        if (typeof text !== 'string' || text.trim() === '') return `${key} is missing`;
        if (/[\r\n]/.test(text)) return `${key} has a line break`;
        if ([...text].length > MAX_CAPTION_CHARS) return `${key} is longer than ${MAX_CAPTION_CHARS} characters`;
    }
    return '';
}

// Pure: the HyperFrames variables of one language's render (the ids the compositions declare in data-composition-variables).
export function previewVariables(data, lang) {
    const lines = data.lines?.[lang];
    const problem = linesProblem(lines);
    if (problem) throw new Error(`preview captions for '${lang}': ${problem}`);
    const style = captionStyle(lang);
    const vars = { lang, dir: style.dir, capFont: style.family };
    for (const key of CAPTION_KEYS) vars[key] = lines[key];
    return vars;
}

// Pure: the languages the App Store locales need, in first-seen order; throws when a locale names a language with no lines.
export function previewLanguages(data) {
    const langs = [...new Set(Object.values(data.appleLocales ?? {}))];
    const missing = langs.filter((lang) => !data.lines?.[lang]);
    if (missing.length) throw new Error(`no preview captions for ${missing.join(', ')}`);
    return langs;
}

const u16 = (b, o) => b.readUInt16BE(o);
const u32 = (b, o) => b.readUInt32BE(o);

// Pure: the table offsets of each face in a font file (a .ttc collection has several; a .ttf one).
export function fontFaces(b) {
    if (b.toString('latin1', 0, 4) !== 'ttcf') return [0];
    return Array.from({ length: u32(b, 8) }, (_, i) => u32(b, 12 + 4 * i));
}

function tableOffset(b, face, tag) {
    const count = u16(b, face + 4);
    for (let i = 0; i < count; i++) {
        const rec = face + 12 + 16 * i;
        if (b.toString('latin1', rec, rec + 4) === tag) return u32(b, rec + 8);
    }
    return -1;
}

// Pure: the Windows (platform 3) full names (name id 4) of one face.
export function faceFullNames(b, face) {
    const name = tableOffset(b, face, 'name');
    if (name < 0) return [];
    const count = u16(b, name + 2);
    const strings = name + u16(b, name + 4);
    const names = [];
    for (let i = 0; i < count; i++) {
        const rec = name + 6 + 12 * i;
        if (u16(b, rec) !== 3 || u16(b, rec + 6) !== 4) continue;
        const raw = b.subarray(strings + u16(b, rec + 10), strings + u16(b, rec + 10) + u16(b, rec + 8));
        names.push(Buffer.from(raw).swap16().toString('utf16le'));
    }
    return names;
}

// Only cmap formats 4 and 12 are read; any other format counts as "no glyph", the safe side (it fails the check, never passes a missing glyph).
function subtableHas(b, sub, cp) {
    const format = u16(b, sub);
    if (format === 12) {
        const groups = u32(b, sub + 12);
        for (let g = 0; g < groups; g++) {
            const at = sub + 16 + 12 * g;
            if (cp >= u32(b, at) && cp <= u32(b, at + 4)) return true;
        }
        return false;
    }
    if (format !== 4 || cp > 0xffff) return false;
    const segs = u16(b, sub + 6) / 2;
    const ends = sub + 14, starts = ends + 2 * segs + 2, deltas = starts + 2 * segs, ranges = deltas + 2 * segs;
    for (let i = 0; i < segs; i++) {
        if (cp > u16(b, ends + 2 * i)) continue;
        const start = u16(b, starts + 2 * i);
        if (cp < start) return false;
        const ro = u16(b, ranges + 2 * i);
        if (ro === 0) return ((cp + u16(b, deltas + 2 * i)) & 0xffff) !== 0;
        const glyph = u16(b, ranges + 2 * i + ro + 2 * (cp - start));
        return glyph !== 0 && ((glyph + u16(b, deltas + 2 * i)) & 0xffff) !== 0;
    }
    return false;
}

// Pure: true when a face's cmap (a Unicode subtable, format 4 or 12) maps the code point to a real glyph.
export function faceHasGlyph(b, face, cp) {
    const cmap = tableOffset(b, face, 'cmap');
    if (cmap < 0) throw new Error('font face has no cmap table');
    const count = u16(b, cmap + 2);
    for (let i = 0; i < count; i++) {
        const rec = cmap + 4 + 8 * i;
        const platform = u16(b, rec), encoding = u16(b, rec + 2);
        const unicode = platform === 0 || (platform === 3 && (encoding === 1 || encoding === 10));
        if (unicode && subtableHas(b, cmap + u32(b, rec + 4), cp)) return true;
    }
    return false;
}

// The face of a font (by full name inside a collection, else the only face) and its bytes.
function openFont(fontKey) {
    const font = FONTS[fontKey];
    if (!existsSync(font.file)) throw new Error(`caption font file ${font.file} is missing`);
    const b = readFileSync(font.file);
    const faces = fontFaces(b);
    if (!font.fullName) return { b, face: faces[0], name: font.family };
    const face = faces.find((f) => faceFullNames(b, f).includes(font.fullName));
    if (face === undefined) throw new Error(`${font.file} has no face named ${font.fullName}`);
    return { b, face, name: font.fullName };
}

// The characters of a language's lines that its caption font has no glyph for ('' = all covered; spaces are drawn by any font and skipped).
export function missingGlyphs(data, lang) {
    const { b, face } = openFont(captionStyle(lang).fontKey);
    const text = CAPTION_KEYS.map((key) => data.lines[lang][key]).join('');
    const missing = new Set([...text].filter((ch) => ch !== ' ' && !faceHasGlyph(b, face, ch.codePointAt(0))));
    return [...missing].map((ch) => `${ch} U+${ch.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')}`).join(', ');
}

// The batch rows of every App Store language, each glyph checked first; throws naming every language with a missing glyph.
export function previewRows(data = loadPreviewCaptions()) {
    const langs = previewLanguages(data);
    const failures = langs.map((lang) => [lang, missingGlyphs(data, lang)]).filter(([, missing]) => missing);
    if (failures.length) throw new Error(`caption glyphs missing: ${failures.map(([lang, m]) => `${lang} (${captionStyle(lang).family}): ${m}`).join('; ')}`);
    return langs.map((lang) => previewVariables(data, lang));
}
