// Slice 243 item 2: turns the three crash formats we get for an iOS crash into one list of frames that name an image by UUID.
//   * an Apple crash log, text (.crash) or .ips JSON (Xcode Organizer, a TestFlight crash submission's log);
//   * an App Store Connect crash-log answer (JSON with data.attributes.logText);
//   * one of our own R2 reports (pull-crashes.mjs) of kind `native`, whose stack is the MetricKit call-stack-tree JSON (cut at 16 KB, so it is scanned, not parsed).
// A frame is { image, uuid, offset?, address?, loadAddress?, thread?, line? }: `offset` is the distance from the start of the image's __TEXT segment, and the
// symbolicator adds that segment's link-time address. Pure; nothing here touches the disk or the network.

const HEX_UUID = /^[0-9a-f]{32}$/i;

/** The UUID as 32 upper-case hex digits with no dashes (the form used in release asset names), or null. */
export function normalizeUuid(text) {
  const bare = String(text ?? "").replace(/[<>\s-]/g, "");
  return HEX_UUID.test(bare) ? bare.toUpperCase() : null;
}

/** The kind of input in `text`: "ips", "asc-log", "report" or "crash" (the plain text log). */
export function detectFormat(text) {
  const trimmed = text.trimStart();
  if (trimmed.startsWith("{")) {
    if (/"usedImages"\s*:/.test(text)) return "ips";
    if (/"logText"\s*:/.test(text)) return "asc-log";
    if (/"schemaVersion"\s*:/.test(text) && /"stack"\s*:/.test(text)) return "report";
  }
  return "crash";
}

/** The text of the crash log inside an App Store Connect crashLog answer ({ data: { attributes: { logText } } }), or null. */
export function ascLogText(text) {
  try {
    return JSON.parse(text)?.data?.attributes?.logText ?? null;
  } catch {
    return null;
  }
}

// ---- Apple crash log, text form ---------------------------------------------------------------

const IMAGE_LINE = /^\s*(0x[0-9a-f]+)\s+-\s+(0x[0-9a-f]+)\s+(?:\+)?(\S+)\s+\S*\s*<([0-9a-f-]{32,36})>\s*(.*)$/i;
const FRAME_LINE = /^\s*(\d+)\s+(\S.*?)\s+(0x[0-9a-f]+)\s+(0x[0-9a-f]+)\s+\+\s+(\d+)\s*$/i;
const FRAME_NO_BASE = /^\s*(\d+)\s+(\S.*?)\s+(0x[0-9a-f]+)\s*$/i;

/** Images of a text crash log: [{ name, uuid, loadAddress, path }]. */
export function parseTextImages(text) {
  const images = [];
  for (const line of text.split(/\r?\n/)) {
    const match = IMAGE_LINE.exec(line);
    const uuid = match && normalizeUuid(match[4]);
    if (uuid) images.push({ name: match[3], uuid, loadAddress: Number.parseInt(match[1], 16), path: match[5].trim() });
  }
  return images;
}

/** Frames of a text crash log (every thread), each tied to the image it ran in. */
export function parseTextFrames(text) {
  const images = parseTextImages(text);
  const byName = new Map(images.map((image) => [image.name, image]));
  const frames = [];
  let thread = null;
  for (const line of text.split(/\r?\n/)) {
    const header = /^Thread (\d+)(?: name:.*)?(?: Crashed)?:/.exec(line);
    if (header) thread = Number(header[1]);
    const match = FRAME_LINE.exec(line) ?? FRAME_NO_BASE.exec(line);
    if (!match) continue;
    const image = byName.get(match[2].trim());
    if (!image) continue;
    const address = Number.parseInt(match[3], 16);
    frames.push({ image: image.name, uuid: image.uuid, loadAddress: image.loadAddress, address, thread, line: line.trim() });
  }
  return frames;
}

/** The app version and build of a text crash log ("Version:  1.90.0 (20261114.101)"), or null. */
export function textVersion(text) {
  const match = /^Version:\s+(\S+)\s*(?:\((\S+)\))?/m.exec(text);
  return match ? { version: match[1], build: match[2] ?? null } : null;
}

// ---- Apple crash log, .ips JSON ---------------------------------------------------------------

/** Frames of an .ips log (a JSON header line, then the JSON body, or the body alone). */
export function parseIps(text) {
  const body = parseIpsBody(text);
  if (!body) return { frames: [], version: null };
  const images = (body.usedImages ?? []).map((image) => ({ name: image.name ?? image.path ?? "?", uuid: normalizeUuid(image.uuid), loadAddress: image.base }));
  const frames = [];
  (body.threads ?? []).forEach((thread, index) => {
    for (const frame of thread.frames ?? []) {
      const image = images[frame.imageIndex];
      if (!image?.uuid || typeof frame.imageOffset !== "number") continue;
      frames.push({ image: image.name, uuid: image.uuid, offset: frame.imageOffset, thread: index });
    }
  });
  const info = body.bundleInfo;
  return { frames, version: info?.CFBundleShortVersionString ? { version: info.CFBundleShortVersionString, build: info.CFBundleVersion ?? null } : null };
}

function parseIpsBody(text) {
  const lines = text.trim().split(/\r?\n/);
  for (const candidate of [text, lines.slice(1).join("\n")]) {
    try {
      const parsed = JSON.parse(candidate);
      if (parsed?.usedImages) return parsed;
    } catch {
      // the first line of an .ips file is a separate JSON header; the next candidate is the body
    }
  }
  return null;
}

// ---- MetricKit call-stack tree inside one of our reports --------------------------------------

/**
 * Frames in a MetricKit call-stack-tree string. The app cuts the JSON at 16 KB, so it is read object by object: each `{` starts a chunk that holds one frame's own
 * keys (binaryUUID, offsetIntoBinaryTextSegment, binaryName) up to the next `{` or `}`. Frames repeat for every sample, so equal ones are listed once.
 */
export function parseMetricKitFrames(stack) {
  const frames = [];
  const seen = new Set();
  for (const chunk of stack.split("{")) {
    const uuid = normalizeUuid(/"binaryUUID"\s*:\s*"([^"]+)"/.exec(chunk)?.[1]);
    const offset = /"offsetIntoBinaryTextSegment"\s*:\s*(\d+)/.exec(chunk)?.[1];
    if (!uuid || offset === undefined) continue;
    const name = /"binaryName"\s*:\s*"([^"]*)"/.exec(chunk)?.[1] ?? "?";
    const key = `${uuid}:${offset}`;
    if (seen.has(key)) continue;
    seen.add(key);
    frames.push({ image: name, uuid, offset: Number(offset) });
  }
  return frames;
}

/**
 * Our MetricKit reports carry no `binaryName`, so every frame is "?" and the symbolicator would skip them all (found on the 2026-11-15 Dino 1.131.0 watchdog report).
 * Frames whose UUID is the report's own `binaryUuid` are the app image: name them after the app so they are looked up.
 */
export function nameAppFrames(frames, report) {
  const appUuid = normalizeUuid(report.binaryUuid);
  const appImage = report.app === "dino" ? "DinoReader" : report.app === "rtst" ? "ReadTheStupidText" : null;
  if (!appUuid || !appImage) return frames;
  return frames.map((frame) => (frame.image === "?" && frame.uuid === appUuid ? { ...frame, image: appImage } : frame));
}

/** Frames, app version and app name of one of our R2 reports (kind `native`); other kinds have no addresses. */
export function parseReport(text) {
  const report = JSON.parse(text);
  const frames = nameAppFrames(parseMetricKitFrames(String(report.stack ?? "")), report);
  return { frames, version: report.version ? { version: report.version, build: report.build ?? null } : null, app: report.app ?? null, binaryUuid: report.binaryUuid ?? null };
}

/** Everything the symbolicator needs from `text` of any supported input: { frames, version, app }. */
export function readCrashInput(text) {
  switch (detectFormat(text)) {
    case "ips":
      return { ...parseIps(text), app: null };
    case "asc-log": {
      const log = ascLogText(text) ?? "";
      return readCrashInput(log);
    }
    case "report":
      return parseReport(text);
    default:
      return { frames: parseTextFrames(text), version: textVersion(text), app: null };
  }
}
