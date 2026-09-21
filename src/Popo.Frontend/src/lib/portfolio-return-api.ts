import type { CashBalance, PortfolioSummary } from "./portfolio-ledger-api";

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

export type PortfolioPage = {
  valuations: PortfolioValuation[];
  cashFlows: PortfolioCashFlow[];
  balances: CashBalance[];
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
    request<PortfolioReturn>(`/portfolio/return?from=${from}&to=${to}`)
};
