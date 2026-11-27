// Slice 435 item 5: lists the spoken card lines (the title, the grown-ups line) the renderer looks for in voice/<lang>/, as JSON for make_voice.py.
//
//   node voice-lines.mjs [--book <stem,...>|all] [--lang <code,...>|all]      prints [{ "language", "key", "text", "book" }]
//
// One `grown-ups` line per language (the same for every book) and one `title-<stem>` line per book and language. The free list is read live, as render.mjs does.
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { valueOf } from './lib/args.mjs';
import { LANGUAGES, stringsFor } from './lib/strings.mjs';
import { fetchFreeStories, stemOfId, titleIn } from './lib/story.mjs';

const CONTENT_ROOT = fileURLToPath(new URL('../../../../content/dino/', import.meta.url));
const GROWN_UPS_KEY = 'grown-ups';

const listOf = (value, all) => (value === 'all' ? all : value.split(',').filter(Boolean));

export function parseArgs(argv) {
  const options = { book: 'all', lang: 'all' };
  for (let i = 0; i < argv.length; i += 1) {
    if (!['--book', '--lang'].includes(argv[i])) throw new Error(`Unknown argument ${argv[i]}`);
    options[argv[i].slice(2)] = valueOf(argv, i, argv[i]);
    i += 1;
  }
  return options;
}

/** A story's title line, or null (after a warning naming story and language) when that translation is missing. */
function titleLine(story, language, warn) {
  try {
    return { language, key: `title-${story.stem}`, text: titleIn(story, language), book: story.stem };
  } catch (error) {
    warn(`skipped title-${story.stem} in ${language}: ${error.message}`);
    return null;
  }
}

/** The lines for the given stories (each with `stem` and titles) and languages: one grown-ups line per language, one title line per story and language (a missing title translation is skipped with a warning). */
export function voiceLines(stories, languages, warn = console.error) {
  const lines = [];
  for (const language of languages) {
    lines.push({ language, key: GROWN_UPS_KEY, text: stringsFor(language).grownUps, book: null });
    for (const story of stories) {
      const line = titleLine(story, language, warn);
      if (line) lines.push(line);
    }
  }
  return lines;
}

/** The comma list as an array; an empty list or (for languages) an unknown code is an error, so an empty shell variable never looks like a finished run. */
export function checkedList(value, all, name) {
  const list = listOf(value, all);
  if (list.length === 0) throw new Error(`--${name} lists nothing`);
  if (name === 'lang') {
    const unknown = list.filter((code) => !LANGUAGES.includes(code));
    if (unknown.length) throw new Error(`--lang has unknown code(s): ${unknown.join(', ')}`);
  }
  return list;
}

export async function main(argv) {
  const options = parseArgs(argv);
  const languages = checkedList(options.lang, LANGUAGES, 'lang');
  const free = (await fetchFreeStories()).map((s) => ({ ...s, stem: stemOfId(CONTENT_ROOT, s.id) }));
  const stories = checkedList(options.book, free.map((s) => s.stem), 'book').map((stem) => free.find((s) => s.stem === stem) ?? (() => { throw new Error(`${stem} is not a free book now`); })());
  console.log(JSON.stringify(voiceLines(stories, languages), null, 2));
  return 0;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2)).then((code) => { process.exitCode = code; }, (error) => { console.error(error.message); process.exitCode = 1; });
}
