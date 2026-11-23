// Hands out the next 1-3 unused offer codes and records them as given in D:\secrets\dinoreader\offer-codes\given.csv.
//   node fetch.mjs --platform ios --count 1 [--note "for Sara"]
//   node fetch.mjs --mark-given <code> --platform ios [--note "..."]   (record a code handed out another way)
//   node fetch.mjs --stock                                              (unused codes left per batch)
import {
    parseArgs, pullStore, commitStore, readBatches, readGiven, appendGiven, readBatchCodes, isLive, Platform,
} from './lib.mjs';

const MAX_PER_FETCH = 3;

function liveBatches(platform) {
    return readBatches()
        .filter((b) => b.platform === platform && isLive(b))
        .sort((a, b) => a.expires.localeCompare(b.expires));
}

function nextUnused(platform, count) {
    const given = readGiven();
    const picked = [];
    for (const batch of liveBatches(platform)) {
        for (const c of readBatchCodes(batch)) {
            if (picked.length === count) return picked;
            if (!given.has(c.code)) picked.push({ ...c, batch: batch.id, expires: batch.expires, platform });
        }
    }
    return picked;
}

function fetchCodes(platform, count, note) {
    if (!Number.isInteger(count) || count < 1 || count > MAX_PER_FETCH) throw new Error(`--count must be 1-${MAX_PER_FETCH}`);
    const picked = nextUnused(platform, count);
    if (picked.length < count) throw new Error(`only ${picked.length} unused ${platform} codes left; create a new batch first`);
    appendGiven(picked.map((p) => ({ ...p, givenAt: new Date().toISOString(), note })));
    for (const p of picked) console.log(`${p.code}  ${p.link}  (redeem by ${p.expires})`);
    commitStore(`dinoreader: hand out ${count} ${platform} offer code(s)`);
}

function markGiven(platform, rawCode, note) {
    const code = rawCode.trim().toUpperCase();
    const batch = readBatches().filter((b) => b.platform === platform).find((b) => readBatchCodes(b).some((c) => c.code === code));
    if (!batch) throw new Error(`code ${code} is in no saved ${platform} batch`);
    if (readGiven().has(code)) return console.log(`${code} already recorded as given`);
    appendGiven([{ code, platform, batch: batch.id, expires: batch.expires, givenAt: new Date().toISOString(), note }]);
    commitStore(`dinoreader: record ${platform} offer code as given`);
    console.log(`${code} recorded as given`);
}

function stock() {
    const given = readGiven();
    for (const b of readBatches()) {
        const left = readBatchCodes(b).filter((c) => !given.has(c.code)).length;
        console.log(`${b.platform} batch ${b.id} "${b.offerName}": ${left}/${b.count} unused, expires ${b.expires} (${isLive(b) ? 'live' : 'expired or too close'})`);
    }
}

function textArg(value, name) {
    if (value === undefined) return undefined;
    if (value === true) throw new Error(`--${name} needs a value`);
    return String(value);
}

const args = parseArgs(process.argv.slice(2));
const platform = String(args.platform ?? Platform.Ios);
if (!Object.values(Platform).includes(platform)) throw new Error(`--platform must be one of: ${Object.values(Platform).join(', ')}`);
const note = textArg(args.note, 'note');
pullStore();
if (args.stock) stock();
else if (args['mark-given']) markGiven(platform, textArg(args['mark-given'], 'mark-given'), note);
else fetchCodes(platform, Number(textArg(args.count, 'count') ?? 1), note);
