export function calculateAccruedInterestTotal(
  accruedInterest: number | null | undefined,
  quantity: number | null | undefined
): number | undefined {
  if (accruedInterest == null || quantity == null || quantity <= 0) return undefined;
  return accruedInterest * quantity;
}
