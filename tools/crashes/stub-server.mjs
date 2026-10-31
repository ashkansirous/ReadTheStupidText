#!/usr/bin/env node
// Slice 227: the local stand-in for POST /api/crash that the scripted emulator check (.maestro/*/crash-report-reaches-stub.yaml) posts to.
//
//   node tools/crashes/stub-server.mjs [--port 8787] [--out DIR]
//
// The emulator reaches the host as http://10.0.2.2:<port>. It answers 204 only for the report the Debug crash hook throws (kind managed, an
// InvalidOperationException "Slice 227 crash test", with the hook's known line in the log tail), and 400 for anything else, so the app's Debug marker
// "CRASH_REPORT report sent (status 204)" proves the right report arrived whole. Every body is saved under --out (default: <temp>/rtst-crash-stub) and
// each request is printed with its verdict. Nothing is forwarded anywhere.

import { createServer } from "node:http";
import { mkdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const CRASH_PATH = "/api/crash";
const EXPECTED_TYPE = "System.InvalidOperationException";
const EXPECTED_MESSAGE = "Slice 227 crash test";
const EXPECTED_LOG_LINE = "crash test about to throw";
const MAX_BYTES = 64 * 1024;

/** True for the report the Debug crash hook produces: managed, the right exception, and its known line in the log tail. */
export function isExpectedReport(report) {
  return (
    report?.schemaVersion === 1 &&
    report.kind === "managed" &&
    report.exceptionType === EXPECTED_TYPE &&
    String(report.message).includes(EXPECTED_MESSAGE) &&
    String(report.logTail).includes(EXPECTED_LOG_LINE)
  );
}

function parseArgs(argv) {
  const args = { port: 8787, out: join(tmpdir(), "rtst-crash-stub") };
  for (let i = 0; i < argv.length; i += 2) args[argv[i].replace(/^--/, "")] = argv[i + 1];
  args.port = Number(args.port);
  return args;
}

export function startStub(port, outDir) {
  mkdirSync(outDir, { recursive: true });
  let count = 0;
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
        writeFileSync(join(outDir, `report-${++count}.json`), text);
        try {
          verdict = isExpectedReport(JSON.parse(text)) ? 204 : 400;
        } catch {
          verdict = 400;
        }
      }
      console.log(`${new Date().toISOString()} ${request.method} ${request.url} ${size} bytes -> ${verdict}`);
      response.writeHead(verdict).end();
    });
  });
  return new Promise((resolve) => server.listen(port, "0.0.0.0", () => resolve(server)));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const { port, out } = parseArgs(process.argv.slice(2));
  await startStub(port, out);
  console.log(`Crash stub listening on :${port}, saving bodies under ${out} (the emulator uses http://10.0.2.2:${port})`);
}
