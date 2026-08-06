// @generated
export const generatedOperations = Object.freeze([
  { method: "GET", path: "/status" },
  { method: "POST", path: "/status/refresh" },
]);

export function createGeneratedClient(transport) {
  return generatedOperations.map(operation => ({ operation, transport }));
}
