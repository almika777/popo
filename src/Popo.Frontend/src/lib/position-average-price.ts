export type PositionAveragePriceSource = {
  averageBuyPrice: number | null;
  averageBuyPriceAtCurrentFaceValue: number | null;
  isNominalIndexed: boolean;
  hasAmbiguousTradeOrder?: boolean;
};

export type PositionAveragePriceDisplay = {
  value: number | null;
  basis: "actual" | "current-nominal" | "indexed-nominal";
};

export function getPositionAverageBuyPrice(
  position: PositionAveragePriceSource
): PositionAveragePriceDisplay {
  if (position.hasAmbiguousTradeOrder) {
    return { value: null, basis: "actual" };
  }

  if (position.isNominalIndexed) {
    return { value: position.averageBuyPrice, basis: "indexed-nominal" };
  }

  if (position.averageBuyPrice != null) {
    return { value: position.averageBuyPrice, basis: "actual" };
  }

  if (position.averageBuyPriceAtCurrentFaceValue != null) {
    return {
      value: position.averageBuyPriceAtCurrentFaceValue,
      basis: "current-nominal"
    };
  }

  return { value: null, basis: "actual" };
}
