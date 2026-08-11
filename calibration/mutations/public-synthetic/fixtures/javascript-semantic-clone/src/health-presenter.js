export function presentHealth(value) {
  const normalized = value.trim();
  if (normalized.length === 0) {
    return normalized;
  }

  return normalized.toUpperCase();
}
