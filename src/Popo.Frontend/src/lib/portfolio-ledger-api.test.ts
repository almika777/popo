import assert from "node:assert/strict";
import test from "node:test";
import { portfolioLedgerApi } from "./portfolio-ledger-api.ts";

test("does not request bond search until the trimmed query has three characters", async () => {
  const originalFetch = globalThis.fetch;
  let requestCount = 0;
  globalThis.fetch = async () => {
    requestCount++;
    return new Response("[]", { status: 200 });
  };

  try {
    const result = await portfolioLedgerApi.searchBonds(" ab ");

    assert.deepEqual(result, []);
    assert.equal(requestCount, 0);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("requests bond search at the three-character boundary after trimming", async () => {
  const originalFetch = globalThis.fetch;
  let requestedUrl = "";
  globalThis.fetch = async (input) => {
    requestedUrl = String(input);
    return new Response("[]", { status: 200 });
  };

  try {
    await portfolioLedgerApi.searchBonds(" abc ");

    assert.match(requestedUrl, /\/bonds\/search\?q=abc$/);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("loads and edits current money-market fund positions through liquidity routes", async () => {
  const originalFetch = globalThis.fetch;
  const requests: Array<{ url: string; method: string }> = [];
  globalThis.fetch = async (input, init) => {
    requests.push({ url: String(input), method: init?.method ?? "GET" });
    return new Response(init?.method === "DELETE" ? null : "{}", { status: init?.method === "DELETE" ? 204 : 200 });
  };

  try {
    await portfolioLedgerApi.getMoneyMarketFundsPage();
    const payload = { secId: "LQDT", quantity: 100, averagePrice: 2.15 };
    await portfolioLedgerApi.addMoneyMarketFund(payload);
    await portfolioLedgerApi.updateMoneyMarketFund("fund-id", payload);
    await portfolioLedgerApi.deleteMoneyMarketFund("fund-id");

    assert.deepEqual(requests, [
      { url: "http://localhost:5109/api/liquidity/money-market-funds/page", method: "GET" },
      { url: "http://localhost:5109/api/liquidity/money-market-funds", method: "POST" },
      { url: "http://localhost:5109/api/liquidity/money-market-funds/fund-id", method: "PUT" },
      { url: "http://localhost:5109/api/liquidity/money-market-funds/fund-id", method: "DELETE" }
    ]);
    assert.equal("addMoneyMarketFundOperation" in portfolioLedgerApi, false);
  } finally {
    globalThis.fetch = originalFetch;
  }
});
