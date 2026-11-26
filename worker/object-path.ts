// A safe reader for a value nested in parsed JSON (an Alexa request): the value at `path`, or undefined when any step is not an object.
// Shared by alexa.ts and alexa-favourites.ts. Kept free of enums so node's type stripping can run it in tests.

export function objectAt(value: unknown, ...path: string[]): unknown {
  let current: unknown = value;
  for (const key of path) {
    if (typeof current !== "object" || current === null) return undefined;
    current = (current as Record<string, unknown>)[key];
  }
  return current;
}
