import type { InvestmentStrategySettings } from "./cash-investment-recommendations-api";

export type StrategyFormValues = Pick<InvestmentStrategySettings, "minimumRating" | "offerWindowDays" | "instrumentType" | "faceUnit" | "currencyId"> & {
  presetName: string;
  minimumMaturityYears: number | null;
  maximumMaturityYears: number | null;
  minimumMedianDailyVolume: number;
  minimumYtmPercent: number;
  maximumYtmPercent: number;
};

const DaysPerYear = 365;

export function yearsToDays(years: number): number {
  return Math.round(years * DaysPerYear);
}

export function daysToYears(days: number): number {
  return days / DaysPerYear;
}

export function toStrategyFormValues(settings: InvestmentStrategySettings): StrategyFormValues {
  return {
    presetName: "",
    minimumRating: settings.minimumRating,
    minimumMaturityYears: settings.minimumMaturityDays == null ? null : daysToYears(settings.minimumMaturityDays),
    maximumMaturityYears: settings.maximumMaturityDays == null ? null : daysToYears(settings.maximumMaturityDays),
    minimumMedianDailyVolume: settings.minimumMedianDailyVolume,
    minimumYtmPercent: settings.minimumYtm,
    maximumYtmPercent: settings.maximumYtm,
    offerWindowDays: settings.offerWindowDays,
    instrumentType: settings.instrumentType,
    faceUnit: settings.faceUnit,
    currencyId: settings.currencyId
  };
}

export function fromStrategyFormValues(values: StrategyFormValues): InvestmentStrategySettings {
  return {
    minimumRating: values.minimumRating,
    minimumMaturityDays: values.minimumMaturityYears == null ? null : yearsToDays(values.minimumMaturityYears),
    maximumMaturityDays: values.maximumMaturityYears == null ? null : yearsToDays(values.maximumMaturityYears),
    minimumMedianDailyVolume: values.minimumMedianDailyVolume,
    minimumYtm: values.minimumYtmPercent,
    maximumYtm: values.maximumYtmPercent,
    offerWindowDays: values.offerWindowDays,
    instrumentType: values.instrumentType,
    faceUnit: values.faceUnit ?? null,
    currencyId: values.currencyId ?? null
  };
}
