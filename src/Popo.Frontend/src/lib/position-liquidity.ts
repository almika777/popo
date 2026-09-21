export type PositionLiquidityLevel = "low" | "medium" | "high";

export function getPositionLiquidityLevel(
  quantity: number,
  averageDailyVolume: number | null
): PositionLiquidityLevel | null {
  if (averageDailyVolume == null) return null;
  if (quantity < averageDailyVolume * 0.08) return "low";
  if (quantity < averageDailyVolume * 0.1) return "medium";
  return "high";
}
