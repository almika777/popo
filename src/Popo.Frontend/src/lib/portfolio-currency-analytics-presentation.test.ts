import assert from "node:assert/strict";
import test from "node:test";
import {
  buildPortfolioCurrencyAnalyticsPresentation,
  filterPortfolioCurrencyAnalyticsItems,
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
  items: [],
  unvaluedAssets: [
    {
      assetId: "position:bond-1",
      kind: "Position",
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
  assert.deepEqual(presentation.groups.map((group) => group.currencyId), ["CNY", "RUB"]);
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

test("groups valued and unvalued assets by nominal currency for the selected segment", () => {
  const responseWithItems = {
    ...response,
    totalValueRub: 14_000,
    slices: [{ currencyId: "CNY", valueRub: 14_000, share: 1 }],
    unvaluedAssets: [
      {
        assetId: "position:bond-usd",
        kind: "Position" as const,
        name: "Неоценённая облигация",
        currencyId: "USD",
        rateCurrencyId: "RUB",
        missingPrice: true,
        missingRate: false
      }
    ],
    items: [
      {
        assetId: "position:bond-cny",
        kind: "Position" as const,
        name: "Юаневая облигация",
        currencyId: "CNY",
        tradingCurrencyId: "RUB",
        valueRub: 10_000,
        missingPrice: false,
        missingRate: false
      },
      {
        assetId: "cash:CNY",
        kind: "Cash" as const,
        name: "Денежные средства CNY",
        currencyId: "CNY",
        tradingCurrencyId: "CNY",
        valueRub: 4_000,
        missingPrice: false,
        missingRate: false
      },
      {
        assetId: "position:bond-usd",
        kind: "Position" as const,
        name: "Неоценённая облигация",
        currencyId: "USD",
        tradingCurrencyId: "RUB",
        valueRub: null,
        missingPrice: true,
        missingRate: false
      }
    ]
  } as PortfolioCurrencyAnalyticsResponse & {
    items: Array<{
      assetId: string;
      kind: "Position" | "MoneyMarketFund" | "Cash";
      name: string;
      currencyId: string;
      tradingCurrencyId: string;
      valueRub: number | null;
      missingPrice: boolean;
      missingRate: boolean;
    }>;
  };

  const presentation = buildPortfolioCurrencyAnalyticsPresentation(responseWithItems);

  assert.deepEqual(presentation.groups.map((group) => ({
    currencyId: group.currencyId,
    valueRub: group.valueRub,
    sharePercent: group.sharePercent,
    items: group.items.map((item) => ({ name: item.name, valueRub: item.valueRub }))
  })), [
    {
      currencyId: "CNY",
      valueRub: 14_000,
      sharePercent: 100,
      items: [
        { name: "Юаневая облигация", valueRub: 10_000 },
        { name: "Денежные средства CNY", valueRub: 4_000 }
      ]
    },
    {
      currencyId: "USD",
      valueRub: 0,
      sharePercent: 0,
      items: [{ name: "Неоценённая облигация", valueRub: null }]
    }
  ]);
});

test("filters segment detail by the API string names for asset kinds", () => {
  const items = [
    { assetId: "bond", kind: "Position" as const },
    { assetId: "cash", kind: "Cash" as const },
    { assetId: "fund", kind: "MoneyMarketFund" as const }
  ];

  assert.deepEqual(filterPortfolioCurrencyAnalyticsItems(items, "position"), [items[0]]);
  assert.deepEqual(filterPortfolioCurrencyAnalyticsItems(items, "cash"), [items[1]]);
  assert.deepEqual(filterPortfolioCurrencyAnalyticsItems(items, "fund"), [items[2]]);
  assert.deepEqual(filterPortfolioCurrencyAnalyticsItems(items, "all"), items);
});
