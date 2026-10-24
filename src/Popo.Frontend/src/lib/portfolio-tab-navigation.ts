export type HorizontalBounds = {
  left: number;
  right: number;
};

export function getHorizontalScrollAdjustment(
  container: HorizontalBounds,
  element: HorizontalBounds
): number {
  if (element.left < container.left) {
    return element.left - container.left;
  }
  if (element.right > container.right) {
    return element.right - container.right;
  }
  return 0;
}
