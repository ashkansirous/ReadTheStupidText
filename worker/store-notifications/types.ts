// Slice 458: what a store's server notification becomes. One `StoreCount` is one `store` row in `dino_usage` (or `dino_usage_beta` when `test`).
// No account id, transaction id or purchase token is kept: the fields below are all closed lists, a country and a book id from our own catalogue.
// Free of enums and parameter properties so node's type stripping can run it in tests.

export const NO_PLAN = "none";

export interface StoreCount {
  /** `android` or `ios` (STORE_PLATFORMS in events.ts). */
  readonly platform: string;
  /** One of STORE_SUBSCRIPTION_KINDS or STORE_BOOK_KINDS. */
  readonly kind: string;
  /** `trial`, `paid` or `code`; a book is always `paid`. */
  readonly offer: string;
  /** The row's source: `shelf` for a book, `sub_monthly` / `sub_yearly` for a plan, `none` when the plan is unknown. */
  readonly source: string;
  /** The book's 32-hex id for a book, else `none`. */
  readonly book: string;
  /** Two capital letters, or `none`. */
  readonly country: string;
  /** Apple sandbox, Play test purchase or license tester: goes to `dino_usage_beta`. */
  readonly test: boolean;
}

/** What one delivery decided: a row to write, a delivery to drop quietly (a test ping, a kind we do not count), or a bad one to refuse. */
export type Outcome =
  | { readonly type: "count"; readonly dedupKey: string; readonly count: StoreCount }
  | { readonly type: "ignore" }
  | { readonly type: "refuse" }
  | { readonly type: "retry" };

export const IGNORE: Outcome = { type: "ignore" };
export const REFUSE: Outcome = { type: "refuse" };
/** The store could not be asked (an outage): answer 5xx so Pub/Sub redelivers. */
export const RETRY: Outcome = { type: "retry" };

/** Maps a store product id (Apple) or sku (Play) of a book to the book's id from its meta.json `productId`, or null when none matches. */
export type BookResolver = (productId: string) => Promise<string | null>;
