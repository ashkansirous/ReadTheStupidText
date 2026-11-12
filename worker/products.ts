// Dino Reader's paid books (Slice 346): which store product unlocks which books. A paid book has one product of its own
// (`uk.sirous.dinoreader.book.<stem>`, written into its meta.json as `productId` by publish-store-books.mjs), which unlocks a
// list of one. A bundle is one more row in BUNDLES below (product id to the book ids it unlocks), with no app update: the list
// route (`/api/stories`) tells the app, per book, every product that unlocks it. Keep this file free of imports and of enums so
// worker/entitlements.test.mjs can run it under node's type stripping.

/** The Android package name and iOS/Windows app id the store checks are made for. */
export const APP_PACKAGE_ID = "uk.sirous.dinoreader";

export const BOOK_PRODUCT_PREFIX = `${APP_PACKAGE_ID}.book.`;

/** Bundle products (none yet): product id to the story ids it unlocks. Add a row here when a bundle exists in the stores. */
export const BUNDLES: Readonly<Record<string, readonly string[]>> = {};

const PRODUCT_ID_PATTERN = /^[A-Za-z0-9][A-Za-z0-9._-]{2,99}$/;

/** True for a product id the stores could carry; anything else is never sent to a store. */
export function isProductId(value: string): boolean {
  return PRODUCT_ID_PATTERN.test(value);
}

/** Every product that unlocks the book: its own product (when it has one) plus each bundle that lists it. */
export function productsUnlocking(bookId: string, bookProductId: string | null, bundles: Readonly<Record<string, readonly string[]>> = BUNDLES): string[] {
  const own = bookProductId === null ? [] : [bookProductId];
  const bundled = Object.entries(bundles).filter(([, books]) => books.includes(bookId)).map(([productId]) => productId);
  return [...own, ...bundled];
}

/** Whether any owned product unlocks the book. */
export function unlocksBook(owned: readonly string[], bookId: string, bookProductId: string | null, bundles: Readonly<Record<string, readonly string[]>> = BUNDLES): boolean {
  const unlocking = productsUnlocking(bookId, bookProductId, bundles);
  return owned.some((productId) => unlocking.includes(productId));
}
