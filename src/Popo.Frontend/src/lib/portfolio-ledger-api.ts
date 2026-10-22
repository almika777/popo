const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5109/api";

export type TradeSide = "Buy" | "Sell";
export const MIN_BOND_SEARCH_LENGTH = 3;
export type BondSearchResult = { secId: string; boardId: string; currency: string; shortName: string; accruedInterest: number | null; faceValue: number };
export type PortfolioTrade = { id: string; secId: string; boardId: string; currencyId: string; tradeDate: string; side: TradeSide; quantity: number; price: number; faceValue: number; accruedInterest: number; commission: number; commissionPercent: number; amount: number; shortName: string };
export type PortfolioPosition = { secId: string; boardId: string; currencyId: string; quantity: number; boughtQuantity: number; soldQuantity: number; boughtAmount: number; soldAmount: number; averageBuyPrice: number | null; averageBuyPriceAtCurrentFaceValue: number | null; averageBuyPricePercent: number | null; currentFaceValue: number | null; faceUnit: string | null; marketPricePercent: number | null; marketPrice: number | null; marketValue: number | null; unrealizedPnl: number | null; unrealizedPnlPercent: number | null; approximateCouponIncome: number | null; approximateTotalPnl: number | null; approximateTotalPnlPercent: number | null; approximateAnnualizedTotalPnlPercent: number | null; shortName: string; averageDailyVolume: number | null; medianDailyVolume: number | null; isNominalIndexed: boolean };
export type PortfolioSummary = { asOf: string; totalValueRub: number; positionsValueRub: number; moneyMarketFundsValueRub: number };
export type PositionTotals = { marketValueRub: number; investedAmountRub: number | null; unrealizedPnlRub: number | null; unrealizedPnlPercent: number | null };
export type PositionsPage = { positions: PortfolioPosition[]; totals: PositionTotals };
export type MoneyMarketFundOption = { secId: string; boardId: string; name: string };
export type MoneyMarketFund = { id: string; secId: string; boardId: string; quantity: number; averagePrice: number; currentPrice: number | null; currentValue: number | null; pnl: number | null; pnlPercent: number | null; quoteTime: string | null };
export type MoneyMarketFundsPage = { moneyMarketFunds: MoneyMarketFund[]; moneyMarketFundOptions: MoneyMarketFundOption[] };
type TradePayload = Omit<PortfolioTrade, "id" | "amount" | "commission" | "accruedInterest" | "shortName"> & { accruedInterestTotal: number };

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) } });
  } catch {
    throw new Error("Не удалось подключиться к серверу.");
  }
  if (!response.ok) {
    const message = await response.text();
    throw new Error(message && !message.trimStart().startsWith("<") ? message : `Ошибка запроса: ${response.status}`);
  }
  if (response.status === 204) return undefined as T;
  try {
    return await response.json() as T;
  } catch {
    throw new Error("Сервер вернул некорректный ответ.");
  }
}

export const portfolioLedgerApi = {
  searchBonds: (query: string) => {
    const normalizedQuery = query.trim();
    if (normalizedQuery.length < MIN_BOND_SEARCH_LENGTH) return Promise.resolve<BondSearchResult[]>([]);
    return request<BondSearchResult[]>(`/bonds/search?q=${encodeURIComponent(normalizedQuery)}`);
  },
  getTrades: () => request<PortfolioTrade[]>("/trades"),
  saveTrade: (payload: TradePayload, id?: string) => request<PortfolioTrade>(id ? `/trades/${id}` : "/trades", { method: id ? "PUT" : "POST", body: JSON.stringify(payload) }),
  deleteTrade: (id: string) => request<void>(`/trades/${id}`, { method: "DELETE" }),
  getPositionsPage: () => request<PositionsPage>("/positions"),
  getPositions: async () => (await request<PositionsPage>("/positions")).positions,
  getMoneyMarketFundsPage: () => request<MoneyMarketFundsPage>("/liquidity/money-market-funds/page"),
  addMoneyMarketFund: (payload: { secId: string; quantity: number; averagePrice: number }) => request<MoneyMarketFund>("/liquidity/money-market-funds", { method: "POST", body: JSON.stringify(payload) }),
  updateMoneyMarketFund: (id: string, payload: { secId: string; quantity: number; averagePrice: number }) => request<MoneyMarketFund>(`/liquidity/money-market-funds/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteMoneyMarketFund: (id: string) => request<void>(`/liquidity/money-market-funds/${id}`, { method: "DELETE" }),
};
