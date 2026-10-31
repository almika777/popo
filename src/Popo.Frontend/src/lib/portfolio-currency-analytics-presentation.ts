import type {
  PortfolioCurrencyAnalyticsResponse,
  PortfolioCurrencyAnalyticsItem,
  PortfolioCurrencyAnalyticsSlice,
  PortfolioCurrencyAnalyticsUnvaluedAsset
} from "./portfolio-currency-analytics-api.ts";

export type PortfolioCurrencyAnalyticsPresentedSlice = PortfolioCurrencyAnalyticsSlice & {
  sharePercent: number;
};

export type PortfolioCurrencyAnalyticsPresentation = {
  totalValueRub: number;
  slices: PortfolioCurrencyAnalyticsPresentedSlice[];
  groups: PortfolioCurrencyAnalyticsPresentedGroup[];
  unvaluedAssets: PortfolioCurrencyAnalyticsUnvaluedAsset[];
  isPartial: boolean;
  isEmpty: boolean;
};

export type PortfolioCurrencyAnalyticsPresentedItem = PortfolioCurrencyAnalyticsItem;

export type PortfolioCurrencyAnalyticsPresentedGroup = {
  currencyId: string;
  valueRub: number;
  share: number;
  sharePercent: number;
  items: PortfolioCurrencyAnalyticsPresentedItem[];
};

export type PortfolioCurrencyAnalyticsItemFilter = "all" | "position" | "cash" | "fund";

export function filterPortfolioCurrencyAnalyticsItems<T extends Pick<PortfolioCurrencyAnalyticsItem, "kind">>(
  items: readonly T[],
  filter: PortfolioCurrencyAnalyticsItemFilter
): T[] {
  if (filter === "all") {
    return [...items];
  }

  const kindByFilter = {
    position: "Position",
    cash: "Cash",
    fund: "MoneyMarketFund"
  } satisfies Record<Exclude<PortfolioCurrencyAnalyticsItemFilter, "all">, PortfolioCurrencyAnalyticsItem["kind"]>;

  return items.filter((item) => item.kind === kindByFilter[filter]);
}

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
  const responseItems = Array.isArray(response.items) ? response.items : [];
  const itemKeys = new Set(responseItems.map((item) => `${item.kind}:${item.assetId}`));
  const items = [
    ...responseItems,
    ...unvaluedAssets
      .filter((asset) => !itemKeys.has(`${asset.kind}:${asset.assetId}`))
      .map((asset): PortfolioCurrencyAnalyticsItem => ({
        assetId: asset.assetId,
        kind: asset.kind,
        name: asset.name,
        currencyId: asset.currencyId,
        tradingCurrencyId: asset.rateCurrencyId,
        valueRub: null,
        missingPrice: asset.missingPrice,
        missingRate: asset.missingRate
      }))
  ].filter((item) => item.currencyId.trim().length > 0);
  const sliceByCurrency = new Map(slices.map((slice) => [normalizeCurrencyId(slice.currencyId), slice]));
  const itemsByCurrency = new Map<string, PortfolioCurrencyAnalyticsPresentedItem[]>();
  for (const item of items) {
    const currencyId = normalizeCurrencyId(item.currencyId);
    const currencyItems = itemsByCurrency.get(currencyId) ?? [];
    currencyItems.push(item);
    itemsByCurrency.set(currencyId, currencyItems);
  }
  const currencyIds = new Set([
    ...sliceByCurrency.keys(),
    ...itemsByCurrency.keys()
  ]);
  const groups = [...currencyIds].sort((left, right) => left.localeCompare(right)).map((currencyId) => {
    const slice = sliceByCurrency.get(currencyId);
    const groupItems = itemsByCurrency.get(currencyId) ?? [];
    const valueRub = slice?.valueRub ?? groupItems.reduce(
      (total, item) => total + (Number.isFinite(item.valueRub) ? item.valueRub! : 0),
      0
    );
    const share = slice?.share ?? (totalValueRub > 0 ? valueRub / totalValueRub : 0);

    return {
      currencyId,
      valueRub,
      share,
      sharePercent: totalValueRub > 0 && Number.isFinite(share) ? Math.max(0, share * 100) : 0,
      items: groupItems
    };
  });

  return {
    totalValueRub,
    slices,
    groups,
    unvaluedAssets,
    isPartial: unvaluedAssets.length > 0,
    isEmpty: totalValueRub === 0 && groups.length === 0 && unvaluedAssets.length === 0
  };
}

function normalizeCurrencyId(currencyId: string): string {
  const normalized = currencyId.trim().toUpperCase();
  return normalized === "SUR" || normalized === "RUR" ? "RUB" : normalized;
}
