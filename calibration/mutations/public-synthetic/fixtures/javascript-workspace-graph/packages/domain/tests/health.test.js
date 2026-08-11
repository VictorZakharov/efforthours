import { normalizeHealth } from "../src/health.js";

test("normalizes known health states", () => {
  expect(normalizeHealth(" READY ")).toBe("ready");
});
