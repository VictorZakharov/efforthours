import { formatStatus } from "./status.js";

export function StatusView({ status }) {
  return <output data-state={status}>{formatStatus(status)}</output>;
}
