const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5109/api";

export type ClosedPositionSession = {
  id: string;
  secId: string;
  boardId: string;
  currencyId: string;
  shortName: string;
  startDate: string;
  endDate: string;
  holdingDays: number;
  boughtQuantity: number;
  boughtCleanAmount: number;
  soldCleanAmount: number;
  commission: number;
  amortizationIncome: number | null;
  bodyPnl: number | null;
  couponIncome: number | null;
  totalPnl: number | null;
  totalPnlPercent: number | null;
  annualizedPnlPercent: number | null;
  isComplete: boolean;
  incompleteReason: string | null;
  calculatorKind: string;
  calculatedAt: string;
};

export type ClosedPositionRecalculation = { jobId: string; state: string };

async function request<T>(path: string, signal: AbortSignal, method = "GET"): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/closed-position-sessions${path}`, {
      method,
      signal,
      cache: "no-store"
    });
  } catch (error) {
    if (signal.aborted) throw error;
    throw new Error("Не удалось подключиться к серверу.");
  }

  if (!response.ok) {
    let detail: string | undefined;
    if (response.status < 500) {
      try {
        const body = await response.text();
        let problem: unknown;
        try { problem = JSON.parse(body); } catch { problem = body; }
        if (typeof problem === "string" && problem.length <= 500 && !/[<>]/.test(problem)) {
          detail = problem;
        } else if (typeof problem === "object" && problem !== null && "detail" in problem
          && typeof problem.detail === "string" && problem.detail.length <= 500
          && !/[<>]/.test(problem.detail)) {
          detail = problem.detail;
        }
      } catch {
        // Не выводим HTML, произвольный текст ответа или внутренние ошибки сервера.
      }
    }
    throw new Error(detail || `Не удалось выполнить запрос (${response.status}).`);
  }

  try {
    return await response.json() as T;
  } catch (error) {
    if (signal.aborted) throw error;
    throw new Error("Сервер вернул некорректный ответ.");
  }
}

export const closedPositionSessionsApi = {
  getAll: (signal: AbortSignal) => request<ClosedPositionSession[]>("", signal),
  recalculate: (signal: AbortSignal) => request<{ jobId: string }>("/recalculate", signal, "POST"),
  getRecalculation: (jobId: string, signal: AbortSignal) =>
    request<ClosedPositionRecalculation>(`/recalculation/${encodeURIComponent(jobId)}`, signal)
};
