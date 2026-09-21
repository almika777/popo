export const recommendationColumnTitles = [
  "Облигация",
  "Эмитент",
  "YTM",
  "YTM до",
  "Грязная цена",
  "Купон",
  "Погашение",
  "Медианный объём, шт."
] as const;

export const recommendationYtmActionLabel = "Нажмите, чтобы открыть расчёт YTM";

const cashFlowTypeLabels: Record<BondYieldCashFlow["type"], string> = {
  Coupon: "Купон",
  Amortization: "Амортизация",
  Redemption: "Погашение"
};

export function buildYieldCashFlowRows(cashFlows: BondYieldCashFlow[]) {
  return cashFlows.map((cashFlow, index) => ({
    key: `${cashFlow.date}-${cashFlow.type}-${index}`,
    date: cashFlow.date,
    amount: cashFlow.amount,
    typeLabel: cashFlowTypeLabels[cashFlow.type],
    isProjected: cashFlow.isProjected
  }));
}
import type { BondYieldCashFlow } from "./cash-investment-recommendations-api";
