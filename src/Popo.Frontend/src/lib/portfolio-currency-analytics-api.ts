const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5109/api";

export type PortfolioCurrencyAnalyticsCashBalance = {
  currencyId: string;
  amount: number;
};

export type PortfolioCurrencyAnalyticsManualPrice = {
  assetId: string;
  price: number;
};

export type PortfolioCurrencyAnalyticsManualRate = {
  currencyId: string;
  rateToRub: number;
};

export type PortfolioCurrencyAnalyticsRequest = {
  cashBalances: PortfolioCurrencyAnalyticsCashBalance[];
  manualPrices: PortfolioCurrencyAnalyticsManualPrice[];
  manualRates: PortfolioCurrencyAnalyticsManualRate[];
};

export type PortfolioCurrencyAnalyticsSlice = {
  currencyId: string;
  valueRub: number;
  share: number;
};

export type PortfolioCurrencyAnalyticsUnvaluedAsset = {
  assetId: string;
  kind: 0 | 1 | 2;
  name: string;
  currencyId: string;
  rateCurrencyId: string;
  missingPrice: boolean;
  missingRate: boolean;
};

export type PortfolioCurrencyAnalyticsResponse = {
  asOf: string;
  totalValueRub: number;
  availableCurrencies: string[];
  slices: PortfolioCurrencyAnalyticsSlice[];
  unvaluedAssets: PortfolioCurrencyAnalyticsUnvaluedAsset[];
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) }
    });
  } catch {
    throw new Error("Не удалось подключиться к серверу.");
  }
  if (!response.ok) {
    const message = await response.text();
    throw new Error(message && !message.trimStart().startsWith("<") ? message : `Ошибка запроса: ${response.status}`);
  }
  try {
    return await response.json() as T;
  } catch {
    throw new Error("Сервер вернул некорректный ответ.");
  }
}

export const portfolioCurrencyAnalyticsApi = {
  calculate: (payload: PortfolioCurrencyAnalyticsRequest) =>
    request<PortfolioCurrencyAnalyticsResponse>("/portfolio/analytics/currency", {
      method: "POST",
      body: JSON.stringify(payload)
    })
};
