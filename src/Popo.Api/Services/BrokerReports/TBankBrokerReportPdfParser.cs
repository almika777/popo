using System.Globalization;
using System.Text.RegularExpressions;
using Popo.Core.Portfolio;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Popo.Api.Services.BrokerReports;

internal sealed record BrokerReportPdfWord(string Text, double X, double Y);
internal sealed record BrokerReportPdfLetter(string Text, double X, double Y);

public sealed record ParsedBrokerTrade(
    DateOnly TradeDate,
    TimeOnly TradeTime,
    string BrokerTradeId,
    string InstrumentCode,
    TradeSide Side,
    decimal ReportedPrice,
    string ReportedPriceUnit,
    decimal Quantity,
    decimal CleanAmount,
    decimal AccruedInterestTotal,
    decimal TotalAmount,
    string CurrencyId,
    decimal BrokerCommission,
    decimal ExchangeCommission,
    decimal ClearingCommission,
    string TradingMode)
{
    public decimal UnitPrice => CleanAmount / Quantity;

    public decimal Commission => BrokerCommission + ExchangeCommission + ClearingCommission;
}

public sealed record ParsedBrokerCashFlow(DateOnly Date, bool IsDeposit, decimal Amount);

public sealed record ParsedBrokerReport(
    IReadOnlyList<ParsedBrokerTrade> Trades,
    IReadOnlyList<ParsedBrokerCashFlow> CashFlows,
    int UnsupportedOperationsHidden);

public sealed class TBankBrokerReportPdfParser
{
    private const string TradeSectionHeading = "1.1 Информация о совершенных и исполненных сделках";
    private const string CashSectionHeading = "Операции с денежными средствами";
    private const double PageWidthPoints = 842;
    private const double TradingModeColumnStart = 783;
    private const double TradingModeColumnEnd = 803;
    private const double TradeIdColumnStart = 10;
    private const double TradeIdColumnEnd = 34;
    private static readonly Regex DatePattern = new(@"^\d{2}\.\d{2}\.\d{4}$", RegexOptions.Compiled);
    private static readonly Regex TimePattern = new(@"^\d{2}:\d{2}:\d{2}$", RegexOptions.Compiled);
    private static readonly Regex BrokerTradeIdPattern = new(@"^[0-9]{6,20}$", RegexOptions.Compiled);
    private static readonly Regex CurrencyPattern = new(@"^[A-Z]{3}$", RegexOptions.Compiled);
    private static readonly Regex NumberPattern = new(@"[-+]?\d[\d,]*(?:\.\d+)?", RegexOptions.Compiled);
    private static readonly Regex NumberedSectionPattern = new(@"(?m)^\s*(?<major>\d+)\.(?<minor>\d+)\s+[А-ЯЁ]", RegexOptions.Compiled);
    private static readonly Regex SectionMarkerPattern = new(@"^(?<major>\d+)\.(?<minor>\d+)?\.?(?:\s|$)", RegexOptions.Compiled);

    public ParsedBrokerReport Parse(Stream pdfStream)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        using var document = PdfDocument.Open(pdfStream);
        var trades = new List<ParsedBrokerTrade>();
        var cashFlows = new List<ParsedBrokerCashFlow>();
        var unsupportedOperationsHidden = 0;
        var foundTradeSection = false;
        var foundCashSection = false;
        var readingTradeSection = false;
        var readingCashSection = false;

        foreach (var page in document.GetPages())
        {
            var pageText = ContentOrderTextExtractor.GetText(page);
            var scale = PageWidthPoints / page.Width;
            var words = page.GetWords(NearestNeighbourWordExtractor.Instance)
                .Select(word => new BrokerReportPdfWord(
                    word.Text,
                    word.BoundingBox.BottomLeft.X * scale,
                    word.BoundingBox.BottomLeft.Y))
                .ToArray();
            var letters = page.Letters
                .Select(letter => new BrokerReportPdfLetter(
                    letter.Value,
                    letter.BoundingBox.BottomLeft.X * scale,
                    letter.BoundingBox.BottomLeft.Y))
                .ToArray();

            if (pageText.Contains(TradeSectionHeading, StringComparison.OrdinalIgnoreCase))
            {
                foundTradeSection = true;
                readingTradeSection = true;
            }

            if (readingTradeSection)
            {
                var tradeSectionEndY = FindSectionHeadingY(words, major: 1, minor: 2);
                var parsedPage = ParsePage(words, letters,
                    FindSectionHeadingY(words, major: 1, minor: 1), tradeSectionEndY);
                trades.AddRange(parsedPage.Trades);
                unsupportedOperationsHidden += parsedPage.UnsupportedOperationsHidden;
                if (tradeSectionEndY.HasValue || ContainsAnotherSection(pageText))
                    readingTradeSection = false;
            }

            if (pageText.Contains(CashSectionHeading, StringComparison.OrdinalIgnoreCase))
            {
                foundCashSection = true;
                readingCashSection = true;
            }

            if (readingCashSection)
            {
                var cashSectionStartY = FindSectionHeadingY(words, major: 2, minor: null);
                var cashSectionEndY = FindNextMajorSectionHeadingY(words, major: 2);
                cashFlows.AddRange(ParseCashFlowsPage(words, cashSectionStartY, cashSectionEndY));
                if (cashSectionEndY.HasValue || ContainsSectionAfterCashSection(pageText))
                    readingCashSection = false;
            }
        }

        if (!foundTradeSection)
            throw new InvalidDataException("В PDF не найден раздел с исполненными сделками.");

        if (!foundCashSection)
            throw new InvalidDataException("В PDF не найден раздел с операциями по денежным средствам.");

        return new ParsedBrokerReport(trades, cashFlows, unsupportedOperationsHidden);
    }

    private static (IReadOnlyList<ParsedBrokerTrade> Trades, int UnsupportedOperationsHidden) ParsePage(
        IEnumerable<BrokerReportPdfWord> sourceWords,
        IEnumerable<BrokerReportPdfLetter> sourceLetters,
        double? sectionStartY = null,
        double? sectionEndY = null)
    {
        var words = sourceWords.ToArray();
        var letters = sourceLetters.ToArray();
        var dateAnchors = words
            .Where(word => IsInColumn(word.X, 80, 115) && DatePattern.IsMatch(word.Text)
                && IsInsideSection(word.Y, sectionStartY, sectionEndY))
            .Where(date => words.Any(word => IsInColumn(word.X, 115, 147)
                && Math.Abs(word.Y - date.Y) < 0.5
                && TimePattern.IsMatch(word.Text)))
            .OrderByDescending(word => word.Y)
            .ToArray();
        var trades = new List<ParsedBrokerTrade>(dateAnchors.Length);
        var unsupportedOperationsHidden = 0;

        for (var index = 0; index < dateAnchors.Length; index++)
        {
            var anchor = dateAnchors[index];
            var nextRowY = dateAnchors.Skip(index + 1)
                .Select(word => (double?)word.Y)
                .FirstOrDefault(y => y.HasValue && y.Value < anchor.Y - 0.5);
            var lowerY = nextRowY.HasValue ? nextRowY.Value + 0.5 : anchor.Y - 27;
            var rowWords = words
                .Where(word => word.Y <= anchor.Y + 0.5 && word.Y > lowerY
                    && IsInsideSection(word.Y, sectionStartY, sectionEndY))
                .ToArray();
            var rowLetters = letters
                .Where(letter => letter.Y <= anchor.Y + 0.5 && letter.Y > lowerY
                    && IsInsideSection(letter.Y, sectionStartY, sectionEndY))
                .ToArray();

            var trade = ParseTradeRow(anchor, rowWords, rowLetters);
            if (trade is null)
                unsupportedOperationsHidden++;
            else
                trades.Add(trade);
        }

        return (trades, unsupportedOperationsHidden);
    }

    private static IReadOnlyList<ParsedBrokerCashFlow> ParseCashFlowsPage(
        IEnumerable<BrokerReportPdfWord> sourceWords,
        double? sectionStartY,
        double? sectionEndY)
    {
        var words = sourceWords.ToArray();
        var dateAnchors = words
            .Where(word => IsInColumn(word.X, 5, 100) && DatePattern.IsMatch(word.Text)
                && IsInsideSection(word.Y, sectionStartY, sectionEndY))
            .Where(date => words.Any(word => IsInColumn(word.X, 380, 496)
                && Math.Abs(word.Y - date.Y) < 1.2
                && !string.IsNullOrWhiteSpace(word.Text)))
            .OrderByDescending(word => word.Y)
            .ToArray();
        var flows = new List<ParsedBrokerCashFlow>();

        for (var index = 0; index < dateAnchors.Length; index++)
        {
            var anchor = dateAnchors[index];
            var nextRowY = dateAnchors.Skip(index + 1)
                .Select(word => (double?)word.Y)
                .FirstOrDefault(y => y.HasValue && y.Value < anchor.Y - 0.5);
            var lowerY = nextRowY.HasValue ? nextRowY.Value + 0.5 : anchor.Y - 30;
            var rowWords = words
                .Where(word => word.Y <= anchor.Y + 0.5 && word.Y > lowerY
                    && IsInsideSection(word.Y, sectionStartY, sectionEndY))
                .ToArray();

            string Cell(double left, double right) => string.Join(" ", rowWords
                .Where(word => IsInColumn(word.X, left, right))
                .OrderByDescending(word => word.Y)
                .ThenBy(word => word.X)
                .Select(word => word.Text));

            var operation = Cell(380, 496);
            var flowType = ClassifyCashFlow(operation);
            if (flowType is null)
                continue;

            var executionDateText = Cell(262, 380);
            var dateText = DatePattern.IsMatch(executionDateText)
                ? executionDateText
                : anchor.Text;
            if (!DateOnly.TryParseExact(dateText, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                throw new InvalidDataException("В строке денежной операции не удалось распознать дату исполнения.");
            }

            var creditedAmount = ReadOptionalNumber(Cell(496, 623));
            var debitedAmount = ReadOptionalNumber(Cell(623, 719));
            var amount = flowType.Value ? creditedAmount : debitedAmount;
            var oppositeAmount = flowType.Value ? debitedAmount : creditedAmount;
            if (amount <= 0 || oppositeAmount > 0)
                throw new InvalidDataException("В строке пополнения или вывода не удалось определить сумму в рублях.");

            flows.Add(new ParsedBrokerCashFlow(date, flowType.Value, amount));
        }

        return flows;
    }

    private static bool? ClassifyCashFlow(string operation)
    {
        var normalized = Regex.Replace(operation, @"\s+", " ").Trim().ToLowerInvariant();
        if (normalized.Contains("вывод", StringComparison.Ordinal)
            || normalized.Contains("снятие средств", StringComparison.Ordinal))
            return false;

        if (normalized.Contains("пополнение", StringComparison.Ordinal)
            || normalized.Contains("ввод средств", StringComparison.Ordinal)
            || normalized.Contains("ввод денежных средств", StringComparison.Ordinal)
            || normalized.Contains("внесение денежных средств", StringComparison.Ordinal)
            || normalized.Contains("зачисление денежных средств", StringComparison.Ordinal)
            || normalized.Contains("зачисление средств", StringComparison.Ordinal))
            return true;

        return null;
    }

    private static decimal ReadOptionalNumber(string cell)
    {
        var normalized = Normalize(cell);
        return string.IsNullOrWhiteSpace(normalized) || normalized is "-" or "—"
            ? 0
            : ReadNumber(normalized, "сумма денежной операции");
    }

    private static double? FindSectionHeadingY(
        IEnumerable<BrokerReportPdfWord> words,
        int major,
        int? minor)
    {
        return words
            .Where(word => SectionMarkerPattern.Match(word.Text) is { Success: true } match
                && int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture) == major
                && (minor is null
                    ? !match.Groups["minor"].Success
                    : match.Groups["minor"].Success
                        && int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture) == minor))
            .Select(word => (double?)word.Y)
            .FirstOrDefault();
    }

    private static double? FindNextMajorSectionHeadingY(IEnumerable<BrokerReportPdfWord> words, int major) => words
        .Where(word => SectionMarkerPattern.Match(word.Text) is { Success: true } match
            && int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture) > major)
        .OrderByDescending(word => word.Y)
        .Select(word => (double?)word.Y)
        .FirstOrDefault();

    private static bool IsInsideSection(double y, double? sectionStartY, double? sectionEndY) =>
        (!sectionStartY.HasValue || y < sectionStartY.Value)
        && (!sectionEndY.HasValue || y > sectionEndY.Value);

    private static bool ContainsSectionAfterCashSection(string pageText) => NumberedSectionPattern
        .Matches(pageText)
        .Cast<Match>()
        .Any(match => int.TryParse(match.Groups["major"].Value, out var major) && major > 2);

    private static ParsedBrokerTrade? ParseTradeRow(
        BrokerReportPdfWord dateAnchor,
        IReadOnlyList<BrokerReportPdfWord> rowWords,
        IReadOnlyList<BrokerReportPdfLetter> rowLetters)
    {
        string Cell(double left, double right) => string.Concat(rowWords
            .Where(word => IsInColumn(word.X, left, right))
            .OrderByDescending(word => word.Y)
            .ThenBy(word => word.X)
            .Select(word => word.Text));
        string LetterCell(double left, double right) => string.Concat(rowLetters
            .Where(letter => IsInColumn(letter.X, left, right))
            .OrderByDescending(letter => letter.Y)
            .ThenBy(letter => letter.X)
            .Select(letter => letter.Text));

        var brokerTradeId = Normalize(LetterCell(TradeIdColumnStart, TradeIdColumnEnd));
        var timeText = Cell(115, 147);
        var sideText = Normalize(Cell(163, 193));
        if (sideText.StartsWith("РЕПО", StringComparison.OrdinalIgnoreCase))
            return null;

        var instrumentCode = Normalize(Cell(220, 253));
        var priceCell = Normalize(Cell(253, 284));
        var priceUnitCell = Normalize(Cell(278, 298));
        var quantity = ReadNumber(Cell(297, 315), "количество");
        var cleanAmount = ReadNumber(Cell(315, 338), "сумма без НКД");
        var accruedInterest = ReadNumber(Cell(338, 365), "НКД");
        var totalAmount = ReadNumber(Cell(365, 389), "сумма сделки");
        var currencyId = Normalize(Cell(386, 409)).ToUpperInvariant();
        var reportedPrice = ReadLeadingNumber(priceCell, "цена");
        var priceUnit = priceCell.Contains('%', StringComparison.Ordinal)
            ? "%"
            : priceUnitCell.ToUpperInvariant();

        if (!DateOnly.TryParseExact(dateAnchor.Text, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var tradeDate)
            || !TimeOnly.TryParseExact(timeText, "HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var tradeTime))
        {
            throw new InvalidDataException("В строке сделки не удалось распознать дату или время.");
        }

        var side = sideText switch
        {
            "Покупка" => TradeSide.Buy,
            "Продажа" => TradeSide.Sell,
            _ => throw new InvalidDataException("В строке сделки указан неизвестный тип операции.")
        };

        var brokerCommission = ReadNumber(Cell(405, 430), "комиссия брокера");
        var exchangeCommission = ReadNumber(Cell(450, 473), "комиссия биржи");
        var clearingCommission = ReadNumber(Cell(493, 515), "комиссия клирингового центра");
        var tradingMode = Normalize(Cell(TradingModeColumnStart, TradingModeColumnEnd)).ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(instrumentCode)
            || !BrokerTradeIdPattern.IsMatch(brokerTradeId)
            || quantity <= 0
            || cleanAmount < 0
            || accruedInterest < 0
            || totalAmount < 0
            || reportedPrice <= 0
            || brokerCommission < 0
            || exchangeCommission < 0
            || clearingCommission < 0
            || !CurrencyPattern.IsMatch(currencyId)
            || (priceUnit != "%" && !CurrencyPattern.IsMatch(priceUnit))
            || Math.Abs(totalAmount - cleanAmount - accruedInterest) > 0.02m)
        {
            throw new InvalidDataException("В строке сделки отсутствуют корректные обязательные значения.");
        }

        return new ParsedBrokerTrade(
            tradeDate,
            tradeTime,
            brokerTradeId,
            instrumentCode,
            side,
            reportedPrice,
            priceUnit,
            quantity,
            cleanAmount,
            accruedInterest,
            totalAmount,
            currencyId,
            brokerCommission,
            exchangeCommission,
            clearingCommission,
            tradingMode);
    }

    private static decimal ReadNumber(string cell, string field)
    {
        var normalized = Normalize(cell);
        if (!decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException($"Не удалось распознать поле «{field}» в строке сделки.");
        }

        return value;
    }

    private static decimal ReadLeadingNumber(string cell, string field)
    {
        var match = NumberPattern.Match(cell);
        return match.Success
            ? ReadNumber(match.Value, field)
            : throw new InvalidDataException($"Не удалось распознать поле «{field}» в строке сделки.");
    }

    private static string Normalize(string value) => Regex.Replace(value, @"\s+", string.Empty);

    private static bool IsInColumn(double x, double left, double right) => x >= left && x < right;

    private static bool ContainsAnotherSection(string pageText) => NumberedSectionPattern
        .Matches(pageText)
        .Cast<Match>()
        .Any(match => match.Groups["major"].Value != "1" || match.Groups["minor"].Value != "1");
}
