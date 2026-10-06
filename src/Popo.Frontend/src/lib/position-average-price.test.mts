import assert from "node:assert/strict";
import test from "node:test";
import { getPositionAverageBuyPrice } from "./position-average-price.ts";

test("shows the actual average purchase price for an amortized bond", () => {
  assert.deepEqual(getPositionAverageBuyPrice({
    averageBuyPrice: 751.5057692307693,
    averageBuyPriceAtCurrentFaceValue: 501.0038461538462,
    isNominalIndexed: false
  }), {
    value: 751.5057692307693,
    basis: "actual"
  });
});

test("shows the actual purchase average when FX revaluation changes an unchanged nominal price", () => {
  assert.deepEqual(getPositionAverageBuyPrice({
    averageBuyPrice: 82354.99552,
    averageBuyPriceAtCurrentFaceValue: 81288.8429721904,
    isNominalIndexed: false
  }), {
    value: 82354.99552,
    basis: "actual"
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

test("hides average cost when FIFO order is unknown", () => {
  assert.deepEqual(getPositionAverageBuyPrice({
    averageBuyPrice: 1500,
    averageBuyPriceAtCurrentFaceValue: 1500,
    isNominalIndexed: false,
    hasAmbiguousTradeOrder: true
  }), {
    value: null,
    basis: "actual"
  });
});
