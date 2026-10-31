import type { PortfolioCurrencyAnalyticsUnvaluedAsset } from "./portfolio-currency-analytics-api.ts";

export type ManualPriceAsset = Pick<
  PortfolioCurrencyAnalyticsUnvaluedAsset,
  "assetId" | "kind" | "name" | "currencyId" | "rateCurrencyId"
>;

export function hasAnalyticsInputsChanged(requestRevision: number, currentRevision: number): boolean {
  return requestRevision !== currentRevision;
}

export function buildManualPriceRows(
  unvaluedAssets: readonly PortfolioCurrencyAnalyticsUnvaluedAsset[],
  trackedAssets: readonly ManualPriceAsset[]
): ManualPriceAsset[] {
  const rows = new Map(trackedAssets.map((asset) => [asset.assetId, asset]));
  for (const asset of unvaluedAssets) {
    if (asset.missingPrice) {
      rows.set(asset.assetId, {
        assetId: asset.assetId,
        kind: asset.kind,
        name: asset.name,
        currencyId: asset.currencyId,
        rateCurrencyId: asset.rateCurrencyId
      });
    }
  }

  return [...rows.values()].sort((left, right) => left.name.localeCompare(right.name, "ru"));
}

export function getManualRateCurrencies(
  unvaluedAssets: readonly PortfolioCurrencyAnalyticsUnvaluedAsset[],
  manualRates: Readonly<Record<string, number | undefined>>
): string[] {
  const currencies = new Set(Object.keys(manualRates));
  for (const asset of unvaluedAssets) {
    if (asset.missingRate) {
      currencies.add(asset.rateCurrencyId);
    }
  }

  return [...currencies].sort((left, right) => left.localeCompare(right, "ru"));
}
