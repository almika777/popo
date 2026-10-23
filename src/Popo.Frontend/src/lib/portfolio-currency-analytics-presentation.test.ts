import assert from "node:assert/strict";
import test from "node:test";
import {
  buildPortfolioCurrencyAnalyticsPresentation,
  type PortfolioCurrencyAnalyticsResponse
} from "./portfolio-currency-analytics-presentation.ts";

const response: PortfolioCurrencyAnalyticsResponse = {
  asOf: "2026-10-24",
  totalValueRub: 250000,
  availableCurrencies: ["RUB", "CNY"],
  slices: [
    { currencyId: "RUB", valueRub: 200000, share: 0.8 },
    { currencyId: "CNY", valueRub: 50000, share: 0.2 }
  ],
  unvaluedAssets: [
    {
      assetId: "position:bond-1",
      kind: 0,
      name: "Тестовая облигация",
      currencyId: "CNY",
      rateCurrencyId: "USD",
      missingPrice: true,
      missingRate: false
    }
  ]
};

test("maps currency slices and incomplete assets for charts and the detail list", () => {
  const presentation = buildPortfolioCurrencyAnalyticsPresentation(response);

  assert.equal(presentation.totalValueRub, 250000);
  assert.deepEqual(presentation.slices, [
    { currencyId: "RUB", valueRub: 200000, share: 0.8, sharePercent: 80 },
    { currencyId: "CNY", valueRub: 50000, share: 0.2, sharePercent: 20 }
  ]);
  assert.deepEqual(presentation.unvaluedAssets, response.unvaluedAssets);
  assert.equal(presentation.isPartial, true);
});

test("empty and zero-valued portfolios produce no NaN or infinite chart values", () => {
  const empty = buildPortfolioCurrencyAnalyticsPresentation({
    ...response,
    totalValueRub: 0,
    slices: [],
    unvaluedAssets: []
  });
  const zeroSlice = buildPortfolioCurrencyAnalyticsPresentation({
    ...response,
    totalValueRub: 0,
    slices: [{ currencyId: "RUB", valueRub: 0, share: 0 }],
    unvaluedAssets: []
  });

  assert.deepEqual(empty.slices, []);
  assert.equal(empty.isEmpty, true);
  assert.equal(zeroSlice.slices[0].sharePercent, 0);
  assert.equal(zeroSlice.isEmpty, false);
  assert.equal([...empty.slices, ...zeroSlice.slices].some((slice) => !Number.isFinite(slice.sharePercent)), false);
});
