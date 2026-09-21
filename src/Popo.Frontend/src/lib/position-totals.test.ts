import assert from "node:assert/strict";
import test from "node:test";
import { calculatePositionTotals } from "./position-totals.ts";

test("calculates totals across all positions", () => {
  const result = calculatePositionTotals({
    positionsValueRub: 2_000,
    unrealizedPnlRub: -100
  });

  assert.deepEqual(result, {
    marketValue: 2_000,
    investedAmount: 2_100,
    unrealizedPnl: -100,
    unrealizedPnlPercent: -100 / 2_100 * 100
  });
});

test("returns zero percent when invested amount is zero", () => {
  const result = calculatePositionTotals({
    positionsValueRub: 0,
    unrealizedPnlRub: 0
  });

  assert.deepEqual(result, {
    marketValue: 0,
    investedAmount: 0,
    unrealizedPnl: 0,
    unrealizedPnlPercent: 0
  });
});
