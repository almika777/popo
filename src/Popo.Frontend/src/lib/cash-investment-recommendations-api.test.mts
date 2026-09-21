import assert from "node:assert/strict";
import test from "node:test";

import { cashInvestmentRecommendationsApi } from "./cash-investment-recommendations-api.ts";

test("getCurrent requests the current liquidity snapshot", async () => {
  const calls: Array<{ url: string; method: string }> = [];
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), method: init?.method ?? "GET" });
    return new Response(JSON.stringify({ snapshotTime: "2026-08-19T10:30:00Z", bonds: [] }), {
      status: 200,
      headers: { "Content-Type": "application/json" }
    });
  };

  try {
    const result = await cashInvestmentRecommendationsApi.getCurrent();
    assert.deepEqual(result.bonds, []);
    assert.deepEqual(calls, [{ url: "http://localhost:5109/api/recommendations/cash", method: "GET" }]);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("recalculate accepts an empty 202 response", async () => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => new Response(null, { status: 202 });

  try {
    await cashInvestmentRecommendationsApi.recalculate();
  } finally {
    globalThis.fetch = originalFetch;
  }
});
