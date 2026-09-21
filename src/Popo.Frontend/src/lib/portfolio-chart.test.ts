import assert from "node:assert/strict";
import test from "node:test";
import {
  buildCashFlowAdjustedValuations,
  buildYearEndForecast,
  buildRoundedScale,
  toRelativePortfolioChanges
} from "./portfolio-chart.ts";

test("rounds a positive chart maximum up to a readable boundary", () => {
  assert.deepEqual(buildRoundedScale([0, 522_870]), {
    min: 0,
    max: 600_000,
    ticks: [0, 200_000, 400_000, 600_000]
  });
});

test("projects the year-to-date daily change through the end of year", () => {
  const forecast = buildYearEndForecast("2026-01-01", "2026-08-13", 522_870);

  assert.equal(forecast.date, "2026-12-31");
  assert.equal(Math.round(forecast.totalValue), 849_664);
});

test("excludes deposits, withdrawals and taxes from portfolio changes", () => {
  const valuations = [
    { id: "1", date: "2026-01-01", totalValue: 8_061_000, comment: "" },
    { id: "2", date: "2026-01-04", totalValue: 8_067_000, comment: "" },
    { id: "3", date: "2026-01-05", totalValue: 8_025_000, comment: "" },
    { id: "4", date: "2026-01-06", totalValue: 8_008_000, comment: "" },
    { id: "5", date: "2026-04-02", totalValue: 8_332_400, comment: "" },
    { id: "6", date: "2026-04-03", totalValue: 8_627_000, comment: "" },
    { id: "7", date: "2026-04-04", totalValue: 8_631_000, comment: "" },
    { id: "8", date: "2026-06-21", totalValue: 8_657_000, comment: "" },
    { id: "9", date: "2026-06-22", totalValue: 8_599_000, comment: "" },
    { id: "10", date: "2026-06-23", totalValue: 6_632_000, comment: "" },
    { id: "11", date: "2026-07-12", totalValue: 6_510_000, comment: "" },
    { id: "12", date: "2026-07-13", totalValue: 4_471_000, comment: "" },
    { id: "13", date: "2026-07-14", totalValue: 4_480_000, comment: "" },
    { id: "14", date: "2026-07-19", totalValue: 4_436_000, comment: "" },
    { id: "15", date: "2026-07-20", totalValue: 4_504_000, comment: "" },
    { id: "16", date: "2026-08-09", totalValue: 4_769_353, comment: "" },
    { id: "17", date: "2026-08-10", totalValue: 4_763_693, comment: "" },
    { id: "18", date: "2026-08-11", totalValue: 4_791_901, comment: "" }
  ];
  const cashFlows = [
    { date: "2026-01-05", type: "Tax" as const, amount: 45_000 },
    { date: "2026-04-03", type: "Deposit" as const, amount: 300_000 },
    { date: "2026-06-22", type: "Withdrawal" as const, amount: 1_921_000 },
    { date: "2026-06-22", type: "Tax" as const, amount: 78_000 },
    { date: "2026-07-13", type: "Withdrawal" as const, amount: 2_050_000 }
  ];

  const result = toRelativePortfolioChanges(buildCashFlowAdjustedValuations(valuations, cashFlows));

  assert.equal(result.at(-1)?.totalValue, 524_901);
  assert.deepEqual(
    result.slice(7, 12).map((row) => row.totalValue),
    [341_000, 283_000, 315_000, 193_000, 204_000]
  );
});

test("rebases adjusted portfolio values to zero for the visible period", () => {
  const result = toRelativePortfolioChanges([
    { id: "start", date: "2026-01-01", totalValue: 8_061_000, comment: "" },
    { id: "middle", date: "2026-04-03", totalValue: 8_372_000, comment: "" },
    { id: "end", date: "2026-08-11", totalValue: 8_585_901, comment: "" }
  ]);

  assert.deepEqual(result.map((row) => row.totalValue), [0, 311_000, 524_901]);
});

test("rebases a selected subrange independently", () => {
  const result = toRelativePortfolioChanges([
    { id: "middle", date: "2026-04-03", totalValue: 8_372_000, comment: "" },
    { id: "end", date: "2026-08-11", totalValue: 8_585_901, comment: "" }
  ]);

  assert.deepEqual(result.map((row) => row.totalValue), [0, 213_901]);
});
