export function normalizeGeneratedStatus(status) {
  if (status === null || status === undefined) {
    return "UNKNOWN";
  }

  return String(status).trim().toUpperCase();
}
