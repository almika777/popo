import assert from "node:assert/strict";
import test from "node:test";
import { buildPositionAnnualizedBreakdownView } from "./position-annualized-breakdown.ts";

test("builds formula and per-lot rows from the API breakdown", () => {
  const result = buildPositionAnnualizedBreakdownView({
    valuationDate: "2026-10-06",
    totalPnl: 480,
    investedCapitalDays: 45_020,
    annualizedPercent: 389.16,
    lots: [
      {
        tradeDate: "2026-10-01",
        quantity: 5,
        purchaseCleanPricePerUnit: 1_000,
        paidAccruedInterestPerUnit: 10,
        commissionPerUnit: 2,
        investedCapital: 5_060,
        holdingDays: 5,
        capitalDays: 25_300
      },
      {
        tradeDate: "2026-10-04",
        quantity: 10,
        purchaseCleanPricePerUnit: 980,
        paidAccruedInterestPerUnit: 5,
        commissionPerUnit: 1,
        investedCapital: 9_860,
        holdingDays: 2,
        capitalDays: 19_720
      }
    ]
  }, "RUB");

  assert.equal(result.valuationDate, "06.10.2026");
  assert.match(result.formula, /365 × \+?480,00 RUB ÷ 45\s*020,00 RUB·дней × 100 ≈ 389,16%/);
  assert.deepEqual(result.lots.map((lot) => ({
    tradeDate: lot.tradeDate,
    quantity: lot.quantity,
    purchaseCleanPrice: lot.purchaseCleanPrice,
    paidAccruedInterest: lot.paidAccruedInterest,
    commission: lot.commission,
    investedCapital: lot.investedCapital,
    holdingDays: lot.holdingDays,
    capitalDays: lot.capitalDays
  })), [
    {
      tradeDate: "01.10.2026",
      quantity: "5",
      purchaseCleanPrice: "1 000,00 RUB",
      paidAccruedInterest: "10,00 RUB",
      commission: "2,00 RUB",
      investedCapital: "5 060,00 RUB",
      holdingDays: "5 дн.",
      capitalDays: "25 300,00 RUB·дней"
    },
    {
      tradeDate: "04.10.2026",
      quantity: "10",
      purchaseCleanPrice: "980,00 RUB",
      paidAccruedInterest: "5,00 RUB",
      commission: "1,00 RUB",
      investedCapital: "9 860,00 RUB",
      holdingDays: "2 дн.",
      capitalDays: "19 720,00 RUB·дней"
    }
  ]);
});

test("keeps absent legacy API breakdown safe", () => {
  assert.equal(buildPositionAnnualizedBreakdownView(null, "RUB"), null);
  assert.equal(buildPositionAnnualizedBreakdownView(undefined, "RUB"), null);
});
