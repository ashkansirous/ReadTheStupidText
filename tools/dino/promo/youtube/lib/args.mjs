// Shared command-line helper: the value after a flag, refused when it is missing or is the next flag.

export function valueOf(argv, i, flag) {
  const value = argv[i + 1];
  if (value === undefined || value.startsWith('--')) throw new Error(`${flag} needs a value`);
  return value;
}
