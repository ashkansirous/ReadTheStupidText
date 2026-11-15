// Dino Reader's paid books (Slice 346): which store product unlocks which books. A paid book has one product of its own
// (`uk.sirous.dinoreader.book.<stem, hyphens as _>`, written into its meta.json as `productId` by publish-store-books.mjs), which unlocks a
// list of one. A bundle is one more row in BUNDLES below (product id to the book ids it unlocks), with no app update: the list
// route (`/api/stories`) tells the app, per book, every product that unlocks it. Keep this file free of imports and of enums so
// worker/entitlements.test.mjs can run it under node's type stripping.

/** The Android package name and iOS/Windows app id the store checks are made for. */
export const APP_PACKAGE_ID = "uk.sirous.dinoreader";

export const BOOK_PRODUCT_PREFIX = `${APP_PACKAGE_ID}.book.`;

/**
 * The subscription products (Slice 384): every one unlocks every paid book (also books added later) while the store says the subscription runs.
 * Play has one subscription (`SUBSCRIPTION_PRODUCT`) with two base plans, App Store Connect two products in one group, Microsoft two add-ons.
 */
export const SUBSCRIPTION_PRODUCT = `${APP_PACKAGE_ID}.sub`;
export const SUBSCRIPTION_MONTHLY_PRODUCT = `${SUBSCRIPTION_PRODUCT}.monthly`;
export const SUBSCRIPTION_YEARLY_PRODUCT = `${SUBSCRIPTION_PRODUCT}.yearly`;
export const SUBSCRIPTION_PRODUCTS: readonly string[] = [SUBSCRIPTION_PRODUCT, SUBSCRIPTION_MONTHLY_PRODUCT, SUBSCRIPTION_YEARLY_PRODUCT];

/** True for a subscription product id: its proof is checked for an end date, not only for ownership. */
export function isSubscriptionProduct(productId: string): boolean {
  return SUBSCRIPTION_PRODUCTS.includes(productId);
}

/** Bundle products (none yet): product id to the story ids it unlocks. Add a row here when a bundle exists in the stores. */
export const BUNDLES: Readonly<Record<string, readonly string[]>> = {};

const PRODUCT_ID_PATTERN = /^[A-Za-z0-9][A-Za-z0-9._-]{2,99}$/;

/** True for a product id the stores could carry; anything else is never sent to a store. */
export function isProductId(value: string): boolean {
  return PRODUCT_ID_PATTERN.test(value);
}

/** Every product that unlocks the paid book: its own product (when it has one), each bundle that lists it, then the subscription products (Slice 384; last, so a bought book's own product is the proof the app sends first). */
export function productsUnlocking(bookId: string, bookProductId: string | null, bundles: Readonly<Record<string, readonly string[]>> = BUNDLES): string[] {
  const own = bookProductId === null ? [] : [bookProductId];
  const bundled = Object.entries(bundles).filter(([, books]) => books.includes(bookId)).map(([productId]) => productId);
  return [...own, ...bundled, ...SUBSCRIPTION_PRODUCTS];
}

/** Whether any owned product unlocks the book. */
export function unlocksBook(owned: readonly string[], bookId: string, bookProductId: string | null, bundles: Readonly<Record<string, readonly string[]>> = BUNDLES): boolean {
  const unlocking = productsUnlocking(bookId, bookProductId, bundles);
  return owned.some((productId) => unlocking.includes(productId));
}
