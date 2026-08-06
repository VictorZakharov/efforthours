import assert from "node:assert/strict";
import test from "node:test";
import { formatStatus } from "../src/status.js";

test("formats a typed status", () => {
  assert.equal(formatStatus({ value: " ready " }), "READY");
});

test("formats a typed offline status", () => {
  assert.equal(formatStatus({ value: "offline" }), "OFFLINE");
});
