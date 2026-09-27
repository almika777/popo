export type PositionAveragePriceSource = {
  averageBuyPrice: number | null;
  averageBuyPriceAtCurrentFaceValue: number | null;
  isNominalIndexed: boolean;
};

export type PositionAveragePriceDisplay = {
  value: number | null;
  basis: "actual" | "current-nominal" | "indexed-nominal";
};

export function getPositionAverageBuyPrice(
  position: PositionAveragePriceSource
): PositionAveragePriceDisplay {
  if (position.isNominalIndexed) {
    return { value: position.averageBuyPrice, basis: "indexed-nominal" };
  }

  if (position.averageBuyPriceAtCurrentFaceValue != null) {
    return {
      value: position.averageBuyPriceAtCurrentFaceValue,
      basis: "current-nominal"
    };
  }

  return { value: position.averageBuyPrice, basis: "actual" };
}
