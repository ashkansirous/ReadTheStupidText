// Run: node --test tools/crashes/symbolicate.test.mjs
import test from "node:test";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import assert from "node:assert/strict";

import { normalizeUuid, parseTextFrames, parseIps, parseMetricKitFrames, readCrashInput, detectFormat } from "./crash-frames.mjs";
import { newestNdkLlvmBin, assetPattern, findLocalDsym, machoUuid, formatResults, linkAddress, parseSymbolizerOutput, parseTextVmaddr, releaseTag, symbolicateFrames, symbolicateText, symbolizerArgs, appOfFrames } from "./symbolicate.mjs";

const APP_UUID = "F1E2D3C4B5A6978899AABBCCDDEEFF00";
const FW_UUID = "0123456789ABCDEF0123456789ABCDEF";

const crashLog = `Incident Identifier: 1
Version:           1.90.0 (20261114.101)

Thread 0 Crashed:
0   libsystem_kernel.dylib        \t0x00000001e0a1b2c4 0x1e0a00000 + 111300
1   DinoReader.App                \t0x0000000102b4a1c0 0x102a30000 + 1155520
2   SherpaOnnxC                   \t0x0000000104100040 0x104000000 + 1048640

Binary Images:
0x102a30000 - 0x1050fffff DinoReader.App arm64  <f1e2d3c4b5a6978899aabbccddeeff00> /private/var/containers/Bundle/Application/X/DinoReader.app/DinoReader.App
0x104000000 - 0x104ffffff SherpaOnnxC arm64  <01234567-89ab-cdef-0123-456789abcdef> /private/var/containers/Bundle/Application/X/DinoReader.app/Frameworks/SherpaOnnxC.framework/SherpaOnnxC
0x1e0a00000 - 0x1e0b00000 libsystem_kernel.dylib arm64  <11111111111111111111111111111111> /usr/lib/system/libsystem_kernel.dylib
`;

const ips = `{"app_name":"DinoReader","bundleID":"uk.sirous.dinoreader"}
${JSON.stringify({
  bundleInfo: { CFBundleShortVersionString: "1.90.0", CFBundleVersion: "20261114.101" },
  usedImages: [{ name: "DinoReader.App", uuid: "f1e2d3c4-b5a6-9788-99aa-bbccddeeff00", base: 4339351552 }, { name: "libsystem_kernel.dylib", uuid: "11111111-1111-1111-1111-111111111111", base: 1 }],
  threads: [{ frames: [{ imageIndex: 1, imageOffset: 100 }, { imageIndex: 0, imageOffset: 1155520 }] }],
})}`;

const tree = (frame) => JSON.stringify({ callStacks: [{ threadAttributed: true, callStackRootFrames: frame }] });
const metricKitReport = {
  schemaVersion: 1, app: "dino", kind: "native", version: "1.85.3", build: "20261101.9",
  stack: tree({ binaryUUID: "F1E2D3C4-B5A6-9788-99AA-BBCCDDEEFF00", offsetIntoBinaryTextSegment: 1155520, binaryName: "DinoReader.App", address: 1, subFrames: [{ binaryUUID: FW_UUID, offsetIntoBinaryTextSegment: 64, binaryName: "SherpaOnnxC" }] }),
};

test("the uuid is normalized to 32 upper-case hex digits and rejects anything else", () => {
  assert.equal(normalizeUuid("<f1e2d3c4-b5a6-9788-99aa-bbccddeeff00>"), APP_UUID);
  assert.equal(normalizeUuid("nope"), null);
});

test("a text crash log gives frames tied to their image by name, with their load address", () => {
  const frames = parseTextFrames(crashLog);
  assert.equal(frames.length, 3);
  assert.deepEqual(frames[1], { image: "DinoReader.App", uuid: APP_UUID, loadAddress: 0x102a30000, address: 0x102b4a1c0, thread: 0, line: frames[1].line });
  assert.equal(frames[2].uuid, FW_UUID);
  assert.equal(readCrashInput(crashLog).version.version, "1.90.0");
});

test("an .ips log gives frames with offsets and the app version", () => {
  const read = parseIps(ips);
  assert.equal(read.version.build, "20261114.101");
  assert.equal(read.frames.length, 2);
  assert.equal(read.frames[1].offset, 1155520);
  assert.equal(detectFormat(ips), "ips");
});

test("a MetricKit report is read frame by frame, also when the JSON is cut off", () => {
  const read = readCrashInput(JSON.stringify(metricKitReport));
  assert.equal(read.app, "dino");
  assert.deepEqual(read.frames.map((f) => [f.uuid, f.offset]), [[APP_UUID, 1155520], [FW_UUID, 64]]);
  const cut = metricKitReport.stack.slice(0, metricKitReport.stack.length - 40);
  assert.equal(parseMetricKitFrames(cut)[0].uuid, APP_UUID);
});

test("the newest NDK's llvm bin is the fallback for llvm-symbolizer, null when there is none", () => {
  const sdk = mkdtempSync(join(tmpdir(), "sdk-"));
  assert.equal(newestNdkLlvmBin(sdk, "win32"), null);
  for (const version of ["29.0.14033849", "30.0.16248370", "9.0.1"]) mkdirSync(join(sdk, "ndk", version, "toolchains", "llvm", "prebuilt", "windows-x86_64", "bin"), { recursive: true });
  assert.equal(newestNdkLlvmBin(sdk, "win32"), join(sdk, "ndk", "30.0.16248370", "toolchains", "llvm", "prebuilt", "windows-x86_64", "bin"));
  assert.equal(newestNdkLlvmBin(sdk, "freebsd"), null);
});

test("a MetricKit report without binaryName names the frames of its own binaryUuid after the app", () => {
  const nameless = { schemaVersion: 1, app: "dino", kind: "native", version: "1.131.0", binaryUuid: APP_UUID,
    stack: tree({ binaryUUID: "F1E2D3C4-B5A6-9788-99AA-BBCCDDEEFF00", offsetIntoBinaryTextSegment: 7, subFrames: [{ binaryUUID: FW_UUID, offsetIntoBinaryTextSegment: 8 }] }) };
  const read = readCrashInput(JSON.stringify(nameless));
  assert.deepEqual(read.frames.map((f) => f.image), ["DinoReader", "?"]);
});

test("an App Store Connect crash-log answer is unwrapped to its log text", () => {
  const answer = JSON.stringify({ data: { attributes: { logText: crashLog } } });
  assert.equal(detectFormat(answer), "asc-log");
  assert.equal(readCrashInput(answer).frames.length, 3);
});

test("link address: offset frames add the segment start, address frames subtract the load address", () => {
  assert.equal(linkAddress({ offset: 16 }, 0x100000000), 0x100000010);
  assert.equal(linkAddress({ address: 0x102b4a1c0, loadAddress: 0x102a30000 }, 0x100000000), 0x10011a1c0 + 0x0);
});

test("the __TEXT vmaddr is read from llvm-objdump headers", () => {
  const out = "Load command 1\n      cmd LC_SEGMENT_64\n  segname __PAGEZERO\n   vmaddr 0x0000000000000000\nLoad command 2\n  segname __TEXT\n   vmaddr 0x0000000100000000\n   vmsize 0x1000";
  assert.equal(parseTextVmaddr(out), 0x100000000);
  assert.equal(parseTextVmaddr("nothing"), null);
});

test("symbolizer output drops unknown lines and the arguments name the dwarf file and the hex address", () => {
  assert.deepEqual(parseSymbolizerOutput("A.B() at /src/A.cs:12:3\n?? at ??:0\n"), ["A.B() at /src/A.cs:12:3"]);
  assert.deepEqual(symbolizerArgs("x/DWARF/App", 255), ["--obj=x/DWARF/App", "-p", "-f", "-i", "0xff"]);
});

test("release tag and asset pattern follow the names the iOS release job uses", () => {
  assert.equal(releaseTag("dino", "1.90.0"), "dino-v1.90.0");
  assert.equal(assetPattern(APP_UUID), `*-${APP_UUID}.dSYM.zip`);
  assert.equal(appOfFrames(parseTextFrames(crashLog)), "dino");
});

test("frames of our images are resolved through the stubbed dSYM and symbolizer; system frames are skipped; a missing dSYM is said plainly", async () => {
  const asked = [];
  const deps = {
    dwarfFor: async (uuid) => (uuid === APP_UUID ? "/dsym/DinoReader.App" : null),
    textVmaddr: async () => 0x100000000,
    symbolize: async (dwarf, address) => {
      asked.push([dwarf, address]);
      return address === 0x10011a1c0 ? ["DinoReader.ReadingSurface.Layout() at /src/ReadingSurface.cs:12:3"] : [];
    },
  };
  const results = await symbolicateFrames(parseTextFrames(crashLog), deps);
  assert.equal(results.length, 2);
  assert.deepEqual(asked, [["/dsym/DinoReader.App", 0x10011a1c0]]);
  const text = formatResults(results, "1.90.0");
  assert.match(text, /ReadingSurface\.Layout\(\) at \/src\/ReadingSurface\.cs:12:3/);
  assert.match(text, /no dSYM for SherpaOnnxC/);
});

test("the whole path from a crash file text to printed lines, with stubbed dependencies", async () => {
  const text = await symbolicateText(crashLog, {}, () => ({
    dwarfFor: async () => "/dsym/x",
    textVmaddr: async () => 0x100000000,
    symbolize: async () => ["Method() at /src/File.cs:1:1"],
  }));
  assert.equal(text.split("\n").length, 2);
  await assert.rejects(() => symbolicateText("no frames here", {}, () => ({})), /which app/);
});

// A thin 64-bit little-endian Mach-O with one filler load command and then LC_UUID.
function thinMacho(uuidHex) {
  const bytes = Buffer.alloc(32 + 24 + 24);
  bytes.writeUInt32LE(0xfeedfacf, 0);
  bytes.writeUInt32LE(2, 16);
  bytes.writeUInt32LE(48, 20);
  bytes.writeUInt32LE(0x19, 32);
  bytes.writeUInt32LE(24, 36);
  bytes.writeUInt32LE(0x1b, 56);
  bytes.writeUInt32LE(24, 60);
  Buffer.from(uuidHex, "hex").copy(bytes, 64);
  return bytes;
}

test("the UUID is read from a thin Mach-O, and a file that is not one has none", () => {
  assert.equal(machoUuid(thinMacho(APP_UUID)), APP_UUID);
  assert.equal(machoUuid(Buffer.from("not a macho file at all, but long enough to read a header from")), null);
});

test("--dsym-dir finds the app's dSYM by its DWARF file's UUID although the folder name has none, and a release-asset zip by its name", async () => {
  const root = mkdtempSync(join(tmpdir(), "dsym-"));
  const dwarf = join(root, "build", "DinoReader.App.app.dSYM", "Contents", "Resources", "DWARF");
  mkdirSync(dwarf, { recursive: true });
  writeFileSync(join(dwarf, "DinoReader.App"), thinMacho(APP_UUID));
  writeFileSync(join(root, `SherpaOnnxC-1.90.0-${FW_UUID}.dSYM.zip`), "not unpacked here");

  assert.equal(await findLocalDsym(root, APP_UUID, join(root, "cache")), join(root, "build", "DinoReader.App.app.dSYM"));
  assert.equal(await findLocalDsym(root, "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", join(root, "cache")), null);
});
