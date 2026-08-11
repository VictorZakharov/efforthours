import { formatStatus } from "@efforthours-mutation/shared";

export function StatusCard({ healthy }) {
  return <article><h2>API status</h2><output>{formatStatus(healthy)}</output></article>;
}
