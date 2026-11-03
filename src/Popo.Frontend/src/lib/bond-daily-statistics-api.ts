const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5109/api";

export type BondDailyChange = {
  secId: string;
  boardId: string;
  shortName: string;
  previousPrice: number;
  currentPrice: number;
  changePoints: number;
  changePercent: number;
  emitterId: number | null;
};

export type BondDailyTurnover = {
  secId: string;
  boardId: string;
  shortName: string;
  turnoverRub: number;
  emitterId: number | null;
};

export type BondIssuerCategory = "Government" | "Municipal" | "Corporate";

export type BondDailyStatisticsResult = {
  date: string;
  comparedWithDate: string | null;
  declines: BondDailyChange[];
  increases: BondDailyChange[];
  turnovers: BondDailyTurnover[];
};

export const bondDailyStatisticsApi = {
  async getDailyStatistics(
    date: string,
    minimumTrades: number,
    minimumTurnoverRub: number,
    signal?: AbortSignal,
    issuerCategory?: BondIssuerCategory
  ): Promise<BondDailyStatisticsResult> {
    let response: Response;
    try {
      const query = new URLSearchParams({
        date,
        minimumTrades: String(minimumTrades),
        minimumTurnoverRub: String(minimumTurnoverRub)
      });
      if (issuerCategory) query.set("issuerCategory", issuerCategory);
      response = await fetch(`${apiBaseUrl}/statistics/bonds/daily-movers?${query}`, { signal });
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") throw error;
      throw new Error("Не удалось подключиться к серверу.");
    }

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message && !message.trimStart().startsWith("<") ? message : `Ошибка запроса: ${response.status}`);
    }

    try {
      return await response.json() as BondDailyStatisticsResult;
    } catch {
      throw new Error("Сервер вернул некорректный ответ.");
    }
  }
};
