export function normalizeStatus(value) {
  return value.trim().toUpperCase();
}

export function isHealthy(code) {
  return code >= 200 && code < 300;
}

export function displayLatency(milliseconds) {
  return milliseconds < 1000 ? `${milliseconds}ms` : `${milliseconds / 1000}s`;
}

export function classifyLoad(active, capacity) {
  if (capacity <= 0) return "unknown";
  if (active >= capacity) return "full";
  return active > capacity / 2 ? "busy" : "ready";
}

export function selectRegion(primary, fallback) {
  return primary || fallback || "local";
}

export function retryDelay(attempt) {
  return Math.min(30_000, 250 * 2 ** attempt);
}
