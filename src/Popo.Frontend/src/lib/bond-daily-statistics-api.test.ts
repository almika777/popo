import assert from "node:assert/strict";
import test from "node:test";
import { bondDailyStatisticsApi } from "./bond-daily-statistics-api.ts";

test("sends date, liquidity thresholds, and issuer category to the statistics endpoint", async () => {
  const originalFetch = globalThis.fetch;
  let requestedUrl = "";
  globalThis.fetch = (async input => {
    requestedUrl = String(input);
    return new Response(JSON.stringify({
      date: "2026-10-27",
      comparedWithDate: "2026-10-26",
      declines: [],
      increases: [],
      turnovers: []
    }));
  }) as typeof fetch;

  try {
    await bondDailyStatisticsApi.getDailyStatistics("2026-10-27", 5, 1_000_000, undefined, "Government");
  } finally {
    globalThis.fetch = originalFetch;
  }

  const query = new URL(requestedUrl).searchParams;
  assert.equal(query.get("date"), "2026-10-27");
  assert.equal(query.get("minimumTrades"), "5");
  assert.equal(query.get("minimumTurnoverRub"), "1000000");
  assert.equal(query.get("issuerCategory"), "Government");
});
