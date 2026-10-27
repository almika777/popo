import type { PositionAnnualizedReturnBreakdown } from "./portfolio-ledger-api.ts";

const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 6 });
const percent = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const quantity = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 4 });
const date = new Intl.DateTimeFormat("ru-RU", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  timeZone: "UTC"
});

export type PositionAnnualizedReturnBreakdownView = {
  valuationDate: string;
  formula: string;
  lots: {
    tradeDate: string;
    quantity: string;
    purchaseCleanPrice: string;
    paidAccruedInterest: string;
    commission: string;
    investedCapital: string;
    holdingDays: string;
    capitalDays: string;
  }[];
};

export function buildPositionAnnualizedBreakdownView(
  breakdown: PositionAnnualizedReturnBreakdown | null | undefined,
  currencyId: string
): PositionAnnualizedReturnBreakdownView | null {
  if (breakdown == null) return null;

  const money = (value: number) => `${number.format(value)} ${currencyId}`;
  return {
    valuationDate: date.format(new Date(`${breakdown.valuationDate}T00:00:00.000Z`)),
    formula: `365 × ${money(breakdown.totalPnl)} ÷ ${number.format(breakdown.investedCapitalDays)} ${currencyId}·дней × 100 ≈ ${percent.format(breakdown.annualizedPercent)}%`,
    lots: breakdown.lots.map((lot) => ({
      tradeDate: date.format(new Date(`${lot.tradeDate}T00:00:00.000Z`)),
      quantity: quantity.format(lot.quantity),
      purchaseCleanPrice: money(lot.purchaseCleanPricePerUnit),
      paidAccruedInterest: money(lot.paidAccruedInterestPerUnit),
      commission: money(lot.commissionPerUnit),
      investedCapital: money(lot.investedCapital),
      holdingDays: `${quantity.format(lot.holdingDays)} дн.`,
      capitalDays: `${number.format(lot.capitalDays)} ${currencyId}·дней`
    }))
  };
}
