import assert from "node:assert/strict";
import test from "node:test";
import { findPositionRecommendation } from "./position-recommendations.ts";
import type { PositionRecommendationState } from "./position-recommendations-api.ts";

const state: PositionRecommendationState = {
  status: "Ready", calculationStartedAt: null, lastSuccessfulAt: null,
  snapshotTime: null, error: null,
  recommendations: [
    { secId: "SEC", boardId: "A", action: "Buy", quantity: 1, ytm: 20, calculationPrice: 1_001, rating: "AA", averageDailyVolume: 100, positionVolumeShare: 0.01, alternative: { secId: "ALT", boardId: "A", shortName: "Альтернатива", ytm: 19, calculationPrice: 999, rating: "AA", yieldDate: "2027-01-01" }, reasons: ["CurrentYieldAdvantage"] },
    { secId: "SEC", boardId: "B", action: "Sell", quantity: 20, ytm: 31, calculationPrice: 1_002, rating: "AA", averageDailyVolume: 100, positionVolumeShare: 0.2, alternative: { secId: "ALT", boardId: "B", shortName: "Альтернатива", ytm: 32, calculationPrice: 998, rating: "AA", yieldDate: "2027-01-01" }, reasons: ["BetterMarketAlternative"] }
  ]
};

test("matches a recommendation by SecId and BoardId", () => {
  assert.equal(findPositionRecommendation(state, "SEC", "B")?.action, "Sell");
});

test("returns undefined when the board does not match", () => {
  assert.equal(findPositionRecommendation(state, "SEC", "C"), undefined);
});
