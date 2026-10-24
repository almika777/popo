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
    { date: "2026-01-01", returnPercent: 0, profitPercent: 0 },
    { date: "2026-02-01", returnPercent: 0.1, profitPercent: 0.1 }
  ],
  indexedBondReturn: null,
  indexedBondStartObservationDate: null,
  indexedBondEndObservationDate: null,
  indexedBondPoints: [],
  indexedBondUnavailableReason: "Нет исторических данных",
  indexedBondComparisonStartDate: null,
  portfolioProfitPercent: null,
  lqdtProfitPercent: 0.1,
  indexedBondProfitPercent: null,
  tmonReturn: null,
  tmonStartObservationDate: null,
  tmonEndObservationDate: null,
  tmonPoints: [],
  tmonUnavailableReason: "Нет торговых цен TMON в выбранном периоде",
  tmonProfitPercent: null,
  portfolioUnavailableReason: "Нет оценки портфеля на дату",
  comparisonStartDate: "2026-01-01"
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

test("frontend rejects a benchmark response without the shared comparison start date", () => {
  const { comparisonStartDate: _comparisonStartDate, ...invalidResponse } = partialBenchmarkResult;

  assert.equal(hasCurrentBenchmarkContract(invalidResponse as PortfolioBenchmarkResult), false);
});

test("frontend rejects inconsistent TMON points and totals", () => {
  const invalidResponse = {
    ...partialBenchmarkResult,
    tmonReturn: 0.1,
    tmonProfitPercent: 0.1,
    tmonPoints: [{ date: "2026-01-01", returnPercent: 0, profitPercent: 0 }]
  };

  assert.equal(hasCurrentBenchmarkContract(invalidResponse as PortfolioBenchmarkResult), false);
});

test("frontend rejects a legacy benchmark response without TMON fields", () => {
  const {
    tmonReturn: _tmonReturn,
    tmonStartObservationDate: _tmonStartObservationDate,
    tmonEndObservationDate: _tmonEndObservationDate,
    tmonPoints: _tmonPoints,
    tmonUnavailableReason: _tmonUnavailableReason,
    tmonProfitPercent: _tmonProfitPercent,
    ...legacyResponse
  } = partialBenchmarkResult;

  assert.equal(hasCurrentBenchmarkContract(legacyResponse as PortfolioBenchmarkResult), false);
});
