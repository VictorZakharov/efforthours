import assert from "node:assert/strict";
import test from "node:test";
import { classifyLoad, isHealthy, normalizeStatus } from "../src/health.js";

test("normalizes and classifies healthy status", () => {
  assert.equal(normalizeStatus(" ready "), "READY");
  assert.equal(isHealthy(204), true);
});

test("classifies capacity", () => {
  assert.equal(classifyLoad(9, 10), "busy");
  assert.equal(classifyLoad(10, 10), "full");
});
