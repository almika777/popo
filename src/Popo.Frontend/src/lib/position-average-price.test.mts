import assert from "node:assert/strict";
import test from "node:test";
import { getPositionAverageBuyPrice } from "./position-average-price.ts";

test("uses the current-nominal average price for an amortized bond", () => {
  assert.deepEqual(getPositionAverageBuyPrice({
    averageBuyPrice: 751.5057692307693,
    averageBuyPriceAtCurrentFaceValue: 501.0038461538462,
    isNominalIndexed: false
  }), {
    value: 501.0038461538462,
    basis: "current-nominal"
  });
});

test("shows the actual average investment price for an indexed-nominal bond", () => {
  assert.deepEqual(getPositionAverageBuyPrice({
    averageBuyPrice: 1032.2798786732458,
    averageBuyPriceAtCurrentFaceValue: 1068.2940865210594,
    isNominalIndexed: true
  }), {
    value: 1032.2798786732458,
    basis: "indexed-nominal"
  });
});

test("marks the actual average price when nominal normalization is unavailable", () => {
  assert.deepEqual(getPositionAverageBuyPrice({
    averageBuyPrice: 751.5057692307693,
    averageBuyPriceAtCurrentFaceValue: null,
    isNominalIndexed: false
  }), {
    value: 751.5057692307693,
    basis: "actual"
  });
});
