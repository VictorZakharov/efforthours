export interface Status {
  value: string;
}

export function formatStatus(status: Status): string {
  return status.value.trim().toUpperCase();
}
