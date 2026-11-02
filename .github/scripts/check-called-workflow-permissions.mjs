#!/usr/bin/env node
// Slice 242 item 7: a reusable workflow cannot ask for more token permissions than the caller's job grants.
// PR #702 shipped a dinoreader-windows.yml job asking `actions: write` while release.yml's build-dinowin job granted only
// `contents: write`: every release on main ended `startup_failure` until hotfix #704, and actionlint did not catch it.
//
// For each `uses: ./.github/workflows/<file>.yml` job in any workflow, this fails when the called workflow (its top-level
// permissions, and each of its jobs' permissions) asks for a permission at a level above what the caller job grants.
// The caller's grant is its job-level `permissions:`, else its workflow-level one; a caller with neither is skipped
// (the repository default applies and cannot be read from the files).
//
// Usage: node check-called-workflow-permissions.mjs [workflows-dir]   (default .github/workflows; exit 1 on a problem)
import { readdirSync, readFileSync } from 'node:fs';
import { join, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const LEVELS = { none: 0, read: 1, write: 2 };
const LOCAL_WORKFLOW_PREFIX = './.github/workflows/';
const READ_ALL = 'read-all';
const WRITE_ALL = 'write-all';

function stripComment(line) {
  return line.replace(/(^|\s)#.*$/, '').replace(/\s+$/, '');
}

function indentOf(line) {
  return line.length - line.trimStart().length;
}

// The permissions value that starts on line `index` (`permissions:` already matched): a scalar, `{}`, or a block map.
// Returns { value, next } where value is { all: 'read'|'write' } | { map: {name: level} } and next is the first line after it.
// A shape this reader cannot read is never passed over: it goes to `unparsed`, and the check fails on it (a silent skip would print "asks for no more than its caller grants").
function addEntry(map, text, unparsed, where) {
  const match = text.trim().match(/^['"]?([\w-]+)['"]?\s*:\s*['"]?(\w+)['"]?$/);
  if (match && match[2] in LEVELS) map[match[1]] = match[2];
  else unparsed.push(`${where}: cannot read the permissions entry "${text.trim()}" (expected "name: read|write|none")`);
}

function readPermissions(lines, index, keyIndent, unparsed, where) {
  const inline = lines[index].slice(lines[index].indexOf(':') + 1).trim();
  if (inline === READ_ALL) return { value: { all: 'read' }, next: index + 1 };
  if (inline === WRITE_ALL) return { value: { all: 'write' }, next: index + 1 };
  const map = {};
  if (inline.startsWith('{') && inline.endsWith('}')) {
    for (const pair of inline.slice(1, -1).split(',')) if (pair.trim()) addEntry(map, pair, unparsed, where);
    return { value: { map }, next: index + 1 };
  }
  if (inline !== '') unparsed.push(`${where}: cannot read the permissions value "${inline}"`);
  let next = index + 1;
  while (next < lines.length && (lines[next].trim() === '' || indentOf(lines[next]) > keyIndent)) {
    if (lines[next].trim() !== '') addEntry(map, lines[next], unparsed, where);
    next += 1;
  }
  return { value: { map }, next };
}

// Minimal reader for the parts of a workflow this check needs (no YAML dependency in this repo's scripts).
export function parseWorkflow(text) {
  const lines = text.split(/\r?\n/).map(stripComment);
  const result = { topPermissions: null, jobs: {}, unparsed: [] };
  let inJobs = false;
  let job = null;
  let jobIndent = null;   // the indentation of the job names: the first line under `jobs:` sets it (2, 4 or tabs-free any width)
  let propIndent = null;  // the indentation of one job's keys: the first line under a job name sets it
  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    if (line.trim() === '') continue;
    const indent = indentOf(line);
    if (indent === 0) {
      inJobs = /^jobs:\s*$/.test(line);
      job = null;
      jobIndent = null;
      if (/^permissions:/.test(line)) {
        const read = readPermissions(lines, index, 0, result.unparsed, 'workflow permissions');
        result.topPermissions = read.value;
        index = read.next - 1;
      }
    } else if (inJobs && (jobIndent === null || indent === jobIndent)) {
      jobIndent = indent;
      propIndent = null;
      const name = line.trim().match(/^([\w-]+):\s*$/);
      job = name ? (result.jobs[name[1]] = { name: name[1], permissions: null, uses: null }) : null;
      if (!name) result.unparsed.push(`cannot read the job line "${line.trim()}"`);
    } else if (inJobs && job && indent > jobIndent) {
      if (propIndent === null) propIndent = indent;
      if (indent !== propIndent) continue;
      if (/^\s*permissions:/.test(line)) {
        const read = readPermissions(lines, index, propIndent, result.unparsed, `job ${job.name}`);
        job.permissions = read.value;
        index = read.next - 1;
      } else {
        const uses = line.trim().match(/^uses:\s*['"]?(\S+?)['"]?$/);
        if (uses) job.uses = uses[1];
      }
    }
  }
  return result;
}

// The level a permission set grants for one permission name (a block map lists only what it grants: the rest is none).
function levelOf(permissions, name) {
  if (permissions.all) return LEVELS[permissions.all];
  return LEVELS[permissions.map[name] ?? 'none'] ?? 0;
}

// The [name, level] pairs a block asks for; read-all / write-all ask for every permission, so the caller must grant the same all.
function asksOf(permissions) {
  if (permissions.all) return [['*', LEVELS[permissions.all]]];
  return Object.entries(permissions.map).map(([name, level]) => [name, LEVELS[level] ?? 0]);
}

function grantedLevel(grant, name) {
  return name === '*' ? (grant.all ? LEVELS[grant.all] : 0) : levelOf(grant, name);
}

// What a caller job grants: its own block, else the workflow's; null when neither is written.
function grantOf(callerJob, caller) {
  return callerJob.permissions ?? caller.topPermissions;
}

// Every [where, permissions] a called workflow asks for: each job's block, and the top-level block only when some job has none of its own
// (a job's own block replaces the top-level one, so a top-level `contents: write` that every job overrides asks for nothing).
function requestsOf(called) {
  const jobs = Object.values(called.jobs);
  const found = jobs.map((job) => [`job ${job.name}`, job.permissions]);
  if (jobs.some((job) => !job.permissions)) found.push(['workflow permissions', called.topPermissions]);
  return found.filter(([, permissions]) => permissions);
}

function problemsFor(callerFile, callerJob, calledFile, called, grant) {
  const problems = [];
  for (const [where, permissions] of requestsOf(called)) {
    for (const [name, asked] of asksOf(permissions)) {
      const granted = grantedLevel(grant, name);
      if (asked <= granted) continue;
      const levelName = (level) => Object.keys(LEVELS).find((key) => LEVELS[key] === level);
      problems.push(`${callerFile} job ${callerJob.name} calls ${calledFile}: its ${where} asks for ${name}: ${levelName(asked)}, the caller job grants ${levelName(granted)}.`);
    }
  }
  return problems;
}

export function checkWorkflows(workflows) {
  const parsed = new Map(Object.entries(workflows).map(([file, text]) => [file, parseWorkflow(text)]));
  const problems = [];
  const involved = new Set();
  for (const [callerFile, caller] of parsed) {
    for (const callerJob of Object.values(caller.jobs)) {
      if (!callerJob.uses?.startsWith(LOCAL_WORKFLOW_PREFIX)) continue;
      const calledFile = callerJob.uses.slice(LOCAL_WORKFLOW_PREFIX.length);
      involved.add(callerFile).add(calledFile);
      const called = parsed.get(calledFile);
      const grant = grantOf(callerJob, caller);
      if (!called) problems.push(`${callerFile} job ${callerJob.name} calls ${calledFile}, which was not found.`);
      else if (grant) problems.push(...problemsFor(callerFile, callerJob, calledFile, called, grant));
    }
  }
  // A caller or called workflow in a shape the reader cannot read is a failure, not a pass: the answer above may be missing permissions.
  for (const file of involved) for (const text of parsed.get(file)?.unparsed ?? []) problems.push(`${file}: ${text}. Write it as "name: read|write|none" lines (or fix this check).`);
  return problems;
}

export function readWorkflowDir(dir) {
  const files = readdirSync(dir).filter((name) => /\.ya?ml$/.test(name));
  return Object.fromEntries(files.map((name) => [name, readFileSync(join(dir, name), 'utf8')]));
}

function main() {
  const dir = process.argv[2] ?? '.github/workflows';
  const problems = checkWorkflows(readWorkflowDir(dir));
  if (problems.length === 0) {
    console.log(`Every called workflow in ${dir} asks for no more than its caller job grants.`);
    return;
  }
  for (const problem of problems) console.error(problem);
  console.error('A reusable workflow cannot ask for more than the caller job grants: the run ends startup_failure. Raise the caller job permissions (or lower the called ones).');
  process.exitCode = 1;
}

if (process.argv[1] && basename(process.argv[1]) === basename(fileURLToPath(import.meta.url))) main();
