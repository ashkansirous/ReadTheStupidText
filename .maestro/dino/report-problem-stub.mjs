#!/usr/bin/env node
// Slice 249: the local stand-in for POST /api/crash that .maestro/dino/report-problem-reaches-stub.yaml posts a user report to.
//
//   node .maestro/dino/report-problem-stub.mjs [--port 8787] [--code K7QX2MRD] [--out DIR]
//
// The emulator reaches the host as http://10.0.2.2:<port>. It answers 204 only for the report "Report a problem" sends when the flow has fixed the code with the
// `report-code` hook: kind user_report, the message `user report <code>`, the app's own trace lines (the page it was sent from) in the log tail, no stack. Anything else gets 400, so
// the app's marker "CRASH_REPORT user report sent (status 204), code <code>" proves the right report arrived whole. Every body is saved under --out
// (default: <temp>/dino-report-stub). Nothing is forwarded anywhere. Not a .yaml file, so tools/emulator/run-flows.ps1 does not take it for a flow.

import { createServer } from "node:http";
import { mkdirSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const CRASH_PATH = "/api/crash";
const DEFAULT_CODE = "K7QX2MRD";
const EXPECTED_TRACE_LINE = "page shown";
const MAX_BYTES = 64 * 1024;
const STUB_NAME = "report-stub";

/** True for the report the flow expects: a user report with the fixed code, no stack and the app's page trace lines in the tail. */
export function isExpectedUserReport(report, code = DEFAULT_CODE) {
  return (
    report?.schemaVersion === 1 &&
    report.app === "dino" &&
    report.kind === "user_report" &&
    report.message === `user report ${code}` &&
    String(report.stack ?? "") === "" &&
    String(report.logTail ?? "").includes(EXPECTED_TRACE_LINE)
  );
}

function parseArgs(argv) {
  const args = { port: 8787, code: DEFAULT_CODE, out: join(tmpdir(), "dino-report-stub") };
  for (let i = 0; i < argv.length; i += 2) args[argv[i].replace(/^--/, "")] = argv[i + 1];
  args.port = Number(args.port);
  return args;
}

/** Removes the report-N.json files an earlier stub left in outDir (only those; the folder may not exist yet). */
export function clearOldReports(outDir) {
  mkdirSync(outDir, { recursive: true });
  for (const name of readdirSync(outDir)) if (/^report-\d+\.json$/.test(name)) rmSync(join(outDir, name));
}

/** The number for the next report: one past the highest report-N.json in outDir, so a stub that keeps running starts again at report-1 after its folder was cleared. */
export function nextReportNumber(outDir) {
  mkdirSync(outDir, { recursive: true }); // the folder may have been deleted while the stub runs
  let highest = 0;
  for (const name of readdirSync(outDir)) {
    const match = /^report-(\d+)\.json$/.exec(name);
    if (match) highest = Math.max(highest, Number(match[1]));
  }
  return highest + 1;
}

export function startStub(port, outDir, code = DEFAULT_CODE) {
  mkdirSync(outDir, { recursive: true });
  const server = createServer((request, response) => {
    const chunks = [];
    let size = 0;
    request.on("data", (chunk) => {
      size += chunk.length;
      if (size <= MAX_BYTES) chunks.push(chunk);
    });
    request.on("end", () => {
      let verdict = 400;
      if (request.method === "POST" && request.url === CRASH_PATH && size <= MAX_BYTES) {
        const text = Buffer.concat(chunks).toString("utf8");
        writeFileSync(join(outDir, `report-${nextReportNumber(outDir)}.json`), text);
        try {
          verdict = isExpectedUserReport(JSON.parse(text), code) ? 204 : 400;
        } catch {
          verdict = 400;
        }
      }
      console.log(`${new Date().toISOString()} ${request.method} ${request.url} ${size} bytes -> ${verdict}`);
      response.writeHead(verdict, { "X-Stub": STUB_NAME }).end(); // Slice 320: tools/emulator/need-stubs.ps1 tells this stub from the crash stub (same port) by this header
    });
  });
  return new Promise((resolve) => server.listen(port, "0.0.0.0", () => resolve(server)));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const { port, out, code } = parseArgs(process.argv.slice(2));
  clearOldReports(out); // Slice 320: a new stub counts from report-1, so reports left by an earlier run must not sit beside the new ones
  await startStub(port, out, code);
  console.log(`Report stub listening on :${port} for code ${code}, saving bodies under ${out} (the emulator uses http://10.0.2.2:${port})`);
}
