#!/usr/bin/env node
// Slice 243 item 2: turn an iOS crash into method, file and line, on this Windows PC (no Mac).
//
//   node tools/crashes/symbolicate.mjs <crash file> [--app dino|rtst] [--version x.y.z] [--dsym-dir DIR] [--repo owner/repo] [--cache DIR]
//
// <crash file> is an Apple crash log (.crash text or .ips), an App Store Connect crash-log answer saved by pull-apple.py (`*_tf_crashlog_*.json`), or one of our own
// R2 reports of kind `native` (saved by pull-crashes.mjs --out). For each frame of our images (the app and SherpaOnnxC) it fetches the dSYM of that exact build, found by
// the image UUID, from the product's GitHub Release (`dino-v<version>` / `rtst-v<version>`, the asset `<Image>-<version>-<UUID>.dSYM.zip` the iOS release job attaches),
// and asks llvm-symbolizer for the source position. Builds before Slice 243 have no dSYM; their frames stay as raw addresses.
//
// Needs: node 22+, `gh` logged in, and llvm-symbolizer + llvm-objdump (LLVM: `winget install LLVM.LLVM`; this tool installs nothing). --dsym-dir uses already unpacked
// `<Image>.dSYM` folders (or zips) in that folder instead of the release.
// Limit: a MetricKit report carries only the app's own binaryUuid, so its SherpaOnnxC frames stay unnamed ("?") and are not symbolicated (an Apple .crash/.ips log lists every image and works).
// link-time address = runtime address - image load address + the image's __TEXT vmaddr (read with llvm-objdump --macho --private-headers); a MetricKit or .ips frame
// already carries the offset from the start of __TEXT, so it is vmaddr + offset.

import { execFile } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { promisify } from "node:util";

import { readCrashInput } from "./crash-frames.mjs";

const run = promisify(execFile);
const OUR_IMAGE = /^(DinoReader|ReadTheStupidText|SherpaOnnxC)/i;
const LLVM_HINT = "llvm-symbolizer and llvm-objdump are needed: install LLVM with `winget install LLVM.LLVM` (and open a new terminal), or one Android NDK with `sdkmanager --install \"ndk;<version>\"` (needs JAVA_HOME; no elevation), then run this again.";

/** True for an image of ours (the app or the sherpa framework); system libraries are never looked up. */
export function isOurImage(name) {
  return OUR_IMAGE.test(String(name ?? ""));
}

/** "dino" or "rtst" from the first of our images in `frames`, or null. */
export function appOfFrames(frames) {
  const ours = frames.find((frame) => /^(DinoReader|ReadTheStupidText)/i.test(frame.image));
  if (!ours) return null;
  return /^Dino/i.test(ours.image) ? "dino" : "rtst";
}

export function releaseTag(app, version) {
  return `${app}-v${version}`;
}

/** The release-asset pattern of the dSYM zip of image `uuid`. */
export function assetPattern(uuid) {
  return `*-${uuid}.dSYM.zip`;
}

/** The link-time address of `frame` in an image whose __TEXT segment starts at `textVmaddr`. */
export function linkAddress(frame, textVmaddr) {
  if (typeof frame.offset === "number") return textVmaddr + frame.offset;
  return frame.address - frame.loadAddress + textVmaddr;
}

/** The vmaddr of the __TEXT segment in `llvm-objdump --macho --private-headers` output, or null. */
export function parseTextVmaddr(output) {
  const match = /segname __TEXT\s*\r?\n\s*vmaddr (0x[0-9a-f]+)/i.exec(output);
  return match ? Number.parseInt(match[1], 16) : null;
}

/** The lines llvm-symbolizer answered with (`-p -f -i`: one per inlined frame); nothing for an unknown address (`??`). */
export function parseSymbolizerOutput(output) {
  return output
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith("??"));
}

export function symbolizerArgs(dwarfPath, address) {
  return [`--obj=${dwarfPath}`, "-p", "-f", "-i", `0x${address.toString(16)}`];
}

/**
 * Resolves every frame of our images. `deps` is { dwarfFor(uuid, image) -> path|null, textVmaddr(dwarfPath) -> number, symbolize(dwarfPath, address) -> string[] }, so tests
 * can stub the disk, the network and llvm. Returns one entry per frame of ours: { frame, status: "resolved"|"no-dsym"|"unknown-address", lines }.
 */
export async function symbolicateFrames(frames, deps) {
  const results = [];
  const dwarfs = new Map();
  for (const frame of frames.filter((item) => isOurImage(item.image))) {
    if (!dwarfs.has(frame.uuid)) dwarfs.set(frame.uuid, await deps.dwarfFor(frame.uuid, frame.image));
    const dwarf = dwarfs.get(frame.uuid);
    if (!dwarf) {
      results.push({ frame, status: "no-dsym", lines: [] });
      continue;
    }
    const lines = await deps.symbolize(dwarf, linkAddress(frame, await deps.textVmaddr(dwarf)));
    results.push({ frame, status: lines.length ? "resolved" : "unknown-address", lines });
  }
  return results;
}

/** One printable block for the results: frames grouped in order, with what could not be resolved said plainly. */
export function formatResults(results, version) {
  if (!results.length) return "No frames of our images found in this crash (nothing to symbolicate).";
  const out = [];
  for (const { frame, status, lines } of results) {
    const where = `${frame.image} ${frame.uuid}${frame.thread !== undefined && frame.thread !== null ? ` thread ${frame.thread}` : ""}`;
    if (status === "resolved") out.push(...lines.map((line) => `${line}    [${where}]`));
    else if (status === "no-dsym") out.push(`(no dSYM for ${where}${version ? `; builds before Slice 243 keep none` : ""})`);
    else out.push(`(address not in the dSYM of ${where})`);
  }
  return out.join("\n");
}

// ---- the real dependencies --------------------------------------------------------------------

function findDwarf(dsymFolder) {
  const dir = join(dsymFolder, "Contents", "Resources", "DWARF");
  const file = existsSync(dir) ? readdirSync(dir)[0] : undefined;
  return file ? join(dir, file) : null;
}

/** The UUID (32 upper-case hex digits) of a thin 64-bit little-endian Mach-O, or of the first slice of a fat one; null when `bytes` is neither. */
export function machoUuid(bytes) {
  let base = 0;
  if (bytes.length >= 20 && bytes.readUInt32BE(0) === 0xcafebabe) base = bytes.readUInt32BE(16);
  if (bytes.length < base + 32 || bytes.readUInt32LE(base) !== 0xfeedfacf) return null;
  let at = base + 32;
  for (let i = 0, count = bytes.readUInt32LE(base + 16); i < count && at + 8 <= bytes.length; i++) {
    const size = bytes.readUInt32LE(at + 4);
    if (size < 8) return null;
    if (bytes.readUInt32LE(at) === 0x1b && at + 24 <= bytes.length) return bytes.subarray(at + 8, at + 24).toString("hex").toUpperCase();
    at += size;
  }
  return null;
}

/** The `.dSYM` folders and `.dSYM.zip` files under `root` (recursive; a dSYM's own contents are not searched). */
function listDsymEntries(root) {
  const found = [];
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const path = join(root, entry.name);
    if (entry.isDirectory() && entry.name.endsWith(".dSYM")) found.push({ path, zip: false });
    else if (entry.isDirectory()) found.push(...listDsymEntries(path));
    else if (entry.name.endsWith(".dSYM.zip")) found.push({ path, zip: true });
  }
  return found;
}

// An unpacked dSYM is the image's when its folder name carries the UUID or, for a folder named after the app (`DinoReader.App.app.dSYM`), when its DWARF file's own UUID is it.
function folderHasUuid(folder, uuid) {
  if (basename(folder).toUpperCase().includes(uuid)) return true;
  const dwarf = findDwarf(folder);
  return dwarf !== null && machoUuid(readFileSync(dwarf)) === uuid;
}

/** On Windows the System32 bsdtar; when that file is missing, plain `tar` with a notice (it may be GNU tar, which cannot read zip files). */
function zipTool() {
  if (process.platform !== "win32") return "tar";
  const system32Tar = join(process.env.SystemRoot ?? "C:\\Windows", "System32", "tar.exe");
  if (existsSync(system32Tar)) return system32Tar;
  console.error(`[symbolicate] ${system32Tar} is missing, trying plain tar (if it cannot read zip files, unpack the dSYM zip by hand and pass --dsym-dir)`);
  return "tar";
}

async function unpackZip(zip, folder) {
  mkdirSync(folder, { recursive: true });
  // resolve: a relative --cache would otherwise be applied twice (the cwd is the folder). On Windows the System32 bsdtar is named, because the GNU tar of Git Bash reads
  // "D:\..." as a remote host and cannot open zip files.
  const tar = zipTool();
  await run(tar, ["-xf", resolve(zip)], { cwd: folder }); // bsdtar (Windows 10+) reads zip files
  return readdirSync(folder).find((name) => name.endsWith(".dSYM"));
}

/** The unpacked dSYM folder of image `uuid` under `root` (folders as they are, release-asset zips unpacked into `cache`), or null. */
export async function findLocalDsym(root, uuid, cache) {
  const entries = listDsymEntries(root);
  const folder = entries.find((entry) => !entry.zip && folderHasUuid(entry.path, uuid));
  if (folder) return folder.path;
  const zip = entries.find((entry) => entry.zip && basename(entry.path).toUpperCase().includes(uuid));
  if (!zip) return null;
  const target = join(cache, uuid);
  const unpacked = await unpackZip(zip.path, target);
  return unpacked ? join(target, unpacked) : null;
}

async function downloadDsym(args, tag, uuid, cache) {
  const folder = join(cache, uuid);
  mkdirSync(folder, { recursive: true });
  const unpacked = () => readdirSync(folder).find((name) => name.endsWith(".dSYM"));
  if (!unpacked()) {
    const repo = args.repo ? ["--repo", args.repo] : [];
    try {
      await run("gh", ["release", "download", tag, "--pattern", assetPattern(uuid), "--dir", folder, "--clobber", ...repo]);
    } catch {
      return null; // no such release or asset: a build from before the dSYMs were kept
    }
    const zip = readdirSync(folder).find((name) => name.endsWith(".dSYM.zip"));
    if (!zip) return null;
    await unpackZip(join(folder, zip), folder);
  }
  return unpacked() ? join(folder, unpacked()) : null;
}

const NDK_LLVM_HOST = { win32: "windows-x86_64", darwin: "darwin-x86_64", linux: "linux-x86_64" };

/**
 * The `bin` folder of the newest Android NDK under `sdkRoot/ndk` (it ships llvm-symbolizer and llvm-objdump, and installs into the user's folder with no elevation), or null.
 * Newest = highest version by numeric parts.
 */
export function newestNdkLlvmBin(sdkRoot, platform = process.platform) {
  const host = NDK_LLVM_HOST[platform];
  const ndkDir = sdkRoot ? join(sdkRoot, "ndk") : null;
  if (!host || !ndkDir || !existsSync(ndkDir)) return null;
  const byVersion = (a, b) => a.localeCompare(b, undefined, { numeric: true });
  const bins = readdirSync(ndkDir).sort(byVersion).reverse().map((name) => join(ndkDir, name, "toolchains", "llvm", "prebuilt", host, "bin"));
  return bins.find((bin) => existsSync(bin)) ?? null;
}

/** The SDK root from ANDROID_HOME / ANDROID_SDK_ROOT, else the Windows default under %LOCALAPPDATA%. */
function androidSdkRoot() {
  return process.env.ANDROID_HOME ?? process.env.ANDROID_SDK_ROOT ?? (process.env.LOCALAPPDATA ? join(process.env.LOCALAPPDATA, "Android", "Sdk") : null);
}

let ndkFallback; // undefined until the first llvm call needs it; then the bin folder or null

/** The NDK llvm folder, looked up once per run, with one log line when found. */
function ndkFallbackBin() {
  if (ndkFallback === undefined) {
    ndkFallback = newestNdkLlvmBin(androidSdkRoot());
    if (ndkFallback) console.error(`[symbolicate] llvm tools not on PATH, using the Android NDK copy in ${ndkFallback}`);
  }
  return ndkFallback;
}

/** `tool` itself when it is on PATH, else the same tool from the newest NDK (Debug line says which one ran). */
async function runLlvm(tool, args) {
  const options = { maxBuffer: 64 * 1024 * 1024 };
  try {
    return await run(tool, args, options);
  } catch (error) {
    const bin = error.code === "ENOENT" ? ndkFallbackBin() : null;
    if (!bin) throw error;
    return run(join(bin, tool), args, options);
  }
}

async function llvm(tool, args) {
  try {
    return (await runLlvm(tool, args)).stdout;
  } catch (error) {
    if (error.code === "ENOENT") throw new Error(`${tool} was not found. ${LLVM_HINT}`);
    throw new Error(`${tool} failed (${error.code ?? "no exit code"}): ${String(error.stderr ?? error.message).trim().slice(0, 500)}`); // a tool error is never "address not in the dSYM"
  }
}

function realDeps(args, version, app, cache) {
  const tag = releaseTag(app, version);
  return {
    async dwarfFor(uuid) {
      const folder = args["dsym-dir"] ? await findLocalDsym(args["dsym-dir"], uuid, cache) : await downloadDsym(args, tag, uuid, cache);
      return folder ? findDwarf(folder) : null;
    },
    async textVmaddr(dwarfPath) {
      const found = parseTextVmaddr(await llvm("llvm-objdump", ["--macho", "--private-headers", dwarfPath]));
      if (found === null) throw new Error(`llvm-objdump gave no __TEXT segment for ${dwarfPath}. ${LLVM_HINT}`);
      return found;
    },
    async symbolize(dwarfPath, address) {
      return parseSymbolizerOutput(await llvm("llvm-symbolizer", symbolizerArgs(dwarfPath, address)));
    },
  };
}

function parseArgs(argv) {
  const args = { _: [] };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i].startsWith("--")) args[argv[i].slice(2)] = argv[++i];
    else args._.push(argv[i]);
  }
  return args;
}

/** Symbolicates the crash in `text`; `args` carries the optional --app, --version, --repo, --dsym-dir, --cache. Returns the printable result. */
export async function symbolicateText(text, args = {}, depsFor = realDeps) {
  const input = readCrashInput(text);
  const app = args.app ?? input.app ?? appOfFrames(input.frames);
  const version = args.version ?? input.version?.version;
  if (!app) throw new Error("Could not tell which app this crash is from: pass --app dino|rtst.");
  if (!version && !args["dsym-dir"]) throw new Error("Could not read the app version from this crash: pass --version x.y.z (or --dsym-dir).");
  const cache = args.cache ?? join(tmpdir(), "rtst-dsyms");
  const results = await symbolicateFrames(input.frames, depsFor(args, version, app, cache));
  return formatResults(results, version);
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const file = args._[0];
  if (!file) throw new Error("Usage: node tools/crashes/symbolicate.mjs <crash file> [--app dino|rtst] [--version x.y.z] [--dsym-dir DIR]");
  console.log(await symbolicateText(readFileSync(file, "utf8"), args));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exit(1);
  });
}
