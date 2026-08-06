// @generated
export const generatedStatusOperations = Object.freeze([
  { method: "GET", path: "/status" },
  { method: "POST", path: "/status/refresh" },
]);

export function createGeneratedStatusClient(transport) {
  return {
    load: () => transport.get("/status"),
    refresh: () => transport.post("/status/refresh"),
  };
}
