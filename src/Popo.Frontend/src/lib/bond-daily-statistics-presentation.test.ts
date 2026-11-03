import assert from "node:assert/strict";
import test from "node:test";
import dayjs from "dayjs";
import {
  getBondDailyChangeDisplay,
  formatBondDailyTurnover,
  getDefaultBondStatisticsDate,
  isBondStatisticsDateDisabled
} from "./bond-daily-statistics-presentation.ts";

test("disables weekends, future dates, and dates before available history", () => {
  const today = dayjs("2026-11-02");

  assert.equal(isBondStatisticsDateDisabled(dayjs("2026-10-31"), today), true);
  assert.equal(isBondStatisticsDateDisabled(dayjs("2026-11-03"), today), true);
  assert.equal(isBondStatisticsDateDisabled(dayjs("2024-12-31"), today), true);
  assert.equal(isBondStatisticsDateDisabled(dayjs("2026-10-30"), today), false);
});

test("uses the previous weekday when today falls on a weekend", () => {
  const date = getDefaultBondStatisticsDate(dayjs("2026-11-01"));

  assert.equal(date.format("YYYY-MM-DD"), "2026-10-30");
});

test("presents a decline as a percentage with its last-trade price range in the tooltip", () => {
  assert.deepEqual(
    getBondDailyChangeDisplay({ changePercent: -31.98, previousPrice: 104, currentPrice: 70.74 }),
    {
      percent: "−31,98%",
      tooltip: "Цена последней сделки: 104,00 → 70,74",
      isIncrease: false
    }
  );
});

test("presents an increase with an explicit plus sign and last-trade price range", () => {
  assert.deepEqual(
    getBondDailyChangeDisplay({ changePercent: 3.25, previousPrice: 100, currentPrice: 103.25 }),
    {
      percent: "+3,25%",
      tooltip: "Цена последней сделки: 100,00 → 103,25",
      isIncrease: true
    }
  );
});

test("formats bond turnover in billions of rubles with one decimal place", () => {
  assert.equal(formatBondDailyTurnover(1_234_567_890), "1,2 млрд. руб.");
});
