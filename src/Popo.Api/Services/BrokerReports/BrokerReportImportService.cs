using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Popo.Api.Models;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Services.BrokerReports;

public sealed class BrokerReportImportService(
    TBankBrokerReportPdfParser parser,
    IBondsService bondsService,
    IPortfolioLedgerProvider ledgerProvider,
    IPortfolioReturnInputsProvider returnInputsProvider,
    IBrokerReportImportProvider importProvider)
{
    private const int MaximumQuantitySubsetStates = 50_000;

    public async Task<BrokerReportPreviewResponse> PreviewAsync(
        Stream pdfStream,
        CancellationToken cancellationToken)
    {
        var parsedReport = parser.Parse(pdfStream);
        var state = await LoadLedgerStateAsync(cancellationToken);
        var cutoffDate = FindCutoffDate(state);
        var bondDetails = await ResolveBondDetailsAsync(parsedReport.Trades, cancellationToken);
        var tradeDuplicateAnalysis = FindDuplicateTradeIndexes(parsedReport.Trades, state);
        var duplicateCashFlowIndexes = FindDuplicateCashFlowIndexes(parsedReport.CashFlows, state.CashFlows);
        var operations = new List<BrokerReportOperationResponse>();
        var duplicatesHidden = 0;
        var outsideHistoryHidden = 0;
        var unsupportedHidden = parsedReport.UnsupportedOperationsHidden;

        for (var tradeIndex = 0; tradeIndex < parsedReport.Trades.Count; tradeIndex++)
        {
            var trade = parsedReport.Trades[tradeIndex];
            var operationId = CreateBrokerTradeOperationId(trade.BrokerTradeId);
            if (cutoffDate.HasValue && trade.TradeDate < cutoffDate.Value)
            {
                outsideHistoryHidden++;
                continue;
            }

            if (RelationHelper.MoneyMarketFunds.TryGetValue(trade.InstrumentCode, out var fund))
            {
                if (tradeDuplicateAnalysis.DuplicateIndexes.Contains(tradeIndex))
                {
                    duplicatesHidden++;
                    continue;
                }

                var hasFundBase = state.Funds.Any(x =>
                    string.Equals(x.SecId, trade.InstrumentCode, StringComparison.OrdinalIgnoreCase));
                operations.Add(new BrokerReportOperationResponse(
                    operationId,
                    BrokerReportOperationKind.FundOperation,
                    trade.TradeDate,
                    fund.Name,
                    trade.InstrumentCode,
                    fund.BoardId,
                    "RUB",
                    trade.Side,
                    (double)trade.Quantity,
                    (double)trade.UnitPrice,
                    null,
                    null,
                    (double)trade.Commission,
                    (double)trade.CleanAmount,
                    hasFundBase,
                    CombineWarnings(
                        hasFundBase
                            ? "Операция будет применена поверх сохранённого базового остатка фонда."
                            : "Сначала добавьте базовый остаток фонда в разделе «Ликвидность».",
                        GetAmbiguousTradeWarning(tradeDuplicateAnalysis, tradeIndex))));
                continue;
            }

            if (!bondDetails.TryGetValue(trade.InstrumentCode, out var bond)
                && !LooksLikeRussianIsin(trade.InstrumentCode))
            {
                unsupportedHidden++;
                continue;
            }

            if (tradeDuplicateAnalysis.DuplicateIndexes.Contains(tradeIndex))
            {
                duplicatesHidden++;
                continue;
            }

            var boardId = string.IsNullOrWhiteSpace(trade.TradingMode) ? bond?.BoardId : trade.TradingMode;
            operations.Add(new BrokerReportOperationResponse(
                operationId,
                BrokerReportOperationKind.BondTrade,
                trade.TradeDate,
                bond?.ShortName ?? trade.InstrumentCode,
                trade.InstrumentCode,
                boardId,
                trade.CurrencyId,
                trade.Side,
                (double)trade.Quantity,
                (double)trade.UnitPrice,
                bond?.FaceValue,
                (double)trade.AccruedInterestTotal,
                (double)trade.Commission,
                (double)trade.TotalAmount,
                true,
                CombineWarnings(
                    bond is null
                        ? "Не найден справочник MOEX: проверьте режим торгов, валюту и вручную укажите номинал."
                        : null,
                    GetAmbiguousTradeWarning(tradeDuplicateAnalysis, tradeIndex))));
        }

        var cashFlowOccurrences = new Dictionary<CashFlowDuplicateKey, int>();
        for (var cashFlowIndex = 0; cashFlowIndex < parsedReport.CashFlows.Count; cashFlowIndex++)
        {
            var cashFlow = parsedReport.CashFlows[cashFlowIndex];
            if (cutoffDate.HasValue && cashFlow.Date < cutoffDate.Value)
            {
                outsideHistoryHidden++;
                continue;
            }

            var type = cashFlow.IsDeposit ? PortfolioCashFlowType.Deposit : PortfolioCashFlowType.Withdrawal;
            var duplicateKey = CreateCashFlowDuplicateKey(cashFlow.Date, type, cashFlow.Amount);
            var occurrence = cashFlowOccurrences.GetValueOrDefault(duplicateKey);
            cashFlowOccurrences[duplicateKey] = occurrence + 1;
            if (duplicateCashFlowIndexes.Contains(cashFlowIndex))
            {
                duplicatesHidden++;
                continue;
            }

            operations.Add(new BrokerReportOperationResponse(
                CreateCashFlowOperationId(duplicateKey, occurrence),
                cashFlow.IsDeposit ? BrokerReportOperationKind.Deposit : BrokerReportOperationKind.Withdrawal,
                cashFlow.Date,
                cashFlow.IsDeposit ? "Пополнение, RUB" : "Вывод, RUB",
                null,
                null,
                "RUB",
                null,
                null,
                null,
                null,
                null,
                null,
                (double)cashFlow.Amount,
                true,
                null));
        }

        return new BrokerReportPreviewResponse(
            operations.OrderBy(x => x.Date).ThenBy(x => x.Description).ToArray(),
            duplicatesHidden,
            outsideHistoryHidden,
            unsupportedHidden);
    }

    public async Task<BrokerReportImportResponse> ImportAsync(
        BrokerReportImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Operations is null || request.Operations.Count == 0)
            throw new ArgumentException("Выберите хотя бы одну операцию для добавления.");

        if (request.Operations.Any(x => x.Id == Guid.Empty)
            || request.Operations.Select(x => x.Id).Distinct().Count() != request.Operations.Count)
        {
            throw new ArgumentException("В запросе обнаружены повторяющиеся или некорректные операции.");
        }

        var selected = request.Operations.Select(ToImportData).ToArray();
        var state = await LoadLedgerStateAsync(cancellationToken);
        var cutoffDate = FindCutoffDate(state);
        var existingOperationIds = state.Trades.Select(x => x.Id)
            .Concat(state.FundOperations.Select(x => x.Id))
            .Concat(state.CashFlows.Select(x => x.Id))
            .ToHashSet();
        var trades = new List<BrokerReportImportItem<PortfolioTrade>>();
        var fundOperations = new List<BrokerReportImportItem<MoneyMarketFundOperation>>();
        var cashFlows = new List<BrokerReportImportItem<PortfolioCashFlowInput>>();

        for (var operationIndex = 0; operationIndex < selected.Length; operationIndex++)
        {
            var operation = selected[operationIndex];
            if (cutoffDate.HasValue && operation.Source.Date < cutoffDate.Value)
                continue;
            if (existingOperationIds.Contains(operation.Source.Id))
                continue;

            if (operation.Trade is not null)
                trades.Add(new BrokerReportImportItem<PortfolioTrade>(operation.Source.Id, operation.Trade));
            else if (operation.FundOperation is not null)
                fundOperations.Add(new BrokerReportImportItem<MoneyMarketFundOperation>(
                    operation.Source.Id, operation.FundOperation));
            else if (operation.CashFlow is not null)
                cashFlows.Add(new BrokerReportImportItem<PortfolioCashFlowInput>(
                    operation.Source.Id, operation.CashFlow));
        }

        if (trades.Count + fundOperations.Count + cashFlows.Count == 0)
            return new BrokerReportImportResponse(0, 0, 0);

        var result = await importProvider.AddAsync(
            new BrokerReportImportBatch(trades, fundOperations, cashFlows),
            cancellationToken);

        return new BrokerReportImportResponse(
            result.TradesAdded,
            result.FundOperationsAdded,
            result.CashFlowsAdded);
    }

    private async Task<LedgerState> LoadLedgerStateAsync(CancellationToken cancellationToken)
    {
        var tradesTask = ledgerProvider.GetTradesAsync(cancellationToken);
        var fundOperationsTask = ledgerProvider.GetMoneyMarketFundOperationsAsync(cancellationToken);
        var fundsTask = ledgerProvider.GetMoneyMarketFundsAsync(cancellationToken);
        var cashFlowsTask = returnInputsProvider.GetCashFlowsAsync(cancellationToken);
        await Task.WhenAll(tradesTask, fundOperationsTask, fundsTask, cashFlowsTask);

        return new LedgerState(
            await tradesTask,
            await fundOperationsTask,
            await fundsTask,
            await cashFlowsTask);
    }

    private async Task<Dictionary<string, BondSearchResult>> ResolveBondDetailsAsync(
        IReadOnlyList<ParsedBrokerTrade> trades,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, BondSearchResult>(StringComparer.OrdinalIgnoreCase);
        var instrumentCodes = trades.Select(x => x.InstrumentCode)
            .Where(x => !RelationHelper.MoneyMarketFunds.ContainsKey(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var instrumentCode in instrumentCodes)
        {
            var matches = await bondsService.SearchAsync(instrumentCode, cancellationToken);
            var match = matches.FirstOrDefault(x =>
                string.Equals(x.SecId, instrumentCode, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                results[instrumentCode] = match;
        }

        return results;
    }

    private static DateOnly? FindCutoffDate(LedgerState state)
    {
        var dates = state.Trades.Select(x => x.TradeDate)
            .Concat(state.FundOperations.Select(x => x.Date))
            .Concat(state.CashFlows.Select(x => x.Date))
            .ToArray();
        return dates.Length == 0 ? null : dates.Min();
    }

    private static bool LooksLikeRussianIsin(string secId) =>
        secId.Length == 12
        && secId.StartsWith("RU", StringComparison.OrdinalIgnoreCase)
        && secId.All(char.IsLetterOrDigit);

    private static TradeDuplicateAnalysis FindDuplicateTradeIndexes(
        IReadOnlyList<ParsedBrokerTrade> candidates,
        LedgerState state)
    {
        var rows = candidates.Select((trade, index) => new TradeDuplicateCandidate(
            index,
            CreateTradeDuplicateKey(trade.TradeDate, trade.InstrumentCode, trade.Side, trade.UnitPrice),
            RoundToSixDecimals(trade.Quantity),
            RelationHelper.MoneyMarketFunds.ContainsKey(trade.InstrumentCode)))
            .ToArray();
        var analysis = FindDuplicateTradeIndexesCore(rows, state);
        var existingBrokerTradeIds = state.Trades.Select(x => x.Id)
            .Concat(state.FundOperations.Select(x => x.Id))
            .ToHashSet();
        for (var index = 0; index < candidates.Count; index++)
        {
            if (existingBrokerTradeIds.Contains(CreateBrokerTradeOperationId(candidates[index].BrokerTradeId)))
                analysis.DuplicateIndexes.Add(index);
        }

        analysis.AmbiguousIndexes.ExceptWith(analysis.DuplicateIndexes);
        return analysis;
    }

    private static TradeDuplicateAnalysis FindDuplicateTradeIndexesCore(
        IReadOnlyList<TradeDuplicateCandidate> candidates,
        LedgerState state)
    {
        var storedTrades = state.Trades.Select(trade => new StoredTradeQuantity(
            CreateTradeDuplicateKey(trade.TradeDate, trade.SecId, trade.Side, (decimal)trade.Price),
            RoundToSixDecimals(trade.Quantity)))
            .ToArray();
        var storedFundOperations = state.FundOperations.Select(operation => new StoredTradeQuantity(
            CreateTradeDuplicateKey(operation.Date, operation.SecId, operation.Side, (decimal)operation.Price),
            RoundToSixDecimals(operation.Quantity)))
            .ToArray();
        var storedFundQuantities = AggregateStoredTradeQuantities(storedFundOperations);

        // Fund operations can be represented in either ledger. Use the larger per-key total
        // rather than adding both ledgers, which could count the same purchase twice.
        foreach (var (key, quantity) in AggregateStoredTradeQuantities(storedTrades))
        {
            if (!storedFundQuantities.TryGetValue(key, out var fundQuantity) || quantity > fundQuantity)
                storedFundQuantities[key] = quantity;
        }

        var storedFunds = storedFundQuantities
            .Select(pair => new StoredTradeQuantity(pair.Key, pair.Value))
            .ToArray();
        var tradeAnalysis = FindDuplicateTradeIndexesCore(
            candidates.Where(candidate => !candidate.IsMoneyMarketFund).ToArray(), storedTrades);
        var fundAnalysis = FindDuplicateTradeIndexesCore(
            candidates.Where(candidate => candidate.IsMoneyMarketFund).ToArray(), storedFunds);
        tradeAnalysis.DuplicateIndexes.UnionWith(fundAnalysis.DuplicateIndexes);
        tradeAnalysis.AmbiguousIndexes.UnionWith(fundAnalysis.AmbiguousIndexes);
        tradeAnalysis.AmbiguousIndexes.ExceptWith(tradeAnalysis.DuplicateIndexes);
        return tradeAnalysis;
    }

    private static TradeDuplicateAnalysis FindDuplicateTradeIndexesCore(
        IReadOnlyList<TradeDuplicateCandidate> candidates,
        IReadOnlyList<StoredTradeQuantity> storedTrades)
    {
        var storedQuantities = AggregateStoredTradeQuantities(storedTrades);
        var duplicates = new HashSet<int>();
        var ambiguous = new HashSet<int>();

        foreach (var group in candidates.GroupBy(candidate => candidate.Key))
        {
            if (!storedQuantities.TryGetValue(group.Key, out var storedQuantity))
                continue;

            var candidatesForKey = group.OrderBy(candidate => candidate.Index).ToArray();
            var matching = AnalyzeQuantitySubsets(candidatesForKey, storedQuantity);
            if (!matching.Found)
            {
                ambiguous.UnionWith(candidatesForKey.Select(candidate => candidate.Index));
                continue;
            }

            duplicates.UnionWith(matching.RequiredIndexes);
            ambiguous.UnionWith(matching.PossibleIndexes.Except(matching.RequiredIndexes));
        }

        ambiguous.ExceptWith(duplicates);
        return new TradeDuplicateAnalysis(duplicates, ambiguous);
    }

    private static Dictionary<TradeDuplicateKey, decimal> AggregateStoredTradeQuantities(
        IEnumerable<StoredTradeQuantity> trades) => trades
        .GroupBy(trade => trade.Key)
        .ToDictionary(group => group.Key,
            group => RoundToSixDecimals(group.Sum(trade => trade.Quantity)));

    private static QuantitySubsetAnalysis AnalyzeQuantitySubsets(
        IReadOnlyList<TradeDuplicateCandidate> candidates,
        decimal targetQuantity)
    {
        var reachableQuantities = new Dictionary<decimal, QuantitySubsetSummary>
        {
            [0] = new QuantitySubsetSummary([], [])
        };
        foreach (var candidate in candidates)
        {
            var previousQuantities = reachableQuantities.ToArray();
            var nextQuantities = reachableQuantities.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone());
            foreach (var (quantity, indexes) in previousQuantities)
            {
                var combinedQuantity = RoundToSixDecimals(quantity + candidate.Quantity);
                if (combinedQuantity > targetQuantity)
                    continue;

                var includedIndexes = indexes.With(candidate.Index);
                if (nextQuantities.TryGetValue(combinedQuantity, out var existingSummary))
                    existingSummary.Merge(includedIndexes);
                else
                    nextQuantities.Add(combinedQuantity, includedIndexes);
            }

            reachableQuantities = nextQuantities;
            if (reachableQuantities.Count > MaximumQuantitySubsetStates)
                return QuantitySubsetAnalysis.NotFound;
        }

        return reachableQuantities.TryGetValue(targetQuantity, out var summary)
            ? new QuantitySubsetAnalysis(true, summary.RequiredIndexes, summary.PossibleIndexes)
            : QuantitySubsetAnalysis.NotFound;
    }

    private static TradeDuplicateKey CreateTradeDuplicateKey(
        DateOnly date,
        string secId,
        TradeSide side,
        decimal unitPrice) =>
        new(date, secId.Trim().ToUpperInvariant(), side, RoundToSixDecimals(unitPrice));

    private static decimal RoundToSixDecimals(double value) =>
        RoundToSixDecimals((decimal)value);

    private static decimal RoundToSixDecimals(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);

    private static HashSet<int> FindDuplicateCashFlowIndexes(
        IReadOnlyList<ParsedBrokerCashFlow> candidates,
        IReadOnlyList<PortfolioCashFlowRecord> existing)
    {
        var remainingCounts = existing
            .GroupBy(flow => CreateCashFlowDuplicateKey(flow.Date, flow.Type, (decimal)flow.Amount))
            .ToDictionary(group => group.Key, group => group.Count());
        var duplicates = new HashSet<int>();

        for (var index = 0; index < candidates.Count; index++)
        {
            var cashFlow = candidates[index];
            var type = cashFlow.IsDeposit ? PortfolioCashFlowType.Deposit : PortfolioCashFlowType.Withdrawal;
            var key = CreateCashFlowDuplicateKey(cashFlow.Date, type, cashFlow.Amount);
            if (!remainingCounts.TryGetValue(key, out var remaining) || remaining == 0)
                continue;

            duplicates.Add(index);
            remainingCounts[key] = remaining - 1;
        }

        return duplicates;
    }

    private static CashFlowDuplicateKey CreateCashFlowDuplicateKey(
        DateOnly date,
        PortfolioCashFlowType type,
        decimal amount) => new(date, type, RoundToSixDecimals(amount));

    private static Guid CreateBrokerTradeOperationId(string brokerTradeId) =>
        CreateStableOperationId($"tbank-trade:{brokerTradeId}");

    private static Guid CreateCashFlowOperationId(CashFlowDuplicateKey key, int occurrence) =>
        CreateStableOperationId(string.Join(':',
            "tbank-cash-flow",
            key.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            key.Type.ToString(),
            key.Amount.ToString("0.000000", CultureInfo.InvariantCulture),
            occurrence.ToString(CultureInfo.InvariantCulture)));

    private static Guid CreateStableOperationId(string sourceKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourceKey));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string? GetAmbiguousTradeWarning(TradeDuplicateAnalysis analysis, int index) =>
        analysis.AmbiguousIndexes.Contains(index)
            ? "В журнале есть операция с совпадающими реквизитами, но её нельзя однозначно сопоставить со строками отчёта. Проверьте перед добавлением."
            : null;

    private static string? CombineWarnings(params string?[] warnings)
    {
        var messages = warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)).ToArray();
        return messages.Length == 0 ? null : string.Join(" ", messages);
    }

    private static bool SameStoredNumber(double left, double right) =>
        RoundToSixDecimals(left) == RoundToSixDecimals(right);

    private static BrokerReportImportData ToImportData(BrokerReportOperationRequest operation)
    {
        if (operation.Date == default)
            throw new ArgumentException("Укажите дату каждой импортируемой операции.");

        var commission = operation.Commission ?? 0;
        if (!double.IsFinite(commission) || commission < 0)
            throw new ArgumentException("Комиссия должна быть неотрицательной.");

        switch (operation.Kind)
        {
            case BrokerReportOperationKind.BondTrade:
            {
                var secId = RequiredText(operation.SecId, "Укажите код облигации.");
                var boardId = RequiredText(operation.BoardId, "Укажите режим торгов облигации.");
                var currencyId = RequiredText(operation.CurrencyId, "Укажите валюту сделки.");
                var side = RequiredSide(operation.Side);
                var quantity = RequiredPositive(operation.Quantity, "Количество должно быть положительным.");
                var unitPrice = RequiredPositive(operation.UnitPrice, "Цена должна быть положительной.");
                var faceValue = RequiredPositive(operation.FaceValue, "Номинал облигации должен быть положительным.");
                var accruedInterestTotal = RequiredNonNegative(
                    operation.AccruedInterestTotal, "НКД должен быть неотрицательным.");
                var accruedInterest = accruedInterestTotal / quantity;
                var grossAmount = quantity * (unitPrice + accruedInterest);
                var commissionPercent = grossAmount == 0 ? 0 : commission / grossAmount * 100;
                var trade = new PortfolioTrade(secId, boardId, currencyId, operation.Date, side,
                    quantity, unitPrice, faceValue, accruedInterest, commissionPercent);
                return new BrokerReportImportData(trade, null, null, operation);
            }
            case BrokerReportOperationKind.FundOperation:
            {
                var secId = RequiredText(operation.SecId, "Укажите код фонда.");
                if (!RelationHelper.MoneyMarketFunds.ContainsKey(secId))
                    throw new ArgumentException("Поддерживаются только LQDT и TMON.");
                var side = RequiredSide(operation.Side);
                var quantity = RequiredPositive(operation.Quantity, "Количество паёв должно быть положительным.");
                if (quantity != Math.Truncate(quantity))
                    throw new ArgumentException("Количество паёв фонда должно быть целым числом.");
                var unitPrice = RequiredPositive(operation.UnitPrice, "Цена пая должна быть положительной.");
                var fundOperation = new MoneyMarketFundOperation(
                    secId, operation.Date, side, quantity, unitPrice, commission);
                return new BrokerReportImportData(null, fundOperation, null, operation);
            }
            case BrokerReportOperationKind.Deposit:
            case BrokerReportOperationKind.Withdrawal:
            {
                var amount = RequiredPositive(operation.Amount, "Сумма пополнения или вывода должна быть положительной.");
                var type = operation.Kind == BrokerReportOperationKind.Deposit
                    ? PortfolioCashFlowType.Deposit
                    : PortfolioCashFlowType.Withdrawal;
                var cashFlow = new PortfolioCashFlowInput(operation.Date, type, amount);
                return new BrokerReportImportData(null, null, cashFlow, operation);
            }
            default:
                throw new ArgumentException("Неизвестный тип операции в запросе импорта.");
        }
    }

    private static string RequiredText(string? value, string error) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(error) : value.Trim();

    private static TradeSide RequiredSide(TradeSide? value) =>
        value.HasValue && Enum.IsDefined(value.Value)
            ? value.Value
            : throw new ArgumentException("Выберите покупку или продажу.");

    private static double RequiredPositive(double? value, string error) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value > 0
            ? value.Value
            : throw new ArgumentException(error);

    private static double RequiredNonNegative(double? value, string error) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value >= 0
            ? value.Value
            : throw new ArgumentException(error);

    private sealed record LedgerState(
        IReadOnlyList<PortfolioTradeRecord> Trades,
        IReadOnlyList<MoneyMarketFundOperationRecord> FundOperations,
        IReadOnlyList<MoneyMarketFundRecord> Funds,
        IReadOnlyList<PortfolioCashFlowRecord> CashFlows);

    private readonly record struct TradeDuplicateKey(
        DateOnly Date,
        string SecId,
        TradeSide Side,
        decimal UnitPrice);

    private readonly record struct TradeDuplicateCandidate(
        int Index,
        TradeDuplicateKey Key,
        decimal Quantity,
        bool IsMoneyMarketFund);

    private readonly record struct StoredTradeQuantity(TradeDuplicateKey Key, decimal Quantity);

    private readonly record struct CashFlowDuplicateKey(
        DateOnly Date,
        PortfolioCashFlowType Type,
        decimal Amount);

    private sealed record TradeDuplicateAnalysis(
        HashSet<int> DuplicateIndexes,
        HashSet<int> AmbiguousIndexes);

    private sealed record QuantitySubsetAnalysis(
        bool Found,
        HashSet<int> RequiredIndexes,
        HashSet<int> PossibleIndexes)
    {
        public static QuantitySubsetAnalysis NotFound { get; } = new(false, [], []);
    }

    private sealed class QuantitySubsetSummary(
        HashSet<int> requiredIndexes,
        HashSet<int> possibleIndexes)
    {
        public HashSet<int> RequiredIndexes { get; } = requiredIndexes;
        public HashSet<int> PossibleIndexes { get; } = possibleIndexes;

        public QuantitySubsetSummary Clone() =>
            new(new HashSet<int>(RequiredIndexes), new HashSet<int>(PossibleIndexes));

        public QuantitySubsetSummary With(int index)
        {
            var required = new HashSet<int>(RequiredIndexes) { index };
            var possible = new HashSet<int>(PossibleIndexes) { index };
            return new QuantitySubsetSummary(required, possible);
        }

        public void Merge(QuantitySubsetSummary other)
        {
            RequiredIndexes.IntersectWith(other.RequiredIndexes);
            PossibleIndexes.UnionWith(other.PossibleIndexes);
        }
    }
}
