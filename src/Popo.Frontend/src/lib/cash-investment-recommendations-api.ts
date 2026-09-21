const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5109/api";

export type CreditRating =
  | "D" | "C" | "CC" | "CCC" | "B_minus" | "B" | "B_plus"
  | "BB_minus" | "BB" | "BB_plus" | "BBB_minus" | "BBB" | "BBB_plus"
  | "A_minus" | "A" | "A_plus" | "AA_minus" | "AA" | "AA_plus"
  | "AAA_minus" | "AAA";

export type BondCouponType = "Fixed" | "Floating";
export type BondInstrumentType = "Any" | "Floating" | "Fixed";
export type BondYieldDateType = "Maturity" | "Offer";
export type BondYieldCashFlowType = "Coupon" | "Amortization" | "Redemption";

export type BondYieldCashFlow = {
  date: string;
  amount: number;
  type: BondYieldCashFlowType;
  isProjected: boolean;
};

export type BondYieldCalculation = {
  settlementDate: string;
  marketPricePercent: number;
  cleanPrice: number;
  accruedInterest: number;
  dirtyPrice: number;
  faceValue: number;
  cashFlows: BondYieldCashFlow[];
};

export type CashRecommendationBond = {
  secId: string;
  boardId: string;
  issuerId: number;
  issuerName: string;
  currencyId: string;
  ytm: number;
  buyPrice: number;
  rating: CreditRating;
  maturityDate: string;
  offerDate: string | null;
  couponType: BondCouponType;
  medianDailyVolume: number;
  yieldDate: string;
  yieldDateType: BondYieldDateType;
  calculation: BondYieldCalculation;
};

export type CashRecommendationResponse = {
  snapshotTime: string;
  bonds: CashRecommendationBond[];
};

export type InvestmentStrategySettings = {
  minimumRating: CreditRating;
  minimumMaturityDays: number | null;
  maximumMaturityDays: number | null;
  minimumMedianDailyVolume: number;
  offerWindowDays: number;
  minimumYtm: number;
  maximumYtm: number;
  instrumentType: BondInstrumentType;
  faceUnit: string | null;
  currencyId: string | null;
};
export type InvestmentStrategyPreset = { id: number; name: string; settings: InvestmentStrategySettings; isActive: boolean };
export type CashRecommendationsPage = { presets: InvestmentStrategyPreset[]; tradingCurrencies: string[]; faceUnits: string[] };

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      cache: "no-store",
      headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) }
    });
  } catch {
    throw new Error("Не удалось подключиться к серверу.");
  }

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message && !message.trimStart().startsWith("<") ? message : `Ошибка запроса: ${response.status}`);
  }

  if (response.status === 202 || response.status === 204) {
    return undefined as T;
  }

  try {
    return await response.json() as T;
  } catch {
    throw new Error("Сервер вернул некорректный ответ.");
  }
}

export const cashInvestmentRecommendationsApi = {
  getPage: () => request<CashRecommendationsPage>("/recommendations/cash/page"),
  getCurrent: () => request<CashRecommendationResponse>("/recommendations/cash"),
  getSettings: () => request<InvestmentStrategySettings>("/recommendations/cash/settings"),
  updateSettings: (settings: InvestmentStrategySettings) =>
    request<InvestmentStrategySettings>("/recommendations/cash/settings", {
      method: "PUT",
      body: JSON.stringify(settings)
    }),
  getPresets: () => request<InvestmentStrategyPreset[]>("/recommendations/cash/presets"),
  createPreset: (name: string, settings: InvestmentStrategySettings) => request<InvestmentStrategyPreset>("/recommendations/cash/presets", { method: "POST", body: JSON.stringify({ name, settings }) }),
  updatePreset: (id: number, name: string, settings: InvestmentStrategySettings) => request<InvestmentStrategyPreset>(`/recommendations/cash/presets/${id}`, { method: "PUT", body: JSON.stringify({ name, settings }) }),
  deletePreset: (id: number) => request<void>(`/recommendations/cash/presets/${id}`, { method: "DELETE" }),
  activatePreset: (id: number) => request<void>(`/recommendations/cash/presets/${id}/activate`, { method: "POST" }),
  recalculate: () => request<CashRecommendationResponse>("/recommendations/cash/recalculate", { method: "POST" })
};
