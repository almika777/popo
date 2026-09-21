type TradeIdentity = { secId: string; boardId: string };

export function positionDetailsPath(secId: string, boardId: string) {
  return `/positions/${encodeURIComponent(secId)}/${encodeURIComponent(boardId)}`;
}

export function filterPositionTrades<T extends TradeIdentity>(trades: T[], secId: string, boardId: string) {
  return trades.filter((trade) => trade.secId === secId && trade.boardId === boardId);
}
