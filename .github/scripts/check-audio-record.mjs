// Slice 409: a pull request that changes a Dino audio pack's clips must also change that language's listening track and the book's audio-cast.json record,
// or the clips would differ from the committed record and dino-store.yml would hold the book back after the merge (`<book>/record`). File names only: no LFS pull.
// Usage: node .github/scripts/check-audio-record.mjs <base-sha> [<head>]   (exit 1 and the fixing command when a language is missing its track or record)
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const CLIP_FILE = /^content\/dino\/([^/]+)\/audio\/([^/]+)\/([^/]+)$/;
const TRACK_NAME = /^listen-([^/]+)-v\d+\.m4a$/;
const RECORD_FILE = 'audio-cast.json';

/** Pure: the `{ book, language, missing }` entries for each pack whose clips changed without its track and/or the book's record. */
export function missingRecords(changedFiles) {
    const files = new Set(changedFiles);
    const clipsChanged = new Map();
    const trackChanged = new Set();
    for (const file of files) {
        const match = CLIP_FILE.exec(file);
        if (!match) continue;
        const [, book, language, name] = match;
        const key = `${book}/${language}`;
        if (TRACK_NAME.test(name)) trackChanged.add(key);
        else clipsChanged.set(key, { book, language });
    }
    return [...clipsChanged].flatMap(([key, { book, language }]) => {
        const missing = [];
        if (!trackChanged.has(key)) missing.push(`listen-${language}-v*.m4a`);
        if (!files.has(`content/dino/${book}/${RECORD_FILE}`)) missing.push(`${book}/${RECORD_FILE}`);
        return missing.length ? [{ book, language, missing }] : [];
    });
}

/** The message naming the book, the language and the command that fixes it. */
export function describe({ book, language, missing }) {
    return `${book} [${language}]: clips changed but this pull request does not change ${missing.join(' and ')}; run on the PC: dotnet run --project tools/dino/DinoReader.BookBuilder -- build-listen-all ${book} (needs ffmpeg and the book's clips pulled), then commit the result.`;
}

function main(base, head = 'HEAD') {
    const changed = execFileSync('git', ['diff', '--name-only', base, head], { encoding: 'utf8' }).split('\n').filter(Boolean);
    const problems = missingRecords(changed);
    for (const problem of problems) console.error(`::error::${describe(problem)}`);
    return problems.length === 0 ? 0 : 1;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    if (!process.argv[2]) {
        console.error('usage: check-audio-record.mjs <base-sha> [<head>]');
        process.exit(2);
    }
    process.exit(main(process.argv[2], process.argv[3]));
}
