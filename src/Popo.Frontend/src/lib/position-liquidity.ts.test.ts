import assert from "node:assert/strict";
import test from "node:test";
import { getPositionLiquidityLevel } from "./position-liquidity.ts";

test("marks a position below 8% of average daily volume as low", () => {
  assert.equal(getPositionLiquidityLevel(79, 1_000), "low");
});

test("marks a position from 8% up to 10% of average daily volume as medium", () => {
  assert.equal(getPositionLiquidityLevel(80, 1_000), "medium");
  assert.equal(getPositionLiquidityLevel(99, 1_000), "medium");
});

test("marks a position at or above 10% of average daily volume as high", () => {
  assert.equal(getPositionLiquidityLevel(100, 1_000), "high");
  assert.equal(getPositionLiquidityLevel(101, 1_000), "high");
});

test("does not assign a level when average daily volume is unavailable", () => {
  assert.equal(getPositionLiquidityLevel(100, null), null);
});
