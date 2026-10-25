export function getVisibleClosedPositionSessions<T>(rows: readonly T[], page: number, pageSize: number): T[] {
  const safePage = Number.isFinite(page) ? Math.max(1, Math.floor(page)) : 1;
  const safePageSize = Number.isFinite(pageSize) ? Math.max(1, Math.floor(pageSize)) : 1;
  const startIndex = (safePage - 1) * safePageSize;

  return rows.slice(startIndex, startIndex + safePageSize);
}
