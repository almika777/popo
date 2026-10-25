import assert from "node:assert/strict";
import test from "node:test";
import { getVisibleClosedPositionSessions } from "./closed-position-sessions-mobile.ts";

test("returns closed sessions for the requested mobile page", () => {
  const sessions = ["Первая", "Вторая", "Третья", "Четвёртая"];

  assert.deepEqual(getVisibleClosedPositionSessions(sessions, 2, 2), ["Третья", "Четвёртая"]);
});

test("uses the first page for invalid mobile pagination values", () => {
  const sessions = ["Первая", "Вторая"];

  assert.deepEqual(getVisibleClosedPositionSessions(sessions, 0, 0), ["Первая"]);
});
