import assert from "node:assert/strict";
import test from "node:test";
import dayjs from "dayjs";
import {
  getPortfolioComparisonUnavailableHint,
  isBenchmarkEndDateDisabled,
  isBenchmarkStartDateDisabled
} from "./portfolio-benchmark-presentation.ts";

test("comparison period requires the end date to be later than the start date", () => {
  const start = dayjs("2026-01-10");
  const end = dayjs("2026-01-20");

  assert.equal(isBenchmarkStartDateDisabled(end, end), true);
  assert.equal(isBenchmarkStartDateDisabled(dayjs("2026-01-21"), end), true);
  assert.equal(isBenchmarkStartDateDisabled(start, end), false);
  assert.equal(isBenchmarkEndDateDisabled(start, start), true);
  assert.equal(isBenchmarkEndDateDisabled(dayjs("2026-01-09"), start), true);
  assert.equal(isBenchmarkEndDateDisabled(end, start), false);
});

test("unavailable portfolio comparison does not claim an instrument excess return", () => {
  assert.equal(
    getPortfolioComparisonUnavailableHint("Нет оценки портфеля на дату"),
    "Портфель не рассчитан; сравнение с ним недоступно."
  );
  assert.equal(getPortfolioComparisonUnavailableHint(null), null);
});
