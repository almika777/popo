using Microsoft.EntityFrameworkCore;
using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.Extensions;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio.Trades;
using Popo.Storage;

using static Popo.Api.Services.Position.PositionCurrencyRates;

namespace Popo.Api.Services.Position;

internal static class PositionValuationCalculator
{
    private static readonly PositionCalculatorFactory PositionCalculatorFactory = new();

    internal static ValuedPosition BuildPosition(
        PositionData data,
        PositionAdditionalData referenceData,
        DateOnly asOf)
    {
        var marketPricing = CalculateMarketPrice(data, referenceData.CurrencyRates, asOf);
        var couponValue = GetPositionCouponValue(data, referenceData, marketPricing.QuoteDate);
        var calculator = PositionCalculatorFactory.Create(data.Security?.BondType);
        var isFloater = calculator is FloaterPositionCalculator;
        var tradePricing = CalculatePositionTradePricing(
            data,
            referenceData.CurrencyRates,
            marketPricing,
            calculator is not ZoPositionCalculator);
        var isNominalIndexed = data.IsNominalIndexed
                               || data.Security is { InitialFaceValue: <= 0, FaceValue: > 0 } security
                               && HasOpenLotNominalGrowth(tradePricing, security.FaceValue);
        var canNormalizeOpenCost = tradePricing
            .Where(x => x.Trade.Side == TradeSide.Buy)
            .All(x => x.PurchasePricePercent.HasValue && x.PurchasePriceAtCurrentFaceValue.HasValue);
        var positionIncomeTrades = tradePricing.Select(x => new PositionIncomeTrade(
            x.Trade.TradeDate,
            x.Trade.Side,
            x.Trade.Quantity,
            x.PurchasePriceAtCurrentFaceValue ?? x.Trade.Price,
            x.Trade.Commission,
            x.PurchasePricePercent,
            x.Trade.Price,
            x.Trade.AccruedInterest)).ToArray();
        var positionCalculation = calculator is DefaultPositionCalculator
                                  && (!marketPricing.MarketPrice.HasValue
                                      || data.Marketdata?.CurrentPrice is null
                                      || data.Security is null
                                      || !canNormalizeOpenCost)
            ? new PositionCalculationResult(
                null,
                PortfolioPositionCalculator.Calculate(data.Position, marketPricing.MarketPrice),
                null,
                data.Position.AverageBuyPrice)
            : calculator.Calculate(new PositionCalculationInput(
                data.Position,
                positionIncomeTrades,
                asOf,
                marketPricing.MarketPrice,
                couponValue.GetValueOrDefault(),
                data.Security?.CouponPeriod ?? 0,
                isNominalIndexed,
                data.Security?.Accruedint,
                GetPositionCouponPayments(data, referenceData),
                data.Security?.CouponFrequency,
                data.Security?.NextCouponDate,
                marketPricing.QuoteDate));
        var income = positionCalculation.Income;
        var averageBuyPriceAtCurrentFaceValue = positionCalculation.AverageBuyPriceAtCurrentFaceValue;
        var valuation = positionCalculation.Valuation;
        var annualizedReturnUnavailableReason = income is { IsCouponIncomeComplete: true }
                                               && couponValue.HasValue
                                               && income.AnnualizedTotalPnlPercent is null
                                               && PositionOpenLotCalculator.Calculate(positionIncomeTrades, asOf)
                                                   .Lots.Any(lot => lot.TradeDate > marketPricing.QuoteDate)
            ? "QuoteBeforeLatestOpenPurchase"
            : null;

        return CreateValuedPosition(
            data,
            marketPricing,
            couponValue,
            income,
            averageBuyPriceAtCurrentFaceValue,
            valuation,
            isNominalIndexed,
            positionCalculation.AverageBuyPrice,
            isFloater,
            annualizedReturnUnavailableReason);
    }

    private static bool HasOpenLotNominalGrowth(
        IReadOnlyCollection<PositionTradePricing> tradePricing,
        double currentFaceValue)
    {
        var openLots = new List<(double Quantity, double FaceValue)>();
        foreach (var day in tradePricing.GroupBy(x => x.Trade.TradeDate).OrderBy(x => x.Key))
        {
            foreach (var buy in day.Where(x => x.Trade.Side == TradeSide.Buy))
            {
                var tradeFaceValue = buy.Trade.FaceValue;
                if (!double.IsFinite(tradeFaceValue) || tradeFaceValue <= 0)
                    return false;

                openLots.Add((buy.Trade.Quantity, tradeFaceValue));
            }

            var soldQuantity = day.Where(x => x.Trade.Side == TradeSide.Sell).Sum(x => x.Trade.Quantity);
            var availableQuantity = openLots.Sum(x => x.Quantity);
            if (soldQuantity > availableQuantity)
                return false;
            if (soldQuantity <= 0)
                continue;

            var retainedFraction = (availableQuantity - soldQuantity) / availableQuantity;
            if (retainedFraction <= 0)
            {
                openLots.Clear();
                continue;
            }

            for (var index = 0; index < openLots.Count; index++)
                openLots[index] = (openLots[index].Quantity * retainedFraction, openLots[index].FaceValue);
        }

        return openLots.Any(x => x.Quantity > 0 && x.FaceValue < currentFaceValue);
    }

    internal static PositionMarketPriceData CalculateMarketPrice(
        PositionData data,
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        DateOnly asOf)
    {
        var quoteDate = data.Marketdata?.SysTime is { } sysTime
            ? DateOnly.FromDateTime(sysTime)
            : asOf;
        var tradingCurrencyRateToRub = GetCurrencyRate(rates, data.Position.CurrencyId, quoteDate);
        var faceCurrencyRateToRub = data.Security is null
            ? null
            : GetCurrencyRate(rates, data.Security.FaceUnit, quoteDate);
        var marketPrice = data.Security is null || data.Marketdata is null
            ? null
            : PortfolioMarketPriceCalculator.Calculate(
                data.Security.FaceValue,
                data.Security.FaceUnit,
                data.Position.CurrencyId,
                data.Marketdata.CurrentPrice,
                faceCurrencyRateToRub,
                tradingCurrencyRateToRub);

        return new PositionMarketPriceData(
            quoteDate,
            tradingCurrencyRateToRub,
            faceCurrencyRateToRub,
            marketPrice);
    }

    internal static bool HasCurrentMarketPrice(PositionData data) =>
        data.Marketdata?.CurrentPrice is { } price && double.IsFinite(price) && price > 0;

    private static double? GetPositionCouponValue(
        PositionData data,
        PositionAdditionalData referenceData,
        DateOnly quoteDate)
    {
        if (data.Security is null)
            return null;

        var latestCoupon = referenceData.CouponHistory
            .Where(x => x.Date <= quoteDate && x.Value.HasValue)
            .OrderByDescending(x => x.Date)
            .FirstOrDefault(x =>
                string.Equals(x.SecId, data.Security.SecId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Isin, data.Security.Isin, StringComparison.OrdinalIgnoreCase));

        return latestCoupon is not null
            ? ConvertCurrencyAmount(
                latestCoupon.Value!.Value,
                latestCoupon.FaceUnit,
                data.Position.CurrencyId,
                referenceData.CurrencyRates,
                quoteDate)
            : ConvertCurrencyAmount(
                data.Security.CouponValue,
                data.Security.FaceUnit,
                data.Position.CurrencyId,
                referenceData.CurrencyRates,
                quoteDate);
    }

    private static IReadOnlyCollection<PositionCouponPayment> GetPositionCouponPayments(
        PositionData data,
        PositionAdditionalData referenceData)
    {
        if (data.Security is null)
            return [];

        return referenceData.CouponHistory
            .Where(x => string.Equals(x.SecId, data.Security.SecId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.Isin, data.Security.Isin, StringComparison.OrdinalIgnoreCase))
            .Select(x => new PositionCouponPayment(
                x.Date,
                x.PeriodStartDate,
                x.Value,
                x.FaceUnit,
                GetCurrencyRate(referenceData.CurrencyRates, x.FaceUnit, x.Date),
                x.RecordDate,
                x.ValueInRub))
            .ToArray();
    }

    private static PositionTradePricing[] CalculatePositionTradePricing(
        PositionData data,
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        PositionMarketPriceData marketPriceData,
        bool normalizeCleanPrices)
    {
        if (data.Security is null)
            return [];

        return data.Trades
            .Select(trade =>
            {
                var purchasePricePercent = PortfolioMarketPriceCalculator.CalculateTradePricePercent(
                    trade.Price,
                    trade.FaceValue,
                    data.Security.InitialFaceValue,
                    data.Security.FaceValue,
                    data.Security.FaceUnit,
                    trade.CurrencyId,
                    GetCurrencyRate(rates, data.Security.FaceUnit, trade.TradeDate),
                    GetCurrencyRate(rates, trade.CurrencyId, trade.TradeDate));
                var purchasePriceAtCurrentFaceValue = normalizeCleanPrices && purchasePricePercent.HasValue
                    ? PortfolioMarketPriceCalculator.Calculate(
                        data.Security.FaceValue,
                        data.Security.FaceUnit,
                        data.Position.CurrencyId,
                        purchasePricePercent.Value,
                        marketPriceData.FaceCurrencyRateToRub,
                        marketPriceData.TradingCurrencyRateToRub)
                    : null;

                return new PositionTradePricing(trade, purchasePricePercent, purchasePriceAtCurrentFaceValue);
            })
            .ToArray();
    }

    private static ValuedPosition CreateValuedPosition(
        PositionData data,
        PositionMarketPriceData marketPriceData,
        double? couponValue,
        PortfolioPositionIncome? income,
        double? averageBuyPriceAtCurrentFaceValue,
        PortfolioPositionValuation valuation,
        bool isNominalIndexed,
        double? averageBuyPrice,
        bool isFloaterCalculator,
        string? annualizedReturnUnavailableReason)
    {
        var completeCouponIncome = income is { IsCouponIncomeComplete: true }
                                   && (isFloaterCalculator || couponValue.HasValue)
            ? income
            : null;
        var position = data.Position with
        {
            AverageBuyPrice = averageBuyPrice ?? (income is
                { RemainingQuantity: > 0, ActualRemainingCleanCost: { } actualRemainingCleanCost }
                ? actualRemainingCleanCost / income.RemainingQuantity
                : data.Position.AverageBuyPrice),
            MarketPrice = valuation.MarketPrice,
            MarketValue = valuation.MarketValue,
            UnrealizedPnl = income?.UnrealizedPnl ?? valuation.UnrealizedPnl,
            UnrealizedPnlPercent = income?.UnrealizedPnlPercent ?? valuation.UnrealizedPnlPercent,
            ShortName = data.Security?.ShortName ?? data.Position.SecId,
            AverageDailyVolume = data.VolumeStatistics?.Average,
            MedianDailyVolume = data.VolumeStatistics?.Median,
            ApproximateCouponIncome = completeCouponIncome?.CouponIncome,
            ApproximateTotalPnl = completeCouponIncome?.TotalPnl,
            ApproximateTotalPnlPercent = completeCouponIncome?.TotalPnlPercent,
            ApproximateAnnualizedTotalPnlPercent = (completeCouponIncome?.AnnualizedTotalPnlPercent).Round(),
            ApproximateAnnualizedReturnBreakdown = completeCouponIncome?.AnnualizedTotalPnlPercent is not null
                ? RoundAnnualizedBreakdown(completeCouponIncome.AnnualizedReturnBreakdown)
                : null,
            ApproximateAnnualizedReturnUnavailableReason = annualizedReturnUnavailableReason,
            AverageBuyPriceAtCurrentFaceValue = averageBuyPriceAtCurrentFaceValue,
            AverageBuyPricePercent = income?.AverageBuyPricePercent,
            CurrentFaceValue = data.Security?.FaceValue,
            FaceUnit = data.Security?.FaceUnit,
            MarketPricePercent = data.Marketdata?.CurrentPrice,
            IsNominalIndexed = isNominalIndexed
        };

        return new ValuedPosition(position, marketPriceData.TradingCurrencyRateToRub);
    }

    private static PositionAnnualizedReturnBreakdown? RoundAnnualizedBreakdown(
        PositionAnnualizedReturnBreakdown? breakdown) => breakdown is null ? null : breakdown with
    {
        TotalPnl = breakdown.TotalPnl.Round(),
        InvestedCapitalDays = breakdown.InvestedCapitalDays.Round(),
        AnnualizedPercent = breakdown.AnnualizedPercent.Round(),
        Lots = breakdown.Lots.Select(lot => lot with
        {
            Quantity = lot.Quantity.Round(),
            PurchaseCleanPricePerUnit = lot.PurchaseCleanPricePerUnit.Round(),
            PaidAccruedInterestPerUnit = lot.PaidAccruedInterestPerUnit.Round(),
            CommissionPerUnit = lot.CommissionPerUnit.Round(),
            InvestedCapital = lot.InvestedCapital.Round(),
            CapitalDays = lot.CapitalDays.Round()
        }).ToArray()
    };

}
