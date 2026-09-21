using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Popo.Core.Common;
using Popo.Core.HttpClients;
using Popo.Jobs.Initialization;
using Popo.Storage;
using Popo.Storage.Entities;

namespace Popo.Jobs.Jobs;

public sealed class CbrCurrencyRatesUpdateJob(
    ICbrHttpClient cbrHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger<CbrCurrencyRatesUpdateJob> logger,
    IInitializationProgressReporter progressReporter)
    : JobBase(dbContextFactory, logger, progressReporter), ICbrCurrencyRatesUpdateJob
{
    private static readonly DateOnly HistoryStart = new(2025, 1, 1);

    public async Task UpdateAsync(CancellationToken ct = default)
    {
        await using var context = await CreateDbContextAsync(ct);
        var settlementCurrencies = await context.MoexBondSecurities
            .AsNoTracking()
            .Where(x => x.CurrencyId != null && x.CurrencyId != string.Empty)
            .Select(x => x.CurrencyId!)
            .ToListAsync(ct);
        var faceCurrencies = await context.MoexBondSecurities
            .AsNoTracking()
            .Where(x => x.FaceUnit != string.Empty)
            .Select(x => x.FaceUnit)
            .ToListAsync(ct);
        var currencies = settlementCurrencies
            .Concat(faceCurrencies)
            .Select(NormalizeCurrencyCode)
            .Where(x => x != "RUB")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        await ReportProgressAsync(0, currencies.Count, "Загрузка курсов валют ЦБ", ct);

        if (currencies.Count == 0)
        {
            logger.LogInformation("Обновление курсов ЦБ пропущено: валютные облигации не найдены.");
            return;
        }

        var definitions = await ExecuteWithRetryAsync(
            () => cbrHttpClient.GetCurrenciesAsync(ct),
            "получение списка валют ЦБ",
            ct);
        var definitionsByCode = definitions.ToDictionary(x => x.CurrencyCode, StringComparer.OrdinalIgnoreCase);
        var to = MoscowTime.Today;
        var entities = new List<CurrencyRateEntity>();

        foreach (var currencyCode in currencies)
        {
            if (!definitionsByCode.TryGetValue(currencyCode, out var definition))
            {
                logger.LogWarning("В списке валют ЦБ не найдена валюта {CurrencyCode}.", currencyCode);
                continue;
            }

            var rates = await ExecuteWithRetryAsync(
                () => cbrHttpClient.GetCurrencyRatesAsync(definition, HistoryStart, to, ct),
                $"получение курсов ЦБ {currencyCode}",
                ct);
            entities.AddRange(rates.Select(x => new CurrencyRateEntity
            {
                RateDate = x.RateDate,
                CurrencyCode = x.CurrencyCode,
                Name = x.Name,
                Nominal = x.Nominal,
                Value = x.Value,
                UnitRate = x.UnitRate
            }));
            await ReportProgressAsync(currencies.IndexOf(currencyCode) + 1, currencies.Count,
                "Загрузка курсов валют ЦБ", ct);
        }

        if (entities.Count == 0)
        {
            logger.LogInformation("Обновление курсов ЦБ завершено: источник не вернул курсы.");
            return;
        }

        await context.BulkInsertOrUpdateAsync(
            entities,
            config =>
            {
                config.UpdateByProperties = [
                    nameof(CurrencyRateEntity.RateDate),
                    nameof(CurrencyRateEntity.CurrencyCode)
                ];
            },
            cancellationToken: ct);

        logger.LogInformation(
            "Обновление курсов ЦБ завершено: загружено курсов — {RateCount}, валют — {CurrencyCount}.",
            entities.Count,
            currencies.Count);
    }

    private static string NormalizeCurrencyCode(string currencyCode) =>
        currencyCode.Trim().ToUpperInvariant() switch
        {
            "SUR" or "RUR" => "RUB",
            var normalized => normalized
        };
}

public interface ICbrCurrencyRatesUpdateJob : IHangfireJob
{
}
