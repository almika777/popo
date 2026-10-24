import type { PortfolioSummary } from "./portfolio-ledger-api";

const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5109/api";

export type CashFlowType = "Deposit" | "Withdrawal" | "Tax";

export type PortfolioValuation = {
  id: string;
  date: string;
  totalValue: number;
  comment: string;
};

export type PortfolioCashFlow = {
  id: string;
  date: string;
  type: CashFlowType;
  amount: number;
  comment: string;
};

export type PortfolioReturn = {
  from: string;
  to: string;
  startValue: number;
  endValue: number;
  deposits: number;
  withdrawals: number;
  taxes: number;
  absoluteReturn: number;
  periodReturn: number;
  annualizedReturn: number;
  periodDays: number;
};

export type PortfolioBenchmarkPoint = {
  date: string;
  returnPercent: number;
  profitPercent?: number | null;
};

export type PortfolioBenchmarkResult = {
  from: string;
  to: string;
  portfolioReturn: number | null;
  lqdtReturn: number;
  excessReturn: number | null;
  lqdtStartObservationDate: string;
  lqdtEndObservationDate: string;
  portfolioPoints: PortfolioBenchmarkPoint[];
  lqdtPoints: PortfolioBenchmarkPoint[];
  indexedBondReturn?: number | null;
  indexedBondStartObservationDate?: string | null;
  indexedBondEndObservationDate?: string | null;
  indexedBondPoints?: PortfolioBenchmarkPoint[] | null;
  indexedBondUnavailableReason?: string | null;
  indexedBondComparisonStartDate?: string | null;
  portfolioProfitPercent?: number | null;
  lqdtProfitPercent?: number | null;
  indexedBondProfitPercent?: number | null;
  tmonReturn: number | null;
  tmonStartObservationDate: string | null;
  tmonEndObservationDate: string | null;
  tmonPoints: PortfolioBenchmarkPoint[];
  tmonUnavailableReason: string | null;
  tmonProfitPercent: number | null;
  portfolioUnavailableReason: string | null;
  comparisonStartDate: string | null;
};

const isFiniteBenchmarkPercent = (value: number | null | undefined): value is number =>
  value != null && Number.isFinite(value);

export function hasCurrentBenchmarkContract(result: PortfolioBenchmarkResult): boolean {
  const hasOwn = (value: object, key: PropertyKey) => Object.prototype.hasOwnProperty.call(value, key);
  const hasTmonContract = hasOwn(result, "tmonReturn") &&
    hasOwn(result, "tmonProfitPercent") &&
    hasOwn(result, "tmonPoints") && Array.isArray(result.tmonPoints) &&
    hasOwn(result, "tmonUnavailableReason") &&
    hasOwn(result, "tmonStartObservationDate") &&
    hasOwn(result, "tmonEndObservationDate");
  if (!hasTmonContract) {
    return false;
  }

  const hasPointMetrics = (points: PortfolioBenchmarkResult["portfolioPoints"]) =>
    points.every((point) => hasOwn(point, "profitPercent") && isFiniteBenchmarkPercent(point.profitPercent));
  const hasBondSeries = (result.indexedBondPoints?.length ?? 0) > 0;
  const hasBondTotals = isFiniteBenchmarkPercent(result.indexedBondProfitPercent);
  const bondSeriesIsConsistent = hasBondSeries
    ? hasBondTotals && hasPointMetrics(result.indexedBondPoints ?? [])
    : result.indexedBondProfitPercent == null;
  const hasTmonSeries = result.tmonPoints.length > 0;
  const tmonSeriesIsConsistent = hasTmonSeries
    ? isFiniteBenchmarkPercent(result.tmonReturn) &&
      isFiniteBenchmarkPercent(result.tmonProfitPercent) &&
      Math.abs(result.tmonReturn - result.tmonProfitPercent) < 0.000001 &&
      Math.abs(result.tmonPoints.at(-1)!.profitPercent! - result.tmonProfitPercent) < 0.000001 &&
      hasPointMetrics(result.tmonPoints)
    : result.tmonReturn == null && result.tmonProfitPercent == null;
  const portfolioAvailable = isFiniteBenchmarkPercent(result.portfolioReturn) &&
    isFiniteBenchmarkPercent(result.portfolioProfitPercent) &&
    hasPointMetrics(result.portfolioPoints) &&
    result.portfolioUnavailableReason === null;
  const portfolioUnavailable = result.portfolioReturn === null && result.portfolioProfitPercent === null &&
    result.portfolioPoints.length === 0 &&
    typeof result.portfolioUnavailableReason === "string" && result.portfolioUnavailableReason.length > 0;

  return hasOwn(result, "portfolioUnavailableReason") &&
    hasOwn(result, "portfolioReturn") &&
    hasOwn(result, "comparisonStartDate") &&
    (result.comparisonStartDate === null || typeof result.comparisonStartDate === "string") &&
    hasOwn(result, "portfolioProfitPercent") && hasOwn(result, "lqdtProfitPercent") &&
    hasOwn(result, "indexedBondProfitPercent") && hasOwn(result, "tmonReturn") &&
    hasOwn(result, "tmonProfitPercent") && hasOwn(result, "tmonPoints") &&
    hasOwn(result, "tmonUnavailableReason") && hasOwn(result, "tmonStartObservationDate") &&
    hasOwn(result, "tmonEndObservationDate") &&
    (result.tmonUnavailableReason === null || typeof result.tmonUnavailableReason === "string") &&
    (result.tmonStartObservationDate === null || typeof result.tmonStartObservationDate === "string") &&
    (result.tmonEndObservationDate === null || typeof result.tmonEndObservationDate === "string") &&
    (portfolioAvailable || portfolioUnavailable) &&
    isFiniteBenchmarkPercent(result.lqdtProfitPercent) &&
    hasPointMetrics(result.lqdtPoints) &&
    bondSeriesIsConsistent && tmonSeriesIsConsistent;
}

export type PortfolioPage = {
  valuations: PortfolioValuation[];
  cashFlows: PortfolioCashFlow[];
  summary: PortfolioSummary;
};

type ValuationPayload = {
  date: string;
  totalValue: number;
  comment: string;
};

type CashFlowPayload = {
  date: string;
  type: CashFlowType;
  amount: number;
  comment: string;
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
  if (response.status === 204) {
    return undefined as T;
  }
  try {
    return await response.json() as T;
  } catch {
    throw new Error("Сервер вернул некорректный ответ.");
  }
}

export const portfolioReturnApi = {
  getPage: (asOf: string) => request<PortfolioPage>(`/portfolio?asOf=${asOf}`),
  saveValuation: (payload: ValuationPayload, id?: string) =>
    request<PortfolioValuation>(id ? `/portfolio/valuations/${id}` : "/portfolio/valuations", {
      method: id ? "PUT" : "POST",
      body: JSON.stringify(payload)
    }),
  deleteValuation: (id: string) => request<void>(`/portfolio/valuations/${id}`, { method: "DELETE" }),
  saveCashFlow: (payload: CashFlowPayload, id?: string) =>
    request<PortfolioCashFlow>(id ? `/portfolio/cash-flows/${id}` : "/portfolio/cash-flows", {
      method: id ? "PUT" : "POST",
      body: JSON.stringify(payload)
    }),
  deleteCashFlow: (id: string) => request<void>(`/portfolio/cash-flows/${id}`, { method: "DELETE" }),
  calculate: (from: string, to: string) =>
    request<PortfolioReturn>(`/portfolio/return?from=${from}&to=${to}`),
  calculateBenchmark: (from: string, to: string, includeLqdt = true, includeIndexedBond = true, includeTmon = true) =>
    request<PortfolioBenchmarkResult>(
      `/portfolio/benchmark?from=${from}&to=${to}&includeLqdt=${includeLqdt}&includeIndexedBond=${includeIndexedBond}&includeTmon=${includeTmon}`
    )
};
