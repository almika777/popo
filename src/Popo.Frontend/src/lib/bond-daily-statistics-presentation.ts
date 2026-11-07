import type { Dayjs } from "dayjs";
import type { BondDailyChange } from "./bond-daily-statistics-api";

const priceFormatter = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 4 });
const percentFormatter = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const turnoverFormatter = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

export function formatBondDailyTurnover(turnoverRub: number): string {
  if (turnoverRub < 1_000_000_000) {
    return `${turnoverFormatter.format(turnoverRub / 1_000_000)} млн. руб.`;
  }

  return `${turnoverFormatter.format(turnoverRub / 1_000_000_000)} млрд. руб.`;
}

export function getBondDailyChangeDisplay(
  bond: Pick<BondDailyChange, "changePercent" | "previousPrice" | "currentPrice">
) {
  const isIncrease = bond.changePercent > 0;
  return {
    percent: `${isIncrease ? "+" : "−"}${percentFormatter.format(Math.abs(bond.changePercent))}%`,
    tooltip: `Цена последней сделки: ${priceFormatter.format(bond.previousPrice)} → ${priceFormatter.format(bond.currentPrice)}`,
    isIncrease
  };
}

export function isBondStatisticsDateDisabled(candidate: Dayjs, today: Dayjs): boolean {
  const weekday = candidate.day();
  return candidate.isBefore("2025-01-01", "day")
    || candidate.isAfter(today, "day")
    || weekday === 0
    || weekday === 6;
}

export function getDefaultBondStatisticsDate(today: Dayjs): Dayjs {
  let date = today;
  while (date.day() === 0 || date.day() === 6) {
    date = date.subtract(1, "day");
  }
  return date;
}
