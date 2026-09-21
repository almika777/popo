import assert from "node:assert/strict";
import test from "node:test";

import {
  daysToYears,
  fromStrategyFormValues,
  toStrategyFormValues,
  type StrategyFormValues
} from "./cash-investment-recommendations-form.ts";

test("maps API values to form values converting maturity days to years", () => {
  const values = toStrategyFormValues({
    minimumRating: "AA_minus",
    minimumMaturityDays: 183,
    maximumMaturityDays: 730,
    minimumMedianDailyVolume: 1_000,
    minimumYtm: 15,
    maximumYtm: 30,
    offerWindowDays: 90,
    instrumentType: "Fixed",
    faceUnit: "USD",
    currencyId: "RUB"
  });

  assert.equal(values.minimumMedianDailyVolume, 1_000);
  assert.equal(values.minimumYtmPercent, 15);
  assert.equal(values.maximumYtmPercent, 30);
  assert.equal(values.instrumentType, "Fixed");
  assert.equal(values.faceUnit, "USD");
  assert.equal(values.currencyId, "RUB");
  assert.ok(Math.abs(values.minimumMaturityYears! - 0.50137) < 0.0001);
  assert.equal(values.maximumMaturityYears, 2);
});

test("maps unbounded maturity settings to null form values", () => {
  const values = toStrategyFormValues({
    minimumRating: "AA_minus",
    minimumMaturityDays: null,
    maximumMaturityDays: null,
    minimumMedianDailyVolume: 1_000,
    minimumYtm: 15,
    maximumYtm: 30,
    offerWindowDays: 90,
    instrumentType: "Any",
    faceUnit: null,
    currencyId: null
  });

  assert.equal(values.minimumMaturityYears, null);
  assert.equal(values.maximumMaturityYears, null);
});

test("maps form values back to API settings converting years to days", () => {
  const values: StrategyFormValues = {
    minimumRating: "AA_minus",
    minimumMaturityYears: 0.5,
    maximumMaturityYears: 2,
    minimumMedianDailyVolume: 1_000,
    minimumYtmPercent: 12.5,
    maximumYtmPercent: 30,
    offerWindowDays: 90,
    instrumentType: "Any",
    faceUnit: "USD",
    currencyId: "RUB"
  };

  assert.deepEqual(fromStrategyFormValues(values), {
    minimumRating: "AA_minus",
    minimumMaturityDays: 183,
    maximumMaturityDays: 730,
    minimumMedianDailyVolume: 1_000,
    minimumYtm: 12.5,
    maximumYtm: 30,
    offerWindowDays: 90,
    instrumentType: "Any",
    faceUnit: "USD",
    currencyId: "RUB"
  });
});

test("maps empty maturity bounds to null days and preserves them as null", () => {
  const values: StrategyFormValues = {
    minimumRating: "AA_minus",
    minimumMaturityYears: null,
    maximumMaturityYears: null,
    minimumMedianDailyVolume: 1_000,
    minimumYtmPercent: 12.5,
    maximumYtmPercent: 30,
    offerWindowDays: 90,
    instrumentType: "Floating",
    faceUnit: null,
    currencyId: null
  };

  const settings = fromStrategyFormValues(values);
  assert.equal(settings.minimumMaturityDays, null);
  assert.equal(settings.maximumMaturityDays, null);
  assert.equal(toStrategyFormValues(settings).minimumMaturityYears, null);
});

test("daysToYears converts days to fractional years", () => {
  assert.equal(daysToYears(365), 1);
  assert.ok(Math.abs(daysToYears(183) - 0.50137) < 0.0001);
});
