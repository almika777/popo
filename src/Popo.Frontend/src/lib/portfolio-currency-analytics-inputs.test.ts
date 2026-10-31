import assert from "node:assert/strict";
import test from "node:test";
import type { PortfolioCurrencyAnalyticsUnvaluedAsset } from "./portfolio-currency-analytics-api.ts";
import {
  buildManualPriceRows,
  hasAnalyticsInputsChanged,
  getManualRateCurrencies,
  type ManualPriceAsset
} from "./portfolio-currency-analytics-inputs.ts";

const asset: PortfolioCurrencyAnalyticsUnvaluedAsset = {
  assetId: "position:bond-1",
  kind: "Position",
  name: "Тестовая облигация",
  currencyId: "CNY",
  rateCurrencyId: "USD",
  missingPrice: true,
  missingRate: true
};
const manualAsset: ManualPriceAsset = {
  assetId: asset.assetId,
  kind: asset.kind,
  name: asset.name,
  currencyId: asset.currencyId,
  rateCurrencyId: asset.rateCurrencyId
};

test("manual price editor keeps its tracked row after valuation or value clearing", () => {
  const tracked: ManualPriceAsset[] = [manualAsset];

  assert.deepEqual(buildManualPriceRows([], tracked), [manualAsset]);
});

test("manual price editor includes newly unvalued assets and updates tracked metadata", () => {
  const tracked: ManualPriceAsset[] = [{ ...manualAsset, name: "Старое название" }];

  assert.deepEqual(buildManualPriceRows([asset], tracked), [manualAsset]);
});

test("manual rate editor keeps a currency visible after its rate resolves the asset", () => {
  assert.deepEqual(getManualRateCurrencies([], { USD: 90 }), ["USD"]);
});

test("manual rate editor keeps a cleared currency editable until recalculation", () => {
  assert.deepEqual(getManualRateCurrencies([], { USD: undefined }), ["USD"]);
});

test("manual rate editor includes newly missing rates without duplicates", () => {
  const rateMissingAsset = { ...asset, missingPrice: false, missingRate: true };

  assert.deepEqual(getManualRateCurrencies([rateMissingAsset], { USD: 90 }), ["USD"]);
});

test("an input edit made after a calculation starts keeps its result marked stale", () => {
  assert.equal(hasAnalyticsInputsChanged(7, 8), true);
  assert.equal(hasAnalyticsInputsChanged(7, 7), false);
});
