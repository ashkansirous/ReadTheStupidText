// Slice 458: App Store Server Notifications v2 (POST /api/store/apple, body `{"signedPayload": "<JWS>"}`).
// The payload is believed only when apple-jws.ts verifies its chain; the transaction inside it (`signedTransactionInfo`) is verified the same way.
// Apple's types and subtypes (https://developer.apple.com/documentation/appstoreservernotifications/notificationtype, .../subtype) map to a closed list of kinds.
// Nothing about the buyer is kept: not the transaction id, original transaction id or app account token. The notification UUID is only the short-lived dedup key.
import { verifyAppleSignedPayload } from "../entitlements/apple-jws.ts";
import type { AppleJwsOptions } from "../entitlements/apple-jws.ts";
import { APP_PACKAGE_ID, isSubscriptionProduct, SUBSCRIPTION_MONTHLY_PRODUCT, SUBSCRIPTION_YEARLY_PRODUCT } from "../products.ts";
import { PLAN_MONTHLY_SOURCE, PLAN_YEARLY_SOURCE } from "../events.ts";
import { countryOfStorefront } from "./countries.ts";
import { IGNORE, NO_PLAN, REFUSE } from "./types.ts";
import type { BookResolver, Outcome, StoreCount } from "./types.ts";

const PLATFORM = "ios";
const SANDBOX = "Sandbox";
const OFFER_INTRO = 1;
const OFFER_CODE = 3;
const FREE_TRIAL = "FREE_TRIAL";
const NO_BOOK = "none";
const TRIAL_GAP_SHARE = 0.9;

/** Apple's notification types this slice counts, and the subtype that changes the kind. A type that is not here (CONSUMPTION_REQUEST, PRICE_INCREASE, RENEWAL_EXTENDED...) is dropped. */
export const APPLE_TYPES = ["SUBSCRIBED", "DID_RENEW", "DID_CHANGE_RENEWAL_STATUS", "EXPIRED", "DID_FAIL_TO_RENEW", "GRACE_PERIOD_EXPIRED", "REFUND", "REVOKE", "OFFER_REDEEMED", "ONE_TIME_CHARGE"] as const;

interface Transaction {
  readonly productId: string;
  readonly offerType: number | null;
  readonly offerDiscountType: string;
  readonly purchaseDate: number;
  readonly originalPurchaseDate: number;
  readonly expiresDate: number;
  readonly storefront: string;
  readonly sandbox: boolean;
}

const asNumber = (value: unknown): number => (typeof value === "number" && Number.isFinite(value) ? value : 0);
const asText = (value: unknown): string => (typeof value === "string" ? value : "");

function transactionOf(payload: Record<string, unknown>, environment: string): Transaction | null {
  const productId = asText(payload["productId"]);
  if (productId === "" || asText(payload["bundleId"]) !== APP_PACKAGE_ID) return null;
  const offerType = payload["offerType"];
  return {
    productId,
    offerType: typeof offerType === "number" ? offerType : null,
    offerDiscountType: asText(payload["offerDiscountType"]),
    purchaseDate: asNumber(payload["purchaseDate"]),
    originalPurchaseDate: asNumber(payload["originalPurchaseDate"]),
    expiresDate: asNumber(payload["expiresDate"]),
    storefront: asText(payload["storefront"]),
    sandbox: environment === SANDBOX || asText(payload["environment"]) === SANDBOX,
  };
}

/** Which plan a subscription product is: the monthly or yearly one by its product id, else `none`. */
export function planOf(productId: string): string {
  if (productId === SUBSCRIPTION_MONTHLY_PRODUCT) return PLAN_MONTHLY_SOURCE;
  return productId === SUBSCRIPTION_YEARLY_PRODUCT ? PLAN_YEARLY_SOURCE : NO_PLAN;
}

/**
 * A renewal is a trial turning paid when the period just left was shorter than the one just bought: the first period (original purchase to this purchase) was the trial.
 * Apple sends no flag for it, so this is read from the dates; a plain renewal's gap equals its paid period.
 */
export function isTrialConversion(transaction: Pick<Transaction, "purchaseDate" | "originalPurchaseDate" | "expiresDate">): boolean {
  const paidPeriod = transaction.expiresDate - transaction.purchaseDate;
  const firstPeriod = transaction.purchaseDate - transaction.originalPurchaseDate;
  return paidPeriod > 0 && firstPeriod > 0 && firstPeriod < paidPeriod * TRIAL_GAP_SHARE;
}

function isTrial(transaction: Transaction): boolean {
  return transaction.offerType === OFFER_INTRO && transaction.offerDiscountType === FREE_TRIAL;
}

function startOffer(transaction: Transaction): { kind: string; offer: string } {
  if (isTrial(transaction)) return { kind: "trial_started", offer: "trial" };
  return { kind: "subscribed", offer: transaction.offerType === OFFER_CODE ? "code" : "paid" };
}

function failToRenew(subtype: string): string {
  return subtype === "GRACE_PERIOD" ? "grace" : "billing_retry";
}

/** The kind (and offer) of one subscription notification, or null for one we do not count. */
export function subscriptionKindOf(type: string, subtype: string, transaction: Transaction): { kind: string; offer: string } | null {
  switch (type) {
    case "SUBSCRIBED": return startOffer(transaction);
    case "DID_RENEW": return { kind: isTrialConversion(transaction) ? "converted" : "renewed", offer: "paid" };
    case "DID_CHANGE_RENEWAL_STATUS": return subtype === "AUTO_RENEW_DISABLED" ? { kind: "cancelled", offer: isTrial(transaction) ? "trial" : "paid" } : null;
    case "EXPIRED":
    case "GRACE_PERIOD_EXPIRED": return { kind: "expired", offer: "paid" };
    case "DID_FAIL_TO_RENEW": return { kind: failToRenew(subtype), offer: "paid" };
    case "REFUND":
    case "REVOKE": return { kind: "refunded", offer: "paid" };
    case "OFFER_REDEEMED": return { kind: "code_redeemed", offer: "code" };
    default: return null;
  }
}

async function bookCount(type: string, transaction: Transaction, books: BookResolver): Promise<Pick<StoreCount, "kind" | "offer" | "source" | "book"> | null> {
  const kind = type === "ONE_TIME_CHARGE" ? "book_bought" : type === "REFUND" || type === "REVOKE" ? "book_refunded" : null;
  const book = kind === null ? null : await books(transaction.productId);
  return kind === null || book === null ? null : { kind, offer: "paid", source: "shelf", book };
}

async function countOf(type: string, subtype: string, transaction: Transaction, books: BookResolver): Promise<Pick<StoreCount, "kind" | "offer" | "source" | "book"> | null> {
  if (!isSubscriptionProduct(transaction.productId)) return bookCount(type, transaction, books);
  const decided = subscriptionKindOf(type, subtype, transaction);
  return decided === null ? null : { ...decided, source: planOf(transaction.productId), book: NO_BOOK };
}

async function verifiedTransaction(data: Record<string, unknown>, options: AppleJwsOptions): Promise<Transaction | null> {
  const signed = data["signedTransactionInfo"];
  if (typeof signed !== "string") return null;
  const payload = await verifyAppleSignedPayload(signed, options);
  return payload === null ? null : transactionOf(payload, asText(data["environment"]));
}

/** Decides what one App Store notification is worth: a row, nothing (a type we do not count, a test ping, another app's), or a refusal (not Apple's). */
export async function decideApple(body: unknown, books: BookResolver, options: AppleJwsOptions = {}): Promise<Outcome> {
  const signed = typeof body === "object" && body !== null ? (body as Record<string, unknown>)["signedPayload"] : null;
  if (typeof signed !== "string") return REFUSE;
  const payload = await verifyAppleSignedPayload(signed, options);
  const data = payload === null ? null : payload["data"];
  if (payload === null) return REFUSE;
  const type = asText(payload["notificationType"]);
  if (typeof data !== "object" || data === null || !(APPLE_TYPES as readonly string[]).includes(type)) return IGNORE;
  const transaction = await verifiedTransaction(data as Record<string, unknown>, options);
  if (transaction === null) return IGNORE;
  const decided = await countOf(type, asText(payload["subtype"]), transaction, books);
  const id = asText(payload["notificationUUID"]);
  if (decided === null) return IGNORE;
  return { type: "count", dedupKey: `apple:${id}`, count: { platform: PLATFORM, ...decided, country: countryOfStorefront(transaction.storefront), test: transaction.sandbox } };
}
