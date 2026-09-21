import assert from "node:assert/strict";
import test from "node:test";

import { filterPositionTrades, positionDetailsPath } from "./position-details.ts";

test("position details route contains SecId and BoardId", () => {
  assert.equal(positionDetailsPath("RU000A0JXE06", "TQCB"), "/positions/RU000A0JXE06/TQCB");
});

test("position details include trades only from the selected board", () => {
  const trades = [
    { id: "1", secId: "RU000A0JXE06", boardId: "TQCB" },
    { id: "2", secId: "RU000A0JXE06", boardId: "TQOB" },
    { id: "3", secId: "RU000A10F504", boardId: "TQCB" },
  ];

  assert.deepEqual(filterPositionTrades(trades, "RU000A0JXE06", "TQCB").map((trade) => trade.id), ["1"]);
});
