import assert from "node:assert/strict";
import test from "node:test";
import { portfolioLedgerApi } from "./portfolio-ledger-api.ts";

test("displays fields returned by the open positions API", async () => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => new Response(JSON.stringify({
    positions: [{ secId: "BOND", boardId: "TQCB", currencyId: "RUB", shortName: "Bond", bondType: "Флоатер",
      quantity: 3, avgBuyPrice: 1000, marketPrice: 1100, totalCost: 3300,
      unrealizedPnl: 300, couponIncome: 25, totalPnl: 320 }],
    totals: { marketValueRub: 3300, investedAmountRub: 3000, unrealizedPnlRub: 300, unrealizedPnlPercent: 10 }
  }), { status: 200 });
  try {
    const row = (await portfolioLedgerApi.getPositionsPage()).positions[0];
    assert.equal(row.bondType, "Флоатер");
    assert.equal(row.averageBuyPrice, 1000);
    assert.equal(row.marketValue, 3300);
    assert.equal(row.approximateCouponIncome, 25);
    assert.equal(row.approximateTotalPnl, 320);
    assert.equal(row.approximateTotalPnlPercent, null);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("loads position totals calculated by the API", async () => {
  const originalFetch = globalThis.fetch;
  const totals = {
    marketValueRub: 2_000,
    investedAmountRub: 2_100,
    unrealizedPnlRub: -100,
    unrealizedPnlPercent: -100 / 2_100 * 100
  };
  globalThis.fetch = async () => new Response(JSON.stringify({ positions: [], totals }), {
    status: 200,
    headers: { "Content-Type": "application/json" }
  });

  try {
    const page = await portfolioLedgerApi.getPositionsPage();
    assert.deepEqual(page.totals, totals);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("preserves unavailable position totals from the API", async () => {
  const originalFetch = globalThis.fetch;
  const totals = {
    marketValueRub: 0,
    investedAmountRub: null,
    unrealizedPnlRub: null,
    unrealizedPnlPercent: null
  };
  globalThis.fetch = async () => new Response(JSON.stringify({ positions: [], totals }), {
    status: 200,
    headers: { "Content-Type": "application/json" }
  });

  try {
    const page = await portfolioLedgerApi.getPositionsPage();
    assert.deepEqual(page.totals, totals);
  } finally {
    globalThis.fetch = originalFetch;
  }
});
