import assert from "node:assert/strict";
import test from "node:test";

import {
  buildYieldCashFlowRows,
  recommendationColumnTitles,
  recommendationYtmActionLabel
} from "./cash-investment-recommendations-table.ts";

test("recommendation table identifies yield as YTM and does not duplicate the offer date", () => {
  assert.deepEqual(recommendationColumnTitles, [
    "Облигация",
    "Эмитент",
    "YTM",
    "YTM до",
    "Грязная цена",
    "Купон",
    "Погашение",
    "Медианный объём, шт."
  ]);
});

test("yield value advertises that its calculation details are clickable", () => {
  assert.equal(recommendationYtmActionLabel, "Нажмите, чтобы открыть расчёт YTM");
});

test("yield details expose the cash-flow meaning and projection source", () => {
  assert.deepEqual(buildYieldCashFlowRows([
    { date: "2026-12-14", amount: 89.22, type: "Coupon", isProjected: true },
    { date: "2026-12-14", amount: 1000, type: "Redemption", isProjected: false },
    { date: "2026-09-01", amount: 100, type: "Amortization", isProjected: false }
  ]), [
    { key: "2026-12-14-Coupon-0", date: "2026-12-14", amount: 89.22, typeLabel: "Купон", isProjected: true },
    { key: "2026-12-14-Redemption-1", date: "2026-12-14", amount: 1000, typeLabel: "Погашение", isProjected: false },
    { key: "2026-09-01-Amortization-2", date: "2026-09-01", amount: 100, typeLabel: "Амортизация", isProjected: false }
  ]);
});
