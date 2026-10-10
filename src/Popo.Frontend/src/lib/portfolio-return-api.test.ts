import assert from "node:assert/strict";
import test from "node:test";
import { hasCurrentBenchmarkContract, type PortfolioBenchmarkResult } from "./portfolio-return-api.ts";

const partialBenchmarkResult: PortfolioBenchmarkResult = {
  from: "2026-01-01",
  to: "2026-02-01",
  portfolioReturn: null,
  lqdtReturn: 0.1,
  excessReturn: null,
  lqdtStartObservationDate: "2026-01-01",
  lqdtEndObservationDate: "2026-02-01",
  portfolioPoints: [],
  lqdtPoints: [
    { date: "2026-01-01", returnPercent: 0, profitPercent: 0, twrPercent: 0 },
    { date: "2026-02-01", returnPercent: 0.1, profitPercent: 0.1, twrPercent: 0.1 }
  ],
  indexedBondReturn: null,
  indexedBondStartObservationDate: null,
  indexedBondEndObservationDate: null,
  indexedBondPoints: [],
  indexedBondUnavailableReason: "Нет исторических данных",
  indexedBondComparisonStartDate: null,
  indexedBondPortfolioReturn: null,
  portfolioProfitPercent: null,
  lqdtProfitPercent: 0.1,
  indexedBondProfitPercent: null,
  portfolioTwrPercent: null,
  lqdtTwrPercent: 0.1,
  indexedBondTwrPercent: null,
  portfolioUnavailableReason: "Нет оценки портфеля на дату"
};

test("frontend accepts serialized partial response with a valid LQDT series", () => {
  const serializedResponse = JSON.parse(JSON.stringify(partialBenchmarkResult)) as PortfolioBenchmarkResult;

  assert.equal(hasCurrentBenchmarkContract(serializedResponse), true);
});

test("frontend rejects a partial response without an explanation", () => {
  const invalidResponse = {
    ...partialBenchmarkResult,
    portfolioUnavailableReason: null
  };

  assert.equal(hasCurrentBenchmarkContract(invalidResponse), false);
});
