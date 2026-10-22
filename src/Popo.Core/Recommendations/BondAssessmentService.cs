using Popo.Core.Bonds;
using Popo.Core.Calculators;
using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;

namespace Popo.Core.Recommendations;

/// <summary>
/// Собирает данные по облигациям и формирует их оценки для рекомендаций по размещению свободных денег.
/// </summary>
/// <param name="bondsProvider">Поставщик сохранённых характеристик облигаций.</param>
/// <param name="ratingsProvider">Поставщик кредитных рейтингов эмитентов.</param>
/// <param name="moexHttpClient">Клиент для получения текущих рыночных котировок MOEX.</param>
/// <param name="historyProvider">Поставщик статистики дневного торгового оборота.</param>
/// <param name="cashFlowProvider">Поставщик будущих денежных потоков для расчёта доходности.</param>
public sealed class BondAssessmentService(
    IBondsProvider bondsProvider,
    IRatingsProvider ratingsProvider,
    IMoexHttpClient moexHttpClient,
    IHistoryProvider historyProvider,
    ICashFlowProvider cashFlowProvider) : IBondAssessmentService
{
    /// <summary>
    /// Рассчитывает доступные показатели и диагностические признаки для каждой сохранённой облигации.
    /// </summary>
    /// <param name="settings">
    /// Настройки стратегии, определяющие признаки соответствия типу инструмента, сроку и окну оферты.
    /// </param>
    /// <param name="cancellationToken">Токен отмены загрузки данных и расчётов.</param>
    /// <returns>
    /// Оценки сохранённых облигаций. Кандидат с рассчитанной доходностью появляется только при наличии
    /// необходимых рыночных данных, рейтинга, ликвидности и графика денежных потоков.
    /// </returns>
    public async Task<IReadOnlyList<BondAssessment>> GetAssessmentsAsync(
        InvestmentStrategySettings settings,
        CancellationToken cancellationToken)
    {
        var today = MoscowTime.Today;
        var data = await LoadAssessmentDataAsync(cancellationToken);
        var result = new List<BondAssessment>(data.Securities.Count);

        foreach (var security in data.Securities)
        {
            result.Add(await AssessSecurityAsync(security, data, settings, today, cancellationToken));
        }

        return result;
    }

    /// <summary>
    /// Последовательно загружает источники и индексирует котировки по бумаге и режиму торгов.
    /// Провайдеры могут использовать общий DbContext, поэтому запросы не выполняются параллельно.
    /// </summary>
    private async Task<AssessmentData> LoadAssessmentDataAsync(CancellationToken cancellationToken)
    {
        var securities = await bondsProvider.GetSecuritiesAsync(cancellationToken);
        var ratings = await ratingsProvider.GetBondRatingsAsync(cancellationToken);
        var statistics = await historyProvider.GetDailyVolumeStatistics(cancellationToken);
        var marketData = await moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken);
        var market = new Dictionary<(string SecId, string BoardId), MoexMarketdata>();
        foreach (var row in marketData.Values.SelectMany(rows => rows))
        {
            // Как и прежде, при дублировании пары используется первая котировка.
            market.TryAdd((row.SecId, row.BoardId), row);
        }

        return new AssessmentData(securities, ratings, statistics, market);
    }

    /// <summary>
    /// Собирает все причины неполноты исходных данных и доступные показатели одной бумаги.
    /// Проверки срока и окна оферты выполняются при наличии всех предпосылок расчёта кандидата.
    /// </summary>
    private async Task<BondAssessment> AssessSecurityAsync(
        BondSecurityDto security,
        AssessmentData data,
        InvestmentStrategySettings settings,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        data.Market.TryGetValue((security.SecId, security.BoardId), out var marketData);
        var rating = data.Ratings.TryGetValue(security.Isin, out var ratingSource)
            ? BondAssessmentRules.GetRecommendationRating(ratingSource)
            : null;
        data.Statistics.TryGetValue(security.SecId, out var volume);
        var yieldRequest = CreateYieldRequest(security, today);
        var issues = CollectDataIssues(marketData?.CurrentPrice, rating, volume, yieldRequest);

        BondCandidate? candidate = null;
        var meetsMaturity = false;
        var meetsOfferWindow = false;
        if (yieldRequest is not null && marketData?.CurrentPrice is { } price &&
            rating is { } actualRating && volume is not null)
        {
            meetsMaturity = BondAssessmentRules.MeetsMaturityRange(security, yieldRequest, settings);
            meetsOfferWindow = BondAssessmentRules.MeetsOfferWindow(yieldRequest, settings);
            candidate = await CreateCandidateAsync(
                security, yieldRequest, price, actualRating, volume.Median, issues, cancellationToken);
        }

        return new BondAssessment(
            security.SecId, security.BoardId, security.ShortName, security.CurrencyId, security.FaceUnit,
            candidate?.YieldDate, candidate?.Ytm, rating, volume?.Average, volume?.Median,
            meetsMaturity, meetsOfferWindow,
            BondAssessmentRules.IsAllowedByInstrumentType(security, settings.InstrumentType), candidate, issues);
    }

    /// <summary>Проверяет данные эмитента и создаёт запрос расчёта до погашения или ближайшей оферты.</summary>
    private static BondYieldRequest? CreateYieldRequest(BondSecurityDto security, DateOnly today)
    {
        if (security.EmitterId is null || string.IsNullOrWhiteSpace(security.IssuerName) ||
            string.IsNullOrWhiteSpace(security.CurrencyId))
            return null;

        return BondAssessmentRules.TryCreateYieldRequest(security, today, out var request) ? request : null;
    }

    /// <summary>Собирает все независимые причины отсутствия исходных данных в стабильном порядке.</summary>
    private static List<BondAssessmentIssue> CollectDataIssues(
        double? price,
        CreditRating? rating,
        DailyVolumeStatistics? volume,
        BondYieldRequest? yieldRequest)
    {
        var issues = new List<BondAssessmentIssue>();
        if (price is null) issues.Add(BondAssessmentIssue.MissingMarketPrice);
        if (rating is null) issues.Add(BondAssessmentIssue.MissingRating);
        if (volume is null) issues.Add(BondAssessmentIssue.MissingLiquidity);
        if (yieldRequest is null) issues.Add(BondAssessmentIssue.MissingSecurityData);
        return issues;
    }

    /// <summary>
    /// Загружает денежные потоки, рассчитывает грязную цену и доходность, затем формирует кандидата.
    /// При неполном расписании или невозможном расчёте добавляет причину отказа и возвращает null.
    /// </summary>
    private async Task<BondCandidate?> CreateCandidateAsync(
        BondSecurityDto security,
        BondYieldRequest yieldRequest,
        double price,
        CreditRating rating,
        double medianDailyVolume,
        List<BondAssessmentIssue> issues,
        CancellationToken cancellationToken)
    {
        var schedule = await cashFlowProvider.GetYieldSchedule(yieldRequest, cancellationToken);
        if (schedule is null)
        {
            issues.Add(BondAssessmentIssue.MissingYieldSchedule);
            return null;
        }

        var dirtyPrice = BondPriceCalculator.CalculateDirtyPrice(
            price, schedule.FaceValueInSettlementCurrency, security.AccruedInterest);
        if (dirtyPrice is null)
        {
            issues.Add(BondAssessmentIssue.InvalidYield);
            return null;
        }

        var ytm = YieldCalculator.TryCalculate(yieldRequest.SettlementDate, dirtyPrice.Value, schedule.CashFlows);
        if (ytm is null)
        {
            issues.Add(BondAssessmentIssue.InvalidYield);
            return null;
        }

        return new BondCandidate(
            security.SecId, security.BoardId, security.EmitterId!.Value, security.IssuerName!,
            security.CurrencyId!, ytm.Value, dirtyPrice.Value, rating, security.MatDate!.Value,
            security.OfferDate ?? security.BuybackDate,
            security.IsFloater ? BondCouponType.Floating : BondCouponType.Fixed,
            medianDailyVolume, schedule.YieldDate, schedule.YieldDateType,
            BondAssessmentRules.CreateCalculationDetails(
                yieldRequest.SettlementDate, price, security.AccruedInterest!.Value, dirtyPrice.Value, schedule));
    }

    /// <summary>Загруженные источники одного запроса, общие для оценки всех облигаций.</summary>
    private sealed record AssessmentData(
        IReadOnlyList<BondSecurityDto> Securities,
        IReadOnlyDictionary<string, RatingsDto> Ratings,
        IReadOnlyDictionary<string, DailyVolumeStatistics> Statistics,
        IReadOnlyDictionary<(string SecId, string BoardId), MoexMarketdata> Market);
}

public interface IBondAssessmentService
{
    /// <summary>
    /// Возвращает оценки сохранённых облигаций, включая доступную доходность и причины неполных данных.
    /// </summary>
    /// <param name="settings">Настройки стратегии для вычисления признаков соответствия.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Оценки облигаций, по которым вызывающий сервис может отобрать рекомендации.</returns>
    Task<IReadOnlyList<BondAssessment>> GetAssessmentsAsync(
        InvestmentStrategySettings settings,
        CancellationToken cancellationToken);
}
