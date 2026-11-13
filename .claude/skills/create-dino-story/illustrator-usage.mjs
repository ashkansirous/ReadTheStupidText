// Measures one dino-story-illustrator run from its subagent transcript (a .jsonl under
// ~/.claude/projects/<project>/<session>/subagents/). Dependency-free Node (Slice 356).
// Usage: node illustrator-usage.mjs <subagent.jsonl> [more.jsonl ...]
// Prints turns, context size (first, median, peak, last), cache reads, image blocks by kind, calls by tool,
// pictures saved (a javascript_tool result of `image/jpeg <bytes>`, what `__grab` returns) and, per picture, seconds, context and cache reads.
import fs from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';
import { fileURLToPath } from 'node:url';

// Events are read from the RESULT of a javascript_tool call, never from the script text (a scanner over the input text was
// wrong twice: injected scripts mention `__grab` and contain regex literals with quotes).
//  - A picture is saved when `__grab` returned its blob list, e.g. `image/jpeg 2654604` (several: comma-joined). The
//    clipboard form `__grab(1, {clipboard:true})` returns the same line; its later `__clipResult` poll ("WxH") is not counted.
//  - The first real send is a `__setupPicture` result starting `ok:` or a `__sendChecked` result of exactly `SENT`
//    (never `dry-run ok:`, `REFUSED`, `MISSING`, `UNCONFIRMED` or `SENT WITHOUT TEXT`).
// The value is the first text part of the tool result; the harness appends a "Tab Context" part after it.
const GRAB_RESULT = /^image\/[\w.+-]+ \d+(,image\/[\w.+-]+ \d+)*$/;
const SEND_RESULT = /^(ok:|SENT$)/;

export const isGrabResult = value => GRAB_RESULT.test((value || '').trim());
export const isSendResult = value => SEND_RESULT.test((value || '').trim());
const IMAGE_FILE = /\.(jpe?g|png|webp)\b/i;
const SECONDS = 1000;

function contextOf(usage) {
  return (usage.input_tokens || 0) + (usage.cache_read_input_tokens || 0) + (usage.cache_creation_input_tokens || 0);
}

function imageKind(toolUse) {
  if (!toolUse) return 'other';
  const input = toolUse.input || {};
  if (toolUse.name === 'Read' && IMAGE_FILE.test(input.file_path || '')) return 'picture read';
  if (/screenshot/.test(toolUse.name)) return 'screenshot';
  if (/computer/.test(toolUse.name)) return input.action === 'zoom' ? 'zoom' : 'screenshot';
  return 'other';
}

async function readRun(file) {
  const run = { turns: [], calls: {}, images: {}, saves: [], first: null, last: null, firstSend: null };
  const seenMessages = new Map();
  const toolUses = {};
  const lines = readline.createInterface({ input: fs.createReadStream(file), crlfDelay: Infinity });
  for await (const line of lines) {
    let entry;
    try { entry = JSON.parse(line); } catch { continue; }
    if (entry.timestamp) {
      run.first = run.first || entry.timestamp;
      run.last = entry.timestamp;
    }
    if (entry.message) readMessage(run, entry, seenMessages, toolUses);
  }
  return run;
}

function readMessage(run, entry, seenMessages, toolUses) {
  const message = entry.message;
  if (entry.type === 'assistant' && message.usage) recordTurn(run, entry, seenMessages);
  const content = Array.isArray(message.content) ? message.content : [];
  for (const block of content) {
    if (block.type === 'tool_use') recordCall(run, entry, block, toolUses);
    if (block.type === 'tool_result') recordResult(run, entry, block, toolUses);
  }
}

function recordTurn(run, entry, seenMessages) {
  const key = entry.message.id || entry.uuid;
  const turn = { time: Date.parse(entry.timestamp), usage: entry.message.usage };
  if (seenMessages.has(key)) run.turns[seenMessages.get(key)] = turn;
  else { seenMessages.set(key, run.turns.length); run.turns.push(turn); }
}

function recordCall(run, entry, block, toolUses) {
  toolUses[block.id] = block;
  run.calls[block.name] = (run.calls[block.name] || 0) + 1;
}

function resultValue(parts) {
  if (typeof parts === 'string') return parts;
  const first = Array.isArray(parts) ? parts.find(p => p.type === 'text') : null;
  return first ? first.text : '';
}

// The turn index and time are those of the result (when the script finished), which is when the event happened.
function recordScriptResult(run, entry, block) {
  const value = resultValue(block.content);
  const at = { turnIndex: Math.max(0, run.turns.length - 1), time: Date.parse(entry.timestamp) };
  if (!run.firstSend && isSendResult(value)) run.firstSend = at;
  if (isGrabResult(value)) run.saves.push(at);
}

function recordResult(run, entry, block, toolUses) {
  const parts = Array.isArray(block.content) ? block.content : [];
  const call = toolUses[block.tool_use_id];
  if (call && /javascript_tool/.test(call.name)) recordScriptResult(run, entry, block);
  const kind = imageKind(call);
  for (const part of parts) {
    if (part.type === 'image') run.images[kind] = (run.images[kind] || 0) + 1;
  }
}

function median(values) {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)] || 0;
}

function sumCacheReads(turns, from, to) {
  let total = 0;
  for (let i = from; i <= to && i < turns.length; i++) total += turns[i].usage.cache_read_input_tokens || 0;
  return total;
}

// Set-up (before the first picture is sent: page checks, character sheets, line-up) is reported on its own, so picture 1
// is not charged for it. Without a real send (`ok:` or `SENT` result) the whole run before the first grab is picture 1.
function setUp(run) {
  const end = run.firstSend || { turnIndex: 0, time: Date.parse(run.first) };
  return {
    seconds: Math.round((end.time - Date.parse(run.first)) / SECONDS),
    turns: end.turnIndex,
    cacheReads: sumCacheReads(run.turns, 0, end.turnIndex - 1),
    startTurn: end.turnIndex,
    startTime: end.time,
  };
}

function perPicture(run, start) {
  const rows = [];
  let startTurn = start.startTurn;
  let startTime = start.startTime;
  run.saves.forEach((save, index) => {
    const lastTurn = run.turns.length - 1;
    const endTurn = Math.min(Math.max(startTurn, save.turnIndex), lastTurn);
    const slice = run.turns.slice(startTurn, endTurn + 1).map(t => contextOf(t.usage));
    const peak = slice.length ? Math.max(...slice) : 0;
    rows.push({
      picture: index + 1,
      seconds: Math.round((save.time - startTime) / SECONDS),
      turns: endTurn - startTurn + 1,
      context: peak,
      cacheReads: sumCacheReads(run.turns, startTurn, endTurn),
    });
    startTurn = endTurn + 1;
    startTime = save.time;
  });
  return rows;
}

function report(file, run) {
  if (!run.turns.length) {
    console.log(`${file}\nno assistant turns found`);
    return;
  }
  const contexts = run.turns.map(t => contextOf(t.usage));
  const cacheReads = sumCacheReads(run.turns, 0, run.turns.length - 1);
  const start = setUp(run);
  const rows = perPicture(run, start);
  const minutes = ((Date.parse(run.last) - Date.parse(run.first)) / SECONDS / 60).toFixed(1);
  console.log(file);
  console.log(`turns ${run.turns.length}, minutes ${minutes}, pictures saved ${run.saves.length}`);
  console.log(`context first ${contexts[0]}, median ${median(contexts)}, peak ${Math.max(...contexts)}, last ${contexts[contexts.length - 1]}`);
  console.log(`cache reads total ${cacheReads}`);
  console.log(`set-up before the first picture: ${start.seconds} s, ${start.turns} turns, cache reads ${start.cacheReads}`);
  console.log(`image blocks ${JSON.stringify(run.images)}`);
  console.log(`calls ${JSON.stringify(run.calls)}`);
  rows.forEach(r => console.log(`  picture ${r.picture}: ${r.seconds} s, ${r.turns} turns, context ${r.context}, cache reads ${r.cacheReads}`));
  if (rows.length) {
    console.log(`per picture: median ${median(rows.map(r => r.seconds))} s, cache reads ${Math.round(rows.reduce((sum, r) => sum + r.cacheReads, 0) / rows.length)} (set-up excluded)`);
  }
}

async function main(files) {
  if (!files.length) {
    console.error('usage: node illustrator-usage.mjs <subagent.jsonl> [more.jsonl ...]');
    process.exit(2);
  }
  for (const file of files) report(file, await readRun(file));
}

const isEntryScript = process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1]);
if (isEntryScript) main(process.argv.slice(2));
