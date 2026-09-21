using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Popo.Core.HttpClients;
using Popo.Jobs.Initialization;
using Popo.Storage;
using Popo.Storage.Entities.Moex;

namespace Popo.Jobs.Jobs;

public sealed class MoexBondSecuritiesUpdateJob(
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger<MoexBondSecuritiesUpdateJob> logger,
    IInitializationProgressReporter progressReporter)
    : JobBase(dbContextFactory, logger, progressReporter), IMoexBondSecuritiesUpdateJob
{
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        var payload = await ExecuteWithRetryAsync(
            () => moexHttpClient.GetActiveBondsSecuritiesAsync(ct),
            "получение списка облигаций MOEX", ct);

        var entities = new List<MoexBondSecurityEntity>(payload.Sum(x => x.Value.Count));
        await ReportProgressAsync(0, entities.Capacity, "Подготовка торговых параметров", ct);

        foreach (var (_, bonds) in payload)
        {
            entities.AddRange(bonds.Select(bond => new MoexBondSecurityEntity
            {
                SecId = bond.SecId,
                ShortName = bond.ShortName,
                PrevWaPrice = bond.PrevWaPrice,
                YieldAtPrevWaPrice = bond.YieldAtPrevWaPrice,
                CouponValue = bond.CouponValue,
                NextCouponDate = bond.NextCouponDate,
                AccruedInt = bond.Accruedint,
                PrevPrice = bond.PrevPrice,
                LotSize = bond.LotSize,
                FaceValue = bond.FaceValue,
                BoardName = bond.BoardName,
                BoardId = bond.BoardId,
                Status = bond.Status,
                MatDate = bond.MatDate,
                Decimals = bond.Decimals,
                CouponPeriod = bond.CouponPeriod,
                IssueSize = bond.IssueSize,
                PrevLegalClosePrice = bond.PrevLegalClosePrice,
                PrevTradeDate = bond.PrevTradeDate,
                SecName = bond.Name,
                Remarks = bond.Remarks,
                MarketCode = bond.MarketCode,
                InstrId = bond.InstrId,
                SectorId = bond.SectorId,
                MinStep = bond.MinStep,
                FaceUnit = NormalizeCurrency(bond.FaceUnit),
                BuybackPrice = bond.BuybackPrice,
                BuybackDate = bond.BuybackDate,
                Isin = bond.Isin,
                LatName = bond.LatName,
                RegNumber = bond.RegNumber,
                CurrencyId = NormalizeCurrency(bond.CurrencyId),
                IssueSizePlaced = bond.IssueSizePlaced,
                ListLevel = bond.ListLevel,
                SecType = bond.SecType,
                CouponPercent = bond.CouponPercent,
                OfferDate = bond.OfferDate,
                SettleDate = bond.SettleDate,
                LotValue = bond.LotValue,
                FaceValueOnSettleDate = bond.FaceValueOnSettleDate,
                CallOptionDate = bond.CallOptionDate,
                PutOptionDate = bond.PutOptionDate,
                DateYieldFromIssuer = bond.DateYieldFromIssuer,
                BondType = bond.BondType,
                BondSubType = bond.BondSubType,
                Updated = DateTimeOffset.UtcNow
            }));
        }

        await using var ctx = await CreateDbContextAsync(ct);
        await ctx.BulkInsertOrUpdateAsync(entities,
            config =>
            {
                config.UpdateByProperties =
                [
                    nameof(MoexBondSecurityEntity.SecId),
                    nameof(MoexBondSecurityEntity.BoardId)
                ];
            }, cancellationToken: ct);
        await ReportProgressAsync(entities.Count, entities.Count, "Сохранение торговых параметров", ct);
    }
    private static string NormalizeCurrency(string currencyId)
    {
        return string.Equals(currencyId, "SUR", StringComparison.OrdinalIgnoreCase) ? "RUB" : currencyId;
    }
}

public interface IMoexBondSecuritiesUpdateJob : IHangfireJob
{
}
