#!/usr/bin/env node
// Slice 243 items 1 and 7: collect the files that turn a release build's crash into readable code lines, named so tools/crashes/symbolicate.mjs finds them, and
// leave them in a folder the release job uploads as Release assets (never Actions artifacts; .github/CLAUDE.md).
//
//   node tools/crashes/collect-symbols.mjs ios-dsyms --root <project>/bin --ipa <the staged .ipa> --version x.y.z --out dist/symbols
//       zips every *.dSYM under --root (the app's `<App>.app.dSYM` and each `<Name>.framework.dSYM`) whose UUID is one of the .ipa's images, as `<Name>-<version>-<UUID>.dSYM.zip`
//       (UUID: 32 upper-case hex digits from `dwarfdump --uuid`). Fails when the app's own dSYM is missing or does not match the .ipa's executable (stale dSYMs from another
//       build are never uploaded), so a build that lost its symbols never ships quietly. macOS only (unzip, plutil, dwarfdump, ditto).
//   node tools/crashes/collect-symbols.mjs android-natives --version x.y.z --name DinoReader --out dist/symbols [--packages DIR]
//       copies the two prebuilt arm64 .so files the app ships (libsherpa-onnx-c-api.so, libonnxruntime.so) from the restored NuGet package folder, as
//       `<name>-<version>-android-arm64-<build id or file hash>-<file>.so`, and prints for each whether it still has a symbol table (.symtab).
//       The sherpa-onnx version is read from --props (default src/Shared/SherpaOnnxVersion.props); --packages defaults to $NUGET_PACKAGES or ~/.nuget/packages.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { basename, join } from "node:path";
import { pathToFileURL } from "node:url";

const APP_DSYM_SUFFIX = ".app.dSYM";
const ANDROID_PACKAGE = "org.k2fsa.sherpa.onnx.runtime.android-arm64";
// The two prebuilt libraries the app ships (src/Shared/AndroidSherpaPackaging.props, AndroidNativeLibrary items); other files in the package are not in the APK.
const ANDROID_NATIVE_FILES = ["libsherpa-onnx-c-api.so", "libonnxruntime.so"];
const SHT_SYMTAB = 2;
const ELF_MAGIC = 0x464c457f;

/** The UUIDs `dwarfdump --uuid` printed (one line per architecture), as 32 upper-case hex digits. */
export function parseDwarfdumpUuids(output) {
  return [...output.matchAll(/^UUID:\s+([0-9A-Fa-f-]{36})\b/gm)].map((match) => match[1].replace(/-/g, "").toUpperCase());
}

/** `<Name>-<version>-<UUID>.dSYM.zip` for the dSYM folder `folderName` (`DinoReader.App.app.dSYM`, `SherpaOnnxC.framework.dSYM`). */
export function dsymAssetName(folderName, version, uuid) {
  return `${folderName.replace(/\.dSYM$/, "")}-${version}-${uuid}.dSYM.zip`;
}

/** The dSYM folders under `root` (recursive; a dSYM's own contents are not searched). */
export function findDsyms(root) {
  const found = [];
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const path = join(root, entry.name);
    if (!entry.isDirectory()) continue;
    if (entry.name.endsWith(".dSYM")) found.push(path);
    else found.push(...findDsyms(path));
  }
  return found;
}

/** The problems with `names` (dSYM folder names): a missing app dSYM, or none at all. Empty when fine. */
export function dsymProblems(names) {
  if (names.length === 0) return ["no .dSYM found: the Release build produced no debug symbols"];
  return names.some((name) => name.endsWith(APP_DSYM_SUFFIX)) ? [] : [`the app's own dSYM (*${APP_DSYM_SUFFIX}) is missing; found: ${names.join(", ")}`];
}

/** True when the dSYM folder holds its DWARF file (`Contents/Resources/DWARF/<binary>`); an empty shell must not be uploaded or count as the copy of its UUID. */
export function hasDwarf(folder) {
  const dir = join(folder, "Contents", "Resources", "DWARF");
  return existsSync(dir) && readdirSync(dir).length > 0;
}

/**
 * The dSYMs ({ folder, uuids }) that belong to the shipped build: those with at least one UUID among the .ipa's images (`ipaUuids`). Stale dSYMs left in the bin folder
 * (Debug, an earlier build) match no image and are left out.
 */
export function dsymsOfBuild(entries, ipaUuids) {
  return entries.filter((entry) => entry.uuids.some((uuid) => ipaUuids.has(uuid)));
}

/** The problems with matching the .ipa's app executable (`appUuids`) to the dSYM UUIDs collected (`dsymUuids`): empty when a dSYM matches it. */
export function ipaProblems(appUuids, dsymUuids) {
  if (appUuids.size === 0) return ["the .ipa's app executable has no UUID"];
  const missing = [...appUuids].filter((uuid) => !dsymUuids.has(uuid));
  return missing.length ? [`no dSYM matches the .ipa's app executable (UUID ${missing.join(", ")}); the dSYMs found are from another build`] : [];
}

// The UUIDs of the app executable and of the frameworks' binaries inside an .ipa, read from an unpacked copy (macOS: unzip, plutil, dwarfdump).
function readIpaUuids(ipa) {
  const dir = mkdtempSync(join(tmpdir(), "rtst-ipa-"));
  try {
    execFileSync("unzip", ["-q", ipa, "-d", dir]);
    const payload = join(dir, "Payload");
    const app = join(payload, readdirSync(payload).find((name) => name.endsWith(".app")) ?? "");
    const executable = execFileSync("plutil", ["-extract", "CFBundleExecutable", "raw", "-o", "-", join(app, "Info.plist")], { encoding: "utf8" }).trim();
    const uuidsOf = (file) => parseDwarfdumpUuids(execFileSync("dwarfdump", ["--uuid", file], { encoding: "utf8" }));
    const appUuids = new Set(uuidsOf(join(app, executable)));
    const all = new Set(appUuids);
    const frameworks = join(app, "Frameworks");
    for (const name of existsSync(frameworks) ? readdirSync(frameworks).filter((n) => n.endsWith(".framework")) : []) {
      const binary = join(frameworks, name, name.replace(/\.framework$/, ""));
      if (existsSync(binary)) uuidsOf(binary).forEach((uuid) => all.add(uuid));
    }
    return { appUuids, all };
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

function collectIosDsyms({ root, ipa, version, out }) {
  if (!ipa) throw new Error("--ipa is required: the dSYMs are matched to the shipped executable's UUID");
  const found = findDsyms(root).filter(hasDwarf).map((folder) => ({ folder, uuids: parseDwarfdumpUuids(execFileSync("dwarfdump", ["--uuid", folder], { encoding: "utf8" })) }));
  const withoutUuid = found.find((entry) => entry.uuids.length === 0);
  if (withoutUuid) throw new Error(`dwarfdump found no UUID in ${withoutUuid.folder}`);
  const shipped = readIpaUuids(ipa);
  const entries = dsymsOfBuild(found, shipped.all);
  const problems = [...dsymProblems(entries.map((entry) => basename(entry.folder))), ...ipaProblems(shipped.appUuids, new Set(entries.flatMap((entry) => entry.uuids)))];
  if (problems.length) throw new Error(problems.join("; "));
  mkdirSync(out, { recursive: true });
  const seen = new Set();
  for (const { folder, uuids } of entries) {
    for (const uuid of uuids) {
      if (!shipped.all.has(uuid)) continue;
      if (seen.has(uuid)) continue;
      seen.add(uuid);
      const zip = join(out, dsymAssetName(basename(folder), version, uuid));
      execFileSync("ditto", ["-c", "-k", "--keepParent", folder, zip]);
      console.log(`${basename(zip)}  (${basename(folder)})`);
    }
  }
}

// ---- Android natives --------------------------------------------------------------------------

/** { buildId, hasSymtab } of a 64-bit little-endian ELF file's bytes, or null when it is not one. Only the section headers are read. */
export function inspectElf(bytes) {
  if (bytes.length < 0x40 || bytes.readUInt32LE(0) !== ELF_MAGIC || bytes[4] !== 2 || bytes[5] !== 1) return null;
  const sectionOffset = Number(bytes.readBigUInt64LE(0x28));
  const entrySize = bytes.readUInt16LE(0x3a);
  const count = bytes.readUInt16LE(0x3c);
  const nameTable = bytes.readUInt16LE(0x3e);
  if (!sectionOffset || sectionOffset + count * entrySize > bytes.length) return null;
  const sections = Array.from({ length: count }, (_, i) => {
    const at = sectionOffset + i * entrySize;
    return { name: bytes.readUInt32LE(at), type: bytes.readUInt32LE(at + 4), offset: Number(bytes.readBigUInt64LE(at + 0x18)), size: Number(bytes.readBigUInt64LE(at + 0x20)) };
  });
  const names = sections[nameTable];
  const nameOf = (section) => {
    const start = names.offset + section.name;
    return bytes.toString("latin1", start, bytes.indexOf(0, start));
  };
  const note = sections.find((section) => nameOf(section) === ".note.gnu.build-id");
  return { buildId: note ? buildIdOf(bytes, note) : null, hasSymtab: sections.some((section) => section.type === SHT_SYMTAB) };
}

// An ELF note: namesz, descsz, type (4 bytes each), the name padded to 4, then the descriptor (the build id).
function buildIdOf(bytes, note) {
  const nameSize = bytes.readUInt32LE(note.offset);
  const descSize = bytes.readUInt32LE(note.offset + 4);
  const start = note.offset + 12 + Math.ceil(nameSize / 4) * 4;
  return bytes.subarray(start, start + descSize).toString("hex");
}

export function nativeAssetName(name, version, buildId, file) {
  return `${name}-${version}-android-arm64-${buildId.slice(0, 16)}-${file}`;
}

/** The sherpa-onnx version the build uses, read from SherpaOnnxVersion.props (the one place it is written; this only reads it). */
export function sherpaVersionOf(propsText) {
  const version = /<SherpaOnnxVersion>([^<]+)<\/SherpaOnnxVersion>/.exec(propsText)?.[1];
  if (!version) throw new Error("SherpaOnnxVersion not found in the props file");
  return version.trim();
}

function androidNativeFiles(packages, sherpaVersion) {
  const dir = join(packages, ANDROID_PACKAGE, sherpaVersion, "runtimes", "android-arm64", "native");
  if (!existsSync(dir)) throw new Error(`${dir} not found: the Android build must have restored ${ANDROID_PACKAGE} ${sherpaVersion} first`);
  const missing = ANDROID_NATIVE_FILES.filter((file) => !existsSync(join(dir, file)));
  if (missing.length) throw new Error(`${missing.join(", ")} not found in ${dir}`);
  return ANDROID_NATIVE_FILES.map((file) => join(dir, file));
}

// The id in the file name: the ELF build id, or (a library built without one) the start of the file's SHA-256, so two builds of a library never share an asset name.
function identityOf(bytes, info) {
  return info.buildId ?? createHash("sha256").update(bytes).digest("hex");
}

function collectAndroidNatives({ version, name, out, packages, props }) {
  const files = androidNativeFiles(packages, sherpaVersionOf(readFileSync(props, "utf8")));
  mkdirSync(out, { recursive: true });
  for (const file of files) {
    const bytes = readFileSync(file);
    const info = inspectElf(bytes);
    if (!info) throw new Error(`${file} is not a 64-bit ELF file`);
    const asset = nativeAssetName(name, version, identityOf(bytes, info), basename(file));
    copyFileSync(file, join(out, asset));
    console.log(`${asset}  build id: ${info.buildId ?? "none (file hash used)"}  symtab: ${info.hasSymtab ? "yes" : "no (stripped; only exported symbols)"}`);
  }
}

/** The command and its `--flag value` pairs; a flag without a value, a value that is itself a flag, or a word where a flag belongs throws (an empty value would shift the later flags). */
export function parseArgs(argv) {
  const args = { command: argv[0] };
  for (let i = 1; i < argv.length; i += 2) {
    const flag = argv[i];
    const value = argv[i + 1];
    if (!flag.startsWith("--") || flag.length === 2) throw new Error(`Expected a --flag, got "${flag}"`);
    if (value === undefined || value === "" || value.startsWith("--")) throw new Error(`${flag} needs a value, got ${value === undefined ? "nothing" : `"${value}"`}`);
    args[flag.slice(2)] = value;
  }
  return args;
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.version || !args.out) throw new Error("--version and --out are required");
  if (args.command === "ios-dsyms" && args.root && args.ipa) return collectIosDsyms(args);
  if (args.command === "android-natives" && args.name) return collectAndroidNatives({ ...args, packages: args.packages ?? process.env.NUGET_PACKAGES ?? join(homedir(), ".nuget", "packages"), props: args.props ?? "src/Shared/SherpaOnnxVersion.props" });
  throw new Error("Usage: collect-symbols.mjs ios-dsyms --root DIR --ipa FILE --version V --out DIR | android-natives --name N --version V --out DIR");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main();
  } catch (error) {
    console.error(`::error::${error.message}`);
    process.exit(1);
  }
}
