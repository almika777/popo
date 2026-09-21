import type { PositionRecommendation, PositionRecommendationReason, PositionRecommendationState } from "./position-recommendations-api";

export function findPositionRecommendation(
  state: PositionRecommendationState | undefined,
  secId: string,
  boardId: string
): PositionRecommendation | undefined {
  return state?.recommendations.find((item) => item.secId === secId && item.boardId === boardId);
}

const reasonLabels: Record<PositionRecommendationReason, string> = {
  MissingData: "Недостаточно данных для расчёта",
  RatingBelowMinimum: "Рейтинг ниже минимального",
  YtmBelowMinimum: "YTM ниже минимальной",
  YtmAboveMaximum: "YTM выше максимальной",
  PositionLiquidityLimitExceeded: "Позиция составляет 10% или больше среднего дневного объёма",
  PositionLiquidityLimitNear: "Позиция составляет от 8% до 10% среднего дневного объёма",
  MaturityOutsideRange: "Срок вне диапазона стратегии",
  OfferTooClose: "Оферта слишком близко",
  InstrumentTypeMismatch: "Тип купона не соответствует стратегии",
  MinimumLiquidityNotMet: "Медианный оборот ниже минимального",
  BetterMarketAlternative: "Сопоставимая бумага доходнее минимум на 0,5 п.п.",
  CurrentYieldAdvantage: "Текущая бумага доходнее сопоставимых минимум на 0,5 п.п.",
  NoMaterialYieldDifference: "Существенной разницы с сопоставимыми бумагами нет",
  NoComparableAlternative: "Сопоставимые бумаги не найдены"
};

export const formatPositionRecommendationReason = (reason: PositionRecommendationReason) => reasonLabels[reason];
