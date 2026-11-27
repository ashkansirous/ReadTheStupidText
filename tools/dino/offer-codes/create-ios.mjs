// Creates (or imports) a batch of one-time-use App Store offer codes for Dino Reader's yearly subscription
// and saves every code in D:\secrets\dinoreader\offer-codes.
//   node create-ios.mjs --list-offers
//   node create-ios.mjs --offer <offerCodeId> --count 500 --expires 2026-12-22
//   node create-ios.mjs --import <oneTimeUseCodesId>
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
    asc, parseArgs, pullStore, commitStore, readBatches, writeBatches, today,
    Platform, STORE_DIR, DINO_YEARLY_SUBSCRIPTION_ID,
} from './lib.mjs';

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

async function listOffers() {
    const text = await asc('GET', `/v1/subscriptions/${DINO_YEARLY_SUBSCRIPTION_ID}/offerCodes?limit=50`);
    for (const o of JSON.parse(text).data) {
        const a = o.attributes;
        console.log(`${o.id}  "${a.name}"  ${a.duration} ${a.offerMode} x${a.numberOfPeriods}  active=${a.active}`);
    }
}

async function createBatch(offerId, count, expires) {
    if (!DATE_PATTERN.test(expires) || expires <= today()) throw new Error(`--expires must be a future YYYY-MM-DD, got ${expires}`);
    if (!Number.isInteger(count) || count < 1) throw new Error(`--count must be a positive whole number, got ${count}`);
    if (!(await dinoOfferIds()).has(offerId)) throw new Error(`offer ${offerId} is not on Dino's yearly subscription; see --list-offers`);
    const body = {
        data: {
            type: 'subscriptionOfferCodeOneTimeUseCodes',
            attributes: { numberOfCodes: count, expirationDate: expires, environment: 'PRODUCTION' },
            relationships: { offerCode: { data: { type: 'subscriptionOfferCodes', id: offerId } } },
        },
    };
    const created = JSON.parse(await asc('POST', '/v1/subscriptionOfferCodeOneTimeUseCodes', body));
    // Printed first: if anything below fails, rerun with --import <id> instead of creating another batch.
    console.log(`created batch ${created.data.id}; if saving fails, run: node create-ios.mjs --import ${created.data.id}`);
    return created.data.id;
}

async function dinoOfferIds() {
    const text = await asc('GET', `/v1/subscriptions/${DINO_YEARLY_SUBSCRIPTION_ID}/offerCodes?limit=50`);
    return new Set(JSON.parse(text).data.map((o) => o.id));
}

async function readBatchInfo(batchId) {
    const text = await asc('GET', `/v1/subscriptionOfferCodeOneTimeUseCodes/${batchId}?include=offerCode`);
    const json = JSON.parse(text);
    const offer = (json.included ?? [])[0];
    if (!offer || !(await dinoOfferIds()).has(offer.id)) throw new Error(`batch ${batchId} is not an offer on Dino's yearly subscription`);
    return { ...json.data.attributes, offerId: offer.id, offerName: offer.attributes?.name };
}

function parseCodes(csv, batchId, expected) {
    const rows = csv.split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
    const lines = rows.filter((l) => /^[A-Z0-9]+,https:\/\/\S+$/.test(l));
    const skipped = rows.filter((l) => !lines.includes(l));
    if (skipped.length > 1) throw new Error(`batch ${batchId}: ${skipped.length} unexpected CSV lines, first: ${skipped[0].slice(0, 80)}`);
    if (lines.length === 0) throw new Error(`batch ${batchId}: Apple returned no codes yet; rerun with --import ${batchId} in a few minutes`);
    if (lines.length !== expected) throw new Error(`batch ${batchId}: got ${lines.length} codes, Apple says ${expected}; rerun --import ${batchId} later`);
    return lines;
}

async function saveBatch(batchId) {
    const info = await readBatchInfo(batchId);
    const csv = await asc('GET', `/v1/subscriptionOfferCodeOneTimeUseCodes/${batchId}/values`);
    const lines = parseCodes(csv, batchId, info.numberOfCodes);
    const file = `${Platform.Ios}-${batchId}.csv`;
    writeFileSync(join(STORE_DIR, file), lines.join('\n') + '\n');
    const batches = readBatches().filter((b) => !(b.platform === Platform.Ios && b.id === batchId));
    batches.push({ platform: Platform.Ios, id: batchId, offerId: info.offerId, offerName: info.offerName,
        expires: info.expirationDate, count: lines.length, file, createdAt: info.createdDate });
    writeBatches(batches);
    commitStore(`dinoreader: iOS offer code batch ${batchId} (${lines.length} codes, expires ${info.expirationDate})`);
    console.log(`saved batch ${batchId}: ${lines.length} codes for "${info.offerName}", expires ${info.expirationDate}`);
}

function textArg(value, name) {
    if (value === true) throw new Error(`--${name} needs a value`);
    return String(value);
}

const args = parseArgs(process.argv.slice(2));
pullStore();
if (args['list-offers']) await listOffers();
else if (args.import) await saveBatch(textArg(args.import, 'import'));
else if (args.offer) await saveBatch(await createBatch(textArg(args.offer, 'offer'), Number(args.count), textArg(args.expires, 'expires')));
else throw new Error('usage: --list-offers | --offer <id> --count <n> --expires <YYYY-MM-DD> | --import <batchId>');
