export type PositionRecommendationAction = "Buy" | "Hold" | "Sell" | "Unavailable";
export type PositionRecommendationStatus = "Calculating" | "Ready" | "Stale" | "Unavailable";
export type PositionRecommendationReason =
  | "MissingData"
  | "RatingBelowMinimum"
  | "YtmBelowMinimum"
  | "YtmAboveMaximum"
  | "PositionLiquidityLimitExceeded"
  | "PositionLiquidityLimitNear"
  | "MaturityOutsideRange"
  | "OfferTooClose"
  | "InstrumentTypeMismatch"
  | "MinimumLiquidityNotMet"
  | "BetterMarketAlternative"
  | "CurrentYieldAdvantage"
  | "NoMaterialYieldDifference"
  | "NoComparableAlternative";

export type PositionRecommendation = {
  secId: string;
  boardId: string;
  action: PositionRecommendationAction;
  quantity: number;
  ytm: number | null;
  calculationPrice: number | null;
  rating: string | null;
  averageDailyVolume: number | null;
  positionVolumeShare: number | null;
  alternative: {
    secId: string;
    boardId: string;
    shortName: string | null;
    ytm: number;
    calculationPrice: number;
    rating: string;
    yieldDate: string;
  } | null;
  reasons: PositionRecommendationReason[];
};

export type PositionRecommendationState = {
  status: PositionRecommendationStatus;
  calculationStartedAt: string | null;
  lastSuccessfulAt: string | null;
  snapshotTime: string | null;
  error: string | null;
  recommendations: PositionRecommendation[];
};
