import assert from "node:assert/strict";
import test from "node:test";
import { getHorizontalScrollAdjustment } from "./portfolio-tab-navigation.ts";

test("scrolls a selected tab just enough to fit its right edge", () => {
  assert.equal(getHorizontalScrollAdjustment({ left: 32, right: 273 }, { left: 229, right: 305 }), 32);
});

test("scrolls a selected tab just enough to fit its left edge", () => {
  assert.equal(getHorizontalScrollAdjustment({ left: 32, right: 273 }, { left: 12, right: 90 }), -20);
});

test("does not move the tab bar when the selected tab is fully visible", () => {
  assert.equal(getHorizontalScrollAdjustment({ left: 32, right: 273 }, { left: 100, right: 180 }), 0);
});
