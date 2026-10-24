import assert from "node:assert/strict";
import test from "node:test";
import { portfolioCurrencyAnalyticsApi, type PortfolioCurrencyAnalyticsRequest } from "./portfolio-currency-analytics-api.ts";

const payload: PortfolioCurrencyAnalyticsRequest = {
  cashBalances: [{ currencyId: "CNY", amount: 1200 }],
  manualPrices: [{ assetId: "position:bond-1", price: 98.5 }],
  manualRates: [{ currencyId: "CNY", rateToRub: 12.4 }]
};

test("posts transient inputs to the portfolio currency analytics endpoint", async () => {
  const originalFetch = globalThis.fetch;
  let requestUrl = "";
  let requestMethod = "";
  let requestBody = "";
  globalThis.fetch = async (input, init) => {
    requestUrl = String(input);
    requestMethod = init?.method ?? "GET";
    requestBody = String(init?.body ?? "");
    return new Response(JSON.stringify({
      asOf: "2026-10-24",
      totalValueRub: 14880,
      availableCurrencies: ["RUB", "CNY"],
      slices: [{ currencyId: "CNY", valueRub: 14880, share: 1 }],
      items: [],
      unvaluedAssets: []
    }), { status: 200 });
  };

  try {
    const response = await portfolioCurrencyAnalyticsApi.calculate(payload);

    assert.match(requestUrl, /\/portfolio\/analytics\/currency$/);
    assert.equal(requestMethod, "POST");
    assert.deepEqual(JSON.parse(requestBody), payload);
    assert.deepEqual(response.availableCurrencies, ["RUB", "CNY"]);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("surfaces network and HTTP errors from the currency analytics endpoint", async () => {
  const originalFetch = globalThis.fetch;

  try {
    globalThis.fetch = async () => { throw new TypeError("network unavailable"); };
    await assert.rejects(portfolioCurrencyAnalyticsApi.calculate(payload), /Не удалось подключиться к серверу/);

    globalThis.fetch = async () => new Response("Курс валюты недоступен", { status: 503 });
    await assert.rejects(portfolioCurrencyAnalyticsApi.calculate(payload), /Курс валюты недоступен/);
  } finally {
    globalThis.fetch = originalFetch;
  }
});
