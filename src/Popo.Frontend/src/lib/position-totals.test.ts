import assert from "node:assert/strict";
import test from "node:test";
import { portfolioLedgerApi } from "./portfolio-ledger-api.ts";

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
