import type { BondYieldCashFlow, CashRecommendationBond } from "./cash-investment-recommendations-api";

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

const recommendationNumber = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const recommendationQuantity = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 2 });

export function getRecommendationMobileBondName(bond: { shortName?: string | null; issuerName: string }) {
  return bond.shortName?.trim() || bond.issuerName;
}

function formatRecommendationDate(value: string) {
  return value.split("-").reverse().join(".");
}

export function buildRecommendationMobileDetails(bond: CashRecommendationBond) {
  return [
    { key: "ytm", label: "YTM", value: `${recommendationNumber.format(bond.ytm)}%` },
    {
      key: "yieldDate",
      label: "YTM до",
      value: `${formatRecommendationDate(bond.yieldDate)} (${bond.yieldDateType === "Offer" ? "Оферта" : "Погашение"})`
    },
    { key: "buyPrice", label: "Грязная цена", value: `${recommendationNumber.format(bond.buyPrice)} ${bond.currencyId}` },
    { key: "couponType", label: "Купон", value: bond.couponType === "Floating" ? "Флоатер" : "Фиксированный" },
    { key: "maturityDate", label: "Погашение", value: formatRecommendationDate(bond.maturityDate) },
    { key: "medianDailyVolume", label: "Медианный объём, шт.", value: recommendationQuantity.format(bond.medianDailyVolume) }
  ];
}

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
