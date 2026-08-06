export interface StatusValue {
  value: string;
}

export function normalizeStatus(status: StatusValue): string {
  return status.value.trim().toUpperCase();
}
