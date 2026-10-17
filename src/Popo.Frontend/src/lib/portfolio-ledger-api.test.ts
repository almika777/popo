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
