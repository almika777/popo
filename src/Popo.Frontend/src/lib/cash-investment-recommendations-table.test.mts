import assert from "node:assert/strict";
import test from "node:test";
import {
  buildRecommendationMobileDetails,
  buildYieldCashFlowRows,
  getRecommendationMobileBondName,
  recommendationColumnTitles,
  recommendationYtmActionLabel
} from "./cash-investment-recommendations-table.ts";

const recommendationBond = {
  secId: "RU000A10FU60",
  boardId: "TQCB",
  shortName: "СФО ВТБ РКС Олимп",
  issuerId: 17,
  issuerName: "СФО ВТБ РКС Олимп",
  currencyId: "RUB",
  ytm: 18.59,
  buyPrice: 923.4,
  rating: "AAA" as const,
  maturityDate: "2028-12-14",
  offerDate: "2027-12-14",
  couponType: "Floating" as const,
  medianDailyVolume: 152430,
  yieldDate: "2027-12-14",
  yieldDateType: "Offer" as const,
  calculation: {
    settlementDate: "2026-10-25",
    marketPricePercent: 92.34,
    cleanPrice: 920,
    accruedInterest: 3.4,
    dirtyPrice: 923.4,
    faceValue: 1000,
    cashFlows: []
  }
};

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

test("mobile recommendation details include all non-calculation fields shown in the sheet", () => {
  assert.deepEqual(buildRecommendationMobileDetails(recommendationBond), [
    { key: "ytm", label: "YTM", value: "18,59%" },
    { key: "yieldDate", label: "YTM до", value: "14.12.2027 (Оферта)" },
    { key: "buyPrice", label: "Грязная цена", value: "923,40 RUB" },
    { key: "couponType", label: "Купон", value: "Флоатер" },
    { key: "maturityDate", label: "Погашение", value: "14.12.2028" },
    { key: "medianDailyVolume", label: "Медианный объём, шт.", value: "152 430" }
  ]);
});

test("mobile recommendation details mark maturity as the YTM date when there is no offer", () => {
  const details = buildRecommendationMobileDetails({
    ...recommendationBond,
    couponType: "Fixed",
    yieldDateType: "Maturity",
    yieldDate: "2028-12-14"
  });

  assert.equal(details.find((detail) => detail.key === "yieldDate")?.value, "14.12.2028 (Погашение)");
  assert.equal(details.find((detail) => detail.key === "couponType")?.value, "Фиксированный");
});

test("mobile recommendation identity prefers the bond short name over the legal issuer name", () => {
  assert.equal(getRecommendationMobileBondName({
    shortName: "СФО ВТБ РКС Олимп",
    issuerName: "Общество с ограниченной ответственностью \"Специализированное финансовое общество ВТБ РКС\""
  }), "СФО ВТБ РКС Олимп");
});

test("mobile recommendation identity falls back to issuer name when the short name is missing", () => {
  assert.equal(getRecommendationMobileBondName({ issuerName: "Полное имя эмитента" }), "Полное имя эмитента");
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
