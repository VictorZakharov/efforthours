import assert from "node:assert/strict";
import test from "node:test";
import { formatStatus } from "../src/status.js";

test("formats a ready status", () => {
  assert.equal(formatStatus(" ready "), "READY");
});

test("formats an offline status", () => {
  assert.equal(formatStatus("offline"), "OFFLINE");
});
