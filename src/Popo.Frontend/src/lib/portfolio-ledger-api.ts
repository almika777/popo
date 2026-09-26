import type { PositionRecommendationState } from "./position-recommendations-api";

const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5109/api";

export type TradeSide = "Buy" | "Sell";
export type BondSearchResult = { secId: string; boardId: string; currency: string; shortName: string; accruedInterest: number; faceValue: number };
export type PortfolioTrade = { id: string; secId: string; boardId: string; currencyId: string; tradeDate: string; side: TradeSide; quantity: number; price: number; faceValue: number; accruedInterest: number; commission: number; commissionPercent: number; amount: number; shortName: string };
export type PortfolioPosition = { secId: string; boardId: string; currencyId: string; quantity: number; boughtQuantity: number; soldQuantity: number; boughtAmount: number; soldAmount: number; averageBuyPrice: number; averageBuyPriceAtCurrentFaceValue: number | null; averageBuyPricePercent: number | null; currentFaceValue: number | null; faceUnit: string | null; marketPricePercent: number | null; marketPrice: number | null; marketValue: number | null; unrealizedPnl: number | null; unrealizedPnlPercent: number | null; approximateCouponIncome: number | null; approximateTotalPnl: number | null; approximateTotalPnlPercent: number | null; shortName: string; averageDailyVolume: number | null; medianDailyVolume: number | null };
export type CashSnapshot = { id: string; currencyId: string; snapshotDate: string; amount: number; comment: string };
export type CashBalance = { currencyId: string; amount: number };
export type PortfolioSummary = { asOf: string; totalValueRub: number; positionsValueRub: number; cashValueRub: number; moneyMarketFundsValueRub: number; unrealizedPnlRub: number };
export type PositionTotals = { marketValueRub: number; investedAmountRub: number; unrealizedPnlRub: number; unrealizedPnlPercent: number };
export type PositionsPage = { positions: PortfolioPosition[]; totals: PositionTotals; recommendation: PositionRecommendationState };
export type MoneyMarketFundOption = { secId: string; boardId: string; name: string };
export type MoneyMarketFund = { id: string; secId: string; boardId: string; quantity: number; averagePrice: number; currentPrice: number | null; currentValue: number | null; pnl: number | null; pnlPercent: number | null; quoteTime: string | null };
export type MoneyMarketFundOperation = { id: string; secId: string; date: string; side: TradeSide; quantity: number; price: number; commission: number; amount: number };
export type CashPage = { snapshots: CashSnapshot[]; balances: CashBalance[]; currencies: string[]; moneyMarketFunds: MoneyMarketFund[]; moneyMarketFundOptions: MoneyMarketFundOption[]; moneyMarketFundOperations: MoneyMarketFundOperation[] };
export type BrokerReportOperationKind = "BondTrade" | "FundOperation" | "Deposit" | "Withdrawal";
export type BrokerReportOperation = { id: string; kind: BrokerReportOperationKind; date: string; description: string; secId: string | null; boardId: string | null; currencyId: string | null; side: TradeSide | null; quantity: number | null; unitPrice: number | null; faceValue: number | null; accruedInterestTotal: number | null; commission: number | null; amount: number | null; canImport: boolean; warning: string | null };
export type BrokerReportImportResult = { tradesAdded: number; fundOperationsAdded: number; cashFlowsAdded: number };

type TradePayload = Omit<PortfolioTrade, "id" | "amount" | "commission" | "accruedInterest" | "shortName"> & { accruedInterestTotal: number };
type CashSnapshotPayload = Omit<CashSnapshot, "id">;

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

async function uploadPdf<T>(path: string, file: File): Promise<T> {
  const formData = new FormData();
  formData.append("file", file);
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, { method: "POST", body: formData });
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

export const portfolioLedgerApi = {
  searchBonds: (query: string) => request<BondSearchResult[]>(`/bonds/search?q=${encodeURIComponent(query)}`),
  getTrades: () => request<PortfolioTrade[]>("/trades"),
  saveTrade: (payload: TradePayload, id?: string) => request<PortfolioTrade>(id ? `/trades/${id}` : "/trades", { method: id ? "PUT" : "POST", body: JSON.stringify(payload) }),
  deleteTrade: (id: string) => request<void>(`/trades/${id}`, { method: "DELETE" }),
  getPositionsPage: () => request<PositionsPage>("/positions"),
  getPositions: async () => (await request<PositionsPage>("/positions")).positions,
  getCashPage: (asOf: string) => request<CashPage>(`/cash/page?asOf=${asOf}`),
  addCashSnapshot: (payload: CashSnapshotPayload) => request<CashSnapshot>("/cash/snapshots", { method: "POST", body: JSON.stringify(payload) }),
  updateCashSnapshot: (id: string, payload: CashSnapshotPayload) => request<CashSnapshot>(`/cash/snapshots/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteCashSnapshot: (id: string) => request<void>(`/cash/snapshots/${id}`, { method: "DELETE" }),
  addMoneyMarketFund: (payload: { secId: string; quantity: number; averagePrice: number }) => request<MoneyMarketFund>("/cash/money-market-funds", { method: "POST", body: JSON.stringify(payload) }),
  updateMoneyMarketFund: (id: string, payload: { secId: string; quantity: number; averagePrice: number }) => request<MoneyMarketFund>(`/cash/money-market-funds/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteMoneyMarketFund: (id: string) => request<void>(`/cash/money-market-funds/${id}`, { method: "DELETE" }),
  addMoneyMarketFundOperation: (payload: Omit<MoneyMarketFundOperation, "id" | "amount">) => request<MoneyMarketFundOperation>("/cash/money-market-fund-operations", { method: "POST", body: JSON.stringify(payload) }),
  previewBrokerReport: (file: File) => uploadPdf<{ operations: BrokerReportOperation[]; duplicatesHidden: number; outsideHistoryHidden: number; unsupportedOperationsHidden: number }>("/broker-reports/preview", file),
  importBrokerReportOperations: (operations: Omit<BrokerReportOperation, "description" | "canImport" | "warning">[]) => request<BrokerReportImportResult>("/broker-reports/import", { method: "POST", body: JSON.stringify({ operations }) })
};
