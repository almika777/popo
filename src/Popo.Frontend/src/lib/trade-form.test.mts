import assert from "node:assert/strict";
import test from "node:test";
import { calculateAccruedInterestTotal } from "./trade-form.ts";

test("unknown accrued interest stays unset instead of becoming zero", () => {
  assert.equal(calculateAccruedInterestTotal(null, 5), undefined);
});

test("known accrued interest is multiplied by the entered quantity", () => {
  assert.equal(calculateAccruedInterestTotal(2.5, 4), 10);
});

test("known zero accrued interest remains an explicit zero", () => {
  assert.equal(calculateAccruedInterestTotal(0, 4), 0);
});
