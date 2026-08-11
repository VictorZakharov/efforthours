import { normalizeHealth } from "@efforthours-mutation/domain";

export function HealthCard({ state }) {
  return <article><h2>Service health</h2><output>{normalizeHealth(state)}</output></article>;
}
