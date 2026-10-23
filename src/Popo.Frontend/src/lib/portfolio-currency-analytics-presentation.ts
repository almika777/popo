import type {
  PortfolioCurrencyAnalyticsResponse,
  PortfolioCurrencyAnalyticsSlice,
  PortfolioCurrencyAnalyticsUnvaluedAsset
} from "./portfolio-currency-analytics-api.ts";

export type PortfolioCurrencyAnalyticsPresentedSlice = PortfolioCurrencyAnalyticsSlice & {
  sharePercent: number;
};

export type PortfolioCurrencyAnalyticsPresentation = {
  totalValueRub: number;
  slices: PortfolioCurrencyAnalyticsPresentedSlice[];
  unvaluedAssets: PortfolioCurrencyAnalyticsUnvaluedAsset[];
  isPartial: boolean;
  isEmpty: boolean;
};

export function buildPortfolioCurrencyAnalyticsPresentation(
  response: PortfolioCurrencyAnalyticsResponse
): PortfolioCurrencyAnalyticsPresentation {
  const totalValueRub = Number.isFinite(response.totalValueRub) && response.totalValueRub >= 0
    ? response.totalValueRub
    : 0;
  const slices = response.slices
    .filter((slice) => slice.currencyId.trim().length > 0 && Number.isFinite(slice.valueRub) && slice.valueRub >= 0)
    .map((slice) => ({
      ...slice,
      sharePercent: totalValueRub > 0 && Number.isFinite(slice.share)
        ? Math.max(0, slice.share * 100)
        : 0
    }));
  const unvaluedAssets = response.unvaluedAssets;

  return {
    totalValueRub,
    slices,
    unvaluedAssets,
    isPartial: unvaluedAssets.length > 0,
    isEmpty: totalValueRub === 0 && slices.length === 0 && unvaluedAssets.length === 0
  };
}
