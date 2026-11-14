// Run: node --test tools/crashes/collect-symbols.test.mjs
import test from "node:test";
import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { dsymAssetName, dsymProblems, dsymsOfBuild, ipaProblems, findDsyms, hasDwarf, inspectElf, nativeAssetName, parseArgs, parseDwarfdumpUuids, sherpaVersionOf } from "./collect-symbols.mjs";
import { assetPattern } from "./symbolicate.mjs";

// A minimal 64-bit little-endian ELF: header, a .shstrtab, a .note.gnu.build-id and (optionally) a .symtab.
function elf({ buildId, symtab }) {
  const names = ["", ".shstrtab", ".note.gnu.build-id", ".symtab"];
  const strtab = Buffer.from(names.join("\0") + "\0", "latin1");
  const nameAt = (n) => names.slice(0, n).reduce((sum, s) => sum + s.length + 1, 0);
  const id = Buffer.from(buildId, "hex");
  const note = Buffer.concat([Buffer.from([4, 0, 0, 0, id.length, 0, 0, 0, 3, 0, 0, 0]), Buffer.from("GNU\0"), id]);
  const sectionCount = symtab ? 4 : 3;
  const strOffset = 0x40;
  const noteOffset = strOffset + strtab.length;
  const headersOffset = Math.ceil((noteOffset + note.length) / 8) * 8;
  const file = Buffer.alloc(headersOffset + sectionCount * 64);
  file.writeUInt32LE(0x464c457f, 0);
  file[4] = 2;
  file[5] = 1;
  file.writeBigUInt64LE(BigInt(headersOffset), 0x28);
  file.writeUInt16LE(64, 0x3a);
  file.writeUInt16LE(sectionCount, 0x3c);
  file.writeUInt16LE(1, 0x3e);
  strtab.copy(file, strOffset);
  note.copy(file, noteOffset);
  const section = (index, nameIndex, type, offset, size) => {
    const at = headersOffset + index * 64;
    file.writeUInt32LE(nameAt(nameIndex), at);
    file.writeUInt32LE(type, at + 4);
    file.writeBigUInt64LE(BigInt(offset), at + 0x18);
    file.writeBigUInt64LE(BigInt(size), at + 0x20);
  };
  section(1, 1, 3, strOffset, strtab.length);
  section(2, 2, 7, noteOffset, note.length);
  if (symtab) section(3, 3, 2, 0, 0);
  return file;
}

test("only dSYMs whose UUID is in the .ipa are kept, and the app executable must have one (stale dSYMs never ship, Slice 243)", () => {
  const entries = [
    { folder: "/b/Debug/App.app.dSYM", uuids: ["OLD1"] },
    { folder: "/b/Release/App.app.dSYM", uuids: ["APP1"] },
    { folder: "/b/Release/Fw.framework.dSYM", uuids: ["FW1"] },
  ];
  const kept = dsymsOfBuild(entries, new Set(["APP1", "FW1", "OTHER"]));
  assert.deepEqual(kept.map((entry) => entry.folder), ["/b/Release/App.app.dSYM", "/b/Release/Fw.framework.dSYM"]);
  assert.deepEqual(ipaProblems(new Set(["APP1"]), new Set(["APP1", "FW1"])), []);
  assert.match(ipaProblems(new Set(["APP1"]), new Set(["FW1"]))[0], /no dSYM matches/);
  assert.match(ipaProblems(new Set(), new Set(["APP1"]))[0], /no UUID/);
});

test("dwarfdump output gives the UUID of each architecture in the release-asset form", () => {
  const out = "UUID: F1E2D3C4-B5A6-9788-99AA-BBCCDDEEFF00 (arm64) /x/DinoReader.App.app.dSYM/Contents/Resources/DWARF/DinoReader.App\n";
  assert.deepEqual(parseDwarfdumpUuids(out), ["F1E2D3C4B5A69788" + "99AABBCCDDEEFF00"]);
  assert.deepEqual(parseDwarfdumpUuids("error"), []);
});

test("the dSYM asset name carries the image name, the version and the UUID, and the symbolicator's pattern finds it", () => {
  const uuid = "F1E2D3C4B5A6978899AABBCCDDEEFF00";
  const name = dsymAssetName("DinoReader.App.app.dSYM", "1.90.0", uuid);
  assert.equal(name, `DinoReader.App.app-1.90.0-${uuid}.dSYM.zip`);
  assert.ok(name.endsWith(assetPattern(uuid).slice(1)));
});

test("a build with no dSYM, or without the app's own, is a problem; app plus framework is fine", () => {
  assert.equal(dsymProblems([]).length, 1);
  assert.match(dsymProblems(["SherpaOnnxC.framework.dSYM"])[0], /app's own dSYM/);
  assert.deepEqual(dsymProblems(["DinoReader.App.app.dSYM", "SherpaOnnxC.framework.dSYM"]), []);
});

test("dSYM folders are found at any depth and not searched inside", () => {
  const root = mkdtempSync(join(tmpdir(), "rtst-dsym-test-"));
  mkdirSync(join(root, "ios-arm64", "x.app.dSYM", "Contents", "Inner.dSYM"), { recursive: true });
  mkdirSync(join(root, "ios-arm64", "publish", "y.framework.dSYM"), { recursive: true });
  assert.deepEqual(findDsyms(root).map((p) => p.split(/[\\/]/).pop()).sort(), ["x.app.dSYM", "y.framework.dSYM"]);
});

test("an ELF's build id and symbol table are read, and a stripped one is reported as having no symtab", () => {
  assert.deepEqual(inspectElf(elf({ buildId: "00112233445566778899aabbccddeeff00112233", symtab: true })), { buildId: "00112233445566778899aabbccddeeff00112233", hasSymtab: true });
  assert.equal(inspectElf(elf({ buildId: "aabbccdd", symtab: false })).hasSymtab, false);
  assert.equal(inspectElf(Buffer.from("not an elf file at all, just text........................................")), null);
});

test("the sherpa version is read from the props file, and a props file without it is an error", () => {
  assert.equal(sherpaVersionOf("<PropertyGroup><SherpaOnnxVersion>9.9.9</SherpaOnnxVersion></PropertyGroup>"), "9.9.9");
  assert.throws(() => sherpaVersionOf("<Project/>"), /not found/);
});

test("native asset names say the app, version, ABI and build id", () => {
  assert.equal(nativeAssetName("DinoReader", "1.90.0", "00112233445566778899", "libonnxruntime.so"), "DinoReader-1.90.0-android-arm64-0011223344556677-libonnxruntime.so");
});

test("a dSYM folder without its DWARF file is not a copy of anything", () => {
  const root = mkdtempSync(join(tmpdir(), "hasdwarf-"));
  const empty = join(root, "a.app.dSYM");
  const full = join(root, "b.app.dSYM", "Contents", "Resources", "DWARF");
  mkdirSync(empty, { recursive: true });
  mkdirSync(full, { recursive: true });
  writeFileSync(join(full, "b"), "x");
  assert.equal(hasDwarf(empty), false);
  assert.equal(hasDwarf(join(root, "b.app.dSYM")), true);
});

test("parseArgs reads flag/value pairs and refuses an empty or missing value instead of shifting the next flag into it (Slice 243)", () => {
  assert.deepEqual(parseArgs(["ios-dsyms", "--root", "r", "--ipa", "a.ipa"]), { command: "ios-dsyms", root: "r", ipa: "a.ipa" });
  assert.throws(() => parseArgs(["ios-dsyms", "--out", "", "--ipa", "a.ipa"]), /--out needs a value/);
  assert.throws(() => parseArgs(["ios-dsyms", "--out", "--ipa", "a.ipa"]), /--out needs a value/);
  assert.throws(() => parseArgs(["ios-dsyms", "--ipa"]), /--ipa needs a value/);
  assert.throws(() => parseArgs(["ios-dsyms", "ipa", "a"]), /Expected a --flag/);
});
