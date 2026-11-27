// Slice 283: the few SOAP pieces the Sonos music service (SMAPI) needs, written by hand: read the action and the request's
// parameters, write the envelopes and the faults. Shapes: docs/plan/slices/283-dino-sonos-music-service.md (sources section).
// Nothing here logs: ids and titles never leave the request (worker/CLAUDE.md).

export const SONOS_NAMESPACE = "http://www.sonos.com/Services/1.1";
const SOAP_ENVELOPE_NAMESPACE = "http://schemas.xmlsoap.org/soap/envelope/";
const XML_CONTENT_TYPE = "text/xml; charset=utf-8";
const XML_DECLARATION = '<?xml version="1.0" encoding="utf-8"?>';
const FAULT_STATUS = 500;
const OK_STATUS = 200;
const MAX_REQUEST_BYTES = 16_384;
const CONTENT_LENGTH_HEADER = "Content-Length";
const TRUE_TEXT = "true";
const WHOLE_NUMBER = /^\d{1,9}$/;
// Code points XML 1.0 forbids (C0 controls but tab, LF and CR; U+FFFE and U+FFFF; lone surrogates).
const XML_ILLEGAL = /[\u0000-\u0008\u000B\u000C\u000E-\u001F￾￿]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/g;

/** The request parameters' element names. */
const Param = { Id: "id", Index: "index", Count: "count", Recursive: "recursive", HouseholdId: "householdId", LinkCode: "linkCode", CallbackPath: "callbackPath", Token: "token", Key: "key", Term: "term" } as const;
const LOGIN_TOKEN = "loginToken";

/** The faultstring texts the service gives (no ids or titles in any of them). */
export const FaultMessage = {
  NoSuchItem: "No such item",
  NotSoap: "Not a SOAP call",
  TooLarge: "Request too large",
  Unsupported: "Unsupported call",
  Unavailable: "Service unavailable",
  NoCatalogue: "The catalogue is not available",
  NotPlayable: "This story is in the Dino app",
  NotLinked: "Link Code not found retry...",
  NotLoggedIn: "Link this Sonos home through the Dino app",
  BadLink: "The link request is not valid",
} as const;

/** The faultcode values the service raises (https://docs.sonos.com/docs/error-handling; the two NOT_LINKED codes: https://docs.sonos.com/docs/getdeviceauthtoken, Slice 438 B1). */
export const FaultCode = {
  ItemNotFound: "Client.ItemNotFound",
  Unavailable: "Server.ServiceUnavailable",
  Unknown: "Server.ServiceUnknownError",
  NotLinkedRetry: "Client.NOT_LINKED_RETRY",
  NotLinkedFailure: "Client.NOT_LINKED_FAILURE",
  AuthTokenExpired: "Client.AuthTokenExpired",
} as const;
export type FaultCodeName = (typeof FaultCode)[keyof typeof FaultCode];

/** The calls this service answers. */
export const SonosAction = {
  GetMetadata: "getMetadata",
  GetExtendedMetadata: "getExtendedMetadata",
  GetMediaURI: "getMediaURI",
  GetMediaMetadata: "getMediaMetadata",
  GetLastUpdate: "getLastUpdate",
  Search: "search",
  GetAppLink: "getAppLink",
  GetDeviceAuthToken: "getDeviceAuthToken",
  RefreshAuthToken: "refreshAuthToken",
} as const;
export type SonosActionName = (typeof SonosAction)[keyof typeof SonosAction];

/** What Sonos puts in the SOAP header after linking: `credentials/loginToken` (https://docs.sonos.com/docs/use-authentication-tokens). */
export interface LoginToken {
  readonly token: string;
  readonly key: string;
  readonly householdId: string;
}

export interface SonosRequest {
  readonly action: string;
  readonly id: string | null;
  readonly index: number | null;
  readonly count: number | null;
  readonly recursive: boolean;
  /** Slice 440: the `search` call's term (null when the Body has none). */
  readonly term: string | null;
  /** Slice 438: the Body's `householdId` (getAppLink, getDeviceAuthToken), `linkCode` and `callbackPath`. */
  readonly householdId: string | null;
  readonly linkCode: string | null;
  readonly callbackPath: string | null;
  /** Slice 438: the header's loginToken, null when the header has none or a part of it is missing. */
  readonly login: LoginToken | null;
}

export class SoapFault extends Error {
  readonly code: FaultCodeName;
  /** Raw XML for the fault's `<detail>` (the NOT_LINKED faults carry ExceptionInfo and SonosError), or null. */
  readonly detail: string | null;

  constructor(code: FaultCodeName, message: string, detail: string | null = null) {
    super(message);
    this.code = code;
    this.detail = detail;
  }
}

/** Escapes the five entities and drops the code points XML cannot carry (one of them makes Sonos reject the whole page). */
export function escapeXml(text: string): string {
  return text.replace(XML_ILLEGAL, "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&apos;");
}

export function unescapeXml(text: string): string {
  return text
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&");
}

interface BodyAction {
  readonly name: string;
  readonly inner: string;
}

// The first child element of Body, by local name (Sonos usually uses the `ns` prefix but does not promise it), with its content:
// the parameters are read from that content only, never from the Header or anywhere else in the envelope.
function bodyActionOf(xml: string): BodyAction | null {
  const open = /<(?:[\w.-]+:)?Body\b[^>]*>\s*<(?:[\w.-]+:)?([A-Za-z]\w*)\b[^>]*?(\/?)>/.exec(xml);
  if (open === null) return null;
  const name = open[1] ?? "";
  if (open[2] === "/") return { name, inner: "" };
  const rest = xml.slice(open.index + open[0].length);
  const close = new RegExp(`</(?:[\\w.-]+:)?${name}\\s*>`).exec(rest);
  return { name, inner: close === null ? rest : rest.slice(0, close.index) };
}

function elementText(xml: string, name: string): string | null {
  const found = new RegExp(`<(?:[\\w.-]+:)?${name}\\b[^>]*>([^<]*)</(?:[\\w.-]+:)?${name}>`).exec(xml);
  return found === null ? null : unescapeXml((found[1] ?? "").trim());
}

function wholeNumberParam(xml: string, name: string): number | null {
  const text = elementText(xml, name);
  return text !== null && WHOLE_NUMBER.test(text) ? Number(text) : null;
}

// Slice 438: the login token is read from the part of the envelope before the Body only, so a Body parameter can never pass for it.
function loginTokenOf(xml: string): LoginToken | null {
  const bodyAt = xml.search(/<(?:[\w.-]+:)?Body\b/);
  const header = bodyAt < 0 ? "" : xml.slice(0, bodyAt);
  const found = new RegExp(`<(?:[\\w.-]+:)?${LOGIN_TOKEN}\\b[^>]*>([\\s\\S]*?)</(?:[\\w.-]+:)?${LOGIN_TOKEN}\\s*>`).exec(header);
  if (found === null) return null;
  const inner = found[1] ?? "";
  const token = elementText(inner, Param.Token);
  const key = elementText(inner, Param.Key);
  const householdId = elementText(inner, Param.HouseholdId);
  return token === null || key === null || householdId === null || token === "" || householdId === "" ? null : { token, key, householdId };
}

/** Reads the action and parameters from the Body's action element only; the SOAPAction header is never used. Null when the Body has no action. */
export function parseSonosRequest(xml: string): SonosRequest | null {
  const body = bodyActionOf(xml);
  if (body === null) return null;
  const inner = body.inner;
  return {
    action: body.name,
    id: elementText(inner, Param.Id),
    index: wholeNumberParam(inner, Param.Index),
    count: wholeNumberParam(inner, Param.Count),
    recursive: elementText(inner, Param.Recursive) === TRUE_TEXT,
    term: elementText(inner, Param.Term),
    householdId: elementText(inner, Param.HouseholdId),
    linkCode: elementText(inner, Param.LinkCode),
    callbackPath: elementText(inner, Param.CallbackPath),
    login: loginTokenOf(xml),
  };
}

const ACCEPT_LANGUAGE_PART = /^\s*([A-Za-z]{1,8})(?:-[A-Za-z0-9-]+)?\s*(?:;\s*q\s*=\s*([0-9.]+))?\s*$/;
/** Browser language codes that are one of the app's languages under another code (Norwegian, "no" and "nn", is `nb`). */
const LANGUAGE_ALIASES: Readonly<Record<string, string>> = { no: "nb", nn: "nb" };

/**
 * Slice 437: the primary languages of an Accept-Language header, best first (`fa-IR, en-GB;q=0.8` is `["fa", "en"]`): region dropped,
 * q order kept (equal q keeps header order), q=0 and `*` left out, duplicates once. Null or junk gives an empty list.
 */
export function acceptedLanguages(header: string | null): string[] {
  const ranked: { code: string; q: number; at: number }[] = [];
  (header ?? "").split(",").forEach((part, at) => {
    const found = ACCEPT_LANGUAGE_PART.exec(part);
    if (found === null) return;
    const q = found[2] === undefined ? 1 : Number(found[2]);
    const lower = (found[1] ?? "").toLowerCase();
    if (Number.isFinite(q) && q > 0) ranked.push({ code: LANGUAGE_ALIASES[lower] ?? lower, q, at });
  });
  ranked.sort((a, b) => b.q - a.q || a.at - b.at);
  return [...new Set(ranked.map((entry) => entry.code))];
}

async function readCapped(request: Request): Promise<Uint8Array | null> {
  const reader = request.body?.getReader();
  if (reader === undefined) return new Uint8Array(0);
  const chunks: Uint8Array[] = [];
  let total = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    total += value.byteLength;
    if (total > MAX_REQUEST_BYTES) {
      await reader.cancel();
      return null;
    }
    chunks.push(value);
  }
  const whole = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    whole.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return whole;
}

/** The request body as text; null when it is over the cap in bytes (a declared Content-Length over it, or a stream that runs past it, is never read to the end). */
export async function readBoundedText(request: Request): Promise<string | null> {
  const declared = Number(request.headers.get(CONTENT_LENGTH_HEADER));
  if (Number.isFinite(declared) && declared > MAX_REQUEST_BYTES) return null;
  const bytes = await readCapped(request);
  return bytes === null ? null : new TextDecoder().decode(bytes);
}

function xmlResponse(status: number, body: string): Response {
  const envelope = `${XML_DECLARATION}<soap:Envelope xmlns:soap="${SOAP_ENVELOPE_NAMESPACE}"><soap:Body>${body}</soap:Body></soap:Envelope>`;
  return new Response(envelope, { status, headers: { "Content-Type": XML_CONTENT_TYPE, "Cache-Control": "no-store" } });
}

/** A success response: `<action>Response><action>Result>inner</...></...>`. */
export function resultResponse(action: SonosActionName, inner: string): Response {
  return xmlResponse(OK_STATUS, `<${action}Response xmlns="${SONOS_NAMESPACE}"><${action}Result>${inner}</${action}Result></${action}Response>`);
}

/** A SOAP fault: HTTP 500 for every failure, only the Fault in the Body (https://docs.sonos.com/docs/error-handling). */
export function faultResponse(fault: SoapFault): Response {
  const detail = fault.detail === null ? "" : `<detail>${fault.detail}</detail>`;
  return xmlResponse(FAULT_STATUS, `<soap:Fault><faultcode>${fault.code}</faultcode><faultstring>${escapeXml(fault.message)}</faultstring>${detail}</soap:Fault>`);
}

/** One element with escaped text. */
export function tag(name: string, value: string | number | boolean): string {
  return `<${name}>${escapeXml(String(value))}</${name}>`;
}
