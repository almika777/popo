export type PositionTotalsSource = {
  positionsValueRub: number;
  unrealizedPnlRub: number;
};

export type PositionTotals = {
  marketValue: number;
  investedAmount: number;
  unrealizedPnl: number;
  unrealizedPnlPercent: number;
};

export function calculatePositionTotals(source: PositionTotalsSource): PositionTotals {
  const investedAmount = source.positionsValueRub - source.unrealizedPnlRub;

  return {
    marketValue: source.positionsValueRub,
    investedAmount,
    unrealizedPnl: source.unrealizedPnlRub,
    unrealizedPnlPercent: investedAmount === 0 ? 0 : source.unrealizedPnlRub / investedAmount * 100
  };
}
