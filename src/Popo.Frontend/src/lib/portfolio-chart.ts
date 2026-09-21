export type PortfolioChartPoint = {
  id: string;
  date: string;
  totalValue: number;
  comment: string;
};

export type PortfolioChartCashFlow = {
  date: string;
  type: "Deposit" | "Withdrawal" | "Tax";
  amount: number;
};

export function buildRoundedScale(values: number[]): { min: number; max: number; ticks: number[] } {
  const rawMin = Math.min(0, ...values);
  const rawMax = Math.max(0, ...values);
  const largestMagnitude = Math.max(Math.abs(rawMin), Math.abs(rawMax), 1);
  const magnitude = 10 ** Math.floor(Math.log10(largestMagnitude / 4));
  const normalizedStep = largestMagnitude / 4 / magnitude;
  const niceStep = (normalizedStep <= 1 ? 1 : normalizedStep <= 2 ? 2 : normalizedStep <= 2.5 ? 2.5 : normalizedStep <= 5 ? 5 : 10) * magnitude;
  const min = Math.floor(rawMin / niceStep) * niceStep;
  const max = Math.ceil(rawMax / niceStep) * niceStep;
  const ticks = Array.from({ length: Math.round((max - min) / niceStep) + 1 }, (_, index) => min + index * niceStep);
  return { min, max, ticks };
}

export function buildYearEndForecast(startDate: string, lastDate: string, currentChange: number): PortfolioChartPoint {
  const start = new Date(`${startDate}T00:00:00Z`);
  const last = new Date(`${lastDate}T00:00:00Z`);
  const end = new Date(Date.UTC(last.getUTCFullYear(), 11, 31));
  const elapsedDays = Math.max((last.getTime() - start.getTime()) / 86_400_000, 1);
  const yearDays = (end.getTime() - start.getTime()) / 86_400_000;
  return {
    id: "forecast-year-end",
    date: `${last.getUTCFullYear()}-12-31`,
    totalValue: currentChange / elapsedDays * yearDays,
    comment: "Прогноз по среднему изменению с начала года"
  };
}

export function buildCashFlowAdjustedValuations<T extends PortfolioChartPoint>(
  valuations: T[],
  cashFlows: PortfolioChartCashFlow[]
): T[] {
  if (valuations.length < 2) {
    return valuations;
  }

  const cashDeltaByDate = new Map<string, number>();
  for (const cashFlow of cashFlows) {
    const cashDelta = cashFlow.type === "Deposit" ? cashFlow.amount : -cashFlow.amount;
    cashDeltaByDate.set(cashFlow.date, (cashDeltaByDate.get(cashFlow.date) ?? 0) + cashDelta);
  }

  const cashDeltaByInterval = new Array<number>(valuations.length).fill(0);
  for (const [flowDate, cashDelta] of cashDeltaByDate) {
    const firstIndexOnOrAfterFlow = valuations.findIndex((valuation) => valuation.date >= flowDate);
    if (firstIndexOnOrAfterFlow <= 0) {
      continue;
    }

    const candidateIndexes = [firstIndexOnOrAfterFlow];
    if (
      valuations[firstIndexOnOrAfterFlow].date === flowDate
      && firstIndexOnOrAfterFlow + 1 < valuations.length
    ) {
      candidateIndexes.push(firstIndexOnOrAfterFlow + 1);
    }

    const effectiveIndex = candidateIndexes.reduce((bestIndex, candidateIndex) => {
      const candidateChange = valuations[candidateIndex].totalValue - valuations[candidateIndex - 1].totalValue;
      const bestChange = valuations[bestIndex].totalValue - valuations[bestIndex - 1].totalValue;
      return Math.abs(candidateChange - cashDelta) < Math.abs(bestChange - cashDelta)
        ? candidateIndex
        : bestIndex;
    });
    cashDeltaByInterval[effectiveIndex] += cashDelta;
  }

  let adjustedValue = valuations[0].totalValue;
  return valuations.map((valuation, index) => {
    if (index > 0) {
      const rawChange = valuation.totalValue - valuations[index - 1].totalValue;
      adjustedValue += rawChange - cashDeltaByInterval[index];
    }

    return { ...valuation, totalValue: adjustedValue };
  });
}

export function toRelativePortfolioChanges<T extends PortfolioChartPoint>(rows: T[]): T[] {
  const baseValue = rows[0]?.totalValue;
  if (baseValue === undefined) {
    return rows;
  }

  return rows.map((row) => ({ ...row, totalValue: row.totalValue - baseValue }));
}
