import { serviceStates } from "@efforthours-mutation/contracts";

export function normalizeHealth(value) {
  const normalized = value.trim().toLowerCase();
  return serviceStates.includes(normalized) ? normalized : "offline";
}
