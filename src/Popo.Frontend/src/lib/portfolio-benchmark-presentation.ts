import type { Dayjs } from "dayjs";

export function isBenchmarkStartDateDisabled(candidate: Dayjs, end: Dayjs): boolean {
  return !candidate.isBefore(end, "day");
}

export function isBenchmarkEndDateDisabled(candidate: Dayjs, start: Dayjs): boolean {
  return !candidate.isAfter(start, "day");
}

export function getPortfolioComparisonUnavailableHint(reason: string | null): string | null {
  return reason ? "Портфель не рассчитан; сравнение с ним недоступно." : null;
}
