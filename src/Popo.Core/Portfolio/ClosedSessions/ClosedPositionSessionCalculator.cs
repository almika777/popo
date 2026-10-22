using Popo.Core.Extensions;

namespace Popo.Core.Portfolio.ClosedSessions;

/// <summary>Строит закрытые сессии владения облигацией и рассчитывает их финансовый результат.</summary>
/// <remarks>
/// Сессия начинается первой покупкой и заканчивается продажей всего остатка.
/// Построение сессий отделено от расчёта купонов, амортизаций и итоговых показателей.
/// </remarks>
public sealed class ClosedPositionSessionCalculator
{
    /// <summary>Разбивает сделки инструмента на завершённые сессии и рассчитывает результат каждой сессии.</summary>
    /// <param name="instrument">Инструмент со сделками, историей выплат и валютными курсами.</param>
    /// <param name="calculatedAt">Время расчёта, записываемое в каждый результат.</param>
    /// <returns>Закрытые сессии по хронологии сделок. Незакрытый остаток в результат не входит.</returns>
    /// <exception cref="ArgumentNullException">Инструмент не передан.</exception>
    /// <exception cref="InvalidOperationException">Продажа превышает остаток с учётом покупок того же дня.</exception>
    public IReadOnlyList<ClosedPositionSession> Calculate(
        ClosedSessionInstrument instrument,
        DateTimeOffset calculatedAt)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        return BuildClosedSessions(instrument)
            .Select(closedSession => CalculateSessionResult(
                instrument, closedSession.Session, closedSession.EndDate, calculatedAt))
            .ToList();
    }

    /// <summary>Восстанавливает количество бумаг по дням и выдаёт сессию при полном закрытии остатка.</summary>
    /// <remarks>
    /// Перед сделками новой даты фиксируется владение прежним количеством до этой даты исключительно.
    /// После закрытия следующая покупка открывает отдельную сессию, даже если произошла в тот же день.
    /// </remarks>
    /// <param name="instrument">Инструмент, сделки которого нужно распределить по сессиям.</param>
    /// <returns>Завершённые сессии с датами закрытия; последняя открытая сессия пропускается.</returns>
    private static IEnumerable<(Session Session, DateOnly EndDate)> BuildClosedSessions(
        ClosedSessionInstrument instrument)
    {
        Session? activeSession = null;
        var tradingDays = instrument.Trades
            .OrderBy(trade => trade.Date)
            .ThenBy(trade => trade.Id)
            .GroupBy(trade => trade.Date);

        foreach (var day in tradingDays)
        {
            activeSession?.RecordHoldingUntil(day.Key);

            foreach (var trade in OrderDailyTrades(day, activeSession?.Quantity ?? 0))
            {
                if (trade.Side == TradeSide.Buy)
                {
                    activeSession ??= new Session(trade.Id, trade.Date);
                    activeSession.AddPurchase(trade);
                    continue;
                }

                if (activeSession is null || trade.Quantity > activeSession.Quantity)
                    throw new InvalidOperationException(
                        $"Продажа {instrument.SecId} на {trade.Date:yyyy-MM-dd} превышает доступное количество.");

                activeSession.AddSale(trade);
                if (activeSession.Quantity != 0)
                    continue;

                yield return (activeSession, trade.Date);
                activeSession = null;
            }
        }
    }

    /// <summary>Задаёт порядок сделок одного дня без использования времени их исполнения.</summary>
    /// <remarks>
    /// Сначала продаётся остаток предыдущих дней, затем учитываются покупки.
    /// Часть продаж сверх прежнего остатка выполняется после покупок этого дня.
    /// Например, при остатке 10 и продаже 15 сначала продаются 10, а оставшиеся 5 относятся к новым покупкам.
    /// </remarks>
    /// <param name="trades">Сделки одной даты, уже упорядоченные по ID.</param>
    /// <param name="openingQuantity">Количество бумаг до сделок этого дня.</param>
    /// <returns>Продажи прежнего остатка, покупки и отложенные части продаж в порядке обработки.</returns>
    private static IEnumerable<ClosedSessionTrade> OrderDailyTrades(
        IEnumerable<ClosedSessionTrade> trades,
        double openingQuantity)
    {
        var remainingOpeningQuantity = openingQuantity;
        var deferredSales = new List<ClosedSessionTrade>();

        foreach (var sale in trades.Where(trade => trade.Side == TradeSide.Sell))
        {
            var quantityFromOpeningBalance = Math.Min(remainingOpeningQuantity, sale.Quantity);
            if (quantityFromOpeningBalance > 0)
            {
                remainingOpeningQuantity -= quantityFromOpeningBalance;
                yield return CreateSalePortion(sale, quantityFromOpeningBalance);
            }

            if (quantityFromOpeningBalance < sale.Quantity)
                deferredSales.Add(CreateSalePortion(sale, sale.Quantity - quantityFromOpeningBalance));
        }

        foreach (var purchase in trades.Where(trade => trade.Side == TradeSide.Buy))
            yield return purchase;

        foreach (var sale in deferredSales)
            yield return sale;
    }

    /// <summary>Выделяет часть продажи и распределяет её комиссию пропорционально количеству.</summary>
    /// <param name="sale">Исходная продажа, цена и остальные реквизиты которой сохраняются.</param>
    /// <param name="quantity">Количество бумаг, относящееся к одной из сессий.</param>
    /// <returns>Часть исходной продажи с соответствующей долей комиссии.</returns>
    private static ClosedSessionTrade CreateSalePortion(ClosedSessionTrade sale, double quantity)
        => sale with
        {
            Quantity = quantity,
            Commission = sale.Commission * quantity / sale.Quantity
        };

    /// <summary>Рассчитывает купоны, амортизации, результат по телу и доходность закрытой сессии.</summary>
    /// <remarks>
    /// Итог равен чистой выручке плюс амортизации и купоны минус чистая стоимость покупок и комиссии.
    /// Годовая доходность — линейная экстраполяция доходности за период; при нулевом сроке она отсутствует.
    /// Неизвестный купон или амортизация оставляет зависящие от него показатели неизвестными.
    /// </remarks>
    /// <param name="instrument">Данные инструмента для выбора калькулятора и расчёта выплат.</param>
    /// <param name="session">Количество, сделки и суммы завершённой сессии.</param>
    /// <param name="endDate">Дата продажи последнего остатка.</param>
    /// <param name="calculatedAt">Время расчёта, сохраняемое в результате.</param>
    /// <returns>Показатели сессии и сведения о полноте исходных данных.</returns>
    private static ClosedPositionSession CalculateSessionResult(
        ClosedSessionInstrument instrument,
        Session session,
        DateOnly endDate,
        DateTimeOffset calculatedAt)
    {
        var calculator = new ClosedSessionBondCalculatorFactory().Create(instrument);
        var sessionInstrument = instrument with { Trades = session.Trades };
        var coupon = calculator.CalculateCouponIncome(sessionInstrument, session.Holdings);
        var amortization = new ClosedSessionAmortizationCalculator().Calculate(sessionInstrument, session.Holdings);

        var bodyPnl = session.SoldCleanAmount + amortization.Amount - session.BoughtCleanAmount;
        var totalPnl = bodyPnl + coupon.Amount - session.Commission;
        var totalPnlPercent = session.BoughtCleanAmount > 0 ? totalPnl / session.BoughtCleanAmount * 100 : null;
        var holdingDays = endDate.DayNumber - session.StartDate.DayNumber;
        var annualizedPnlPercent = holdingDays > 0 ? totalPnlPercent * 365 / holdingDays : null;
        var incompleteReason = string.Join(" ",
            new[] { coupon.IncompleteReason, amortization.IncompleteReason }
                .Where(reason => !string.IsNullOrEmpty(reason)));

        return new ClosedPositionSession(
            Id: session.Id,
            SecId: instrument.SecId,
            BoardId: instrument.BoardId,
            CurrencyId: instrument.CurrencyId,
            ShortName: instrument.ShortName,
            StartDate: session.StartDate,
            EndDate: endDate,
            HoldingDays: holdingDays,
            BoughtQuantity: session.BoughtQuantity,
            BoughtCleanAmount: session.BoughtCleanAmount,
            SoldCleanAmount: session.SoldCleanAmount,
            Commission: session.Commission.Round(),
            AmortizationIncome: amortization.Amount.Round(),
            BodyPnl: bodyPnl.Round(),
            CouponIncome: coupon.Amount.Round(),
            TotalPnl: totalPnl.Round(),
            TotalPnlPercent: totalPnlPercent.Round(),
            AnnualizedPnlPercent: annualizedPnlPercent.Round(),
            IsComplete: coupon.Amount.HasValue && amortization.Amount.HasValue,
            IncompleteReason: incompleteReason.Length > 0 ? incompleteReason : null,
            CalculatorKind: calculator.Kind,
            CalculatedAt: calculatedAt);
    }

    /// <summary>Хранит состояние одной сессии и согласованно изменяет количество, суммы и историю сделок.</summary>
    private sealed class Session(Guid id, DateOnly startDate)
    {
        private readonly List<ClosedSessionHolding> _holdings = [];
        private readonly List<ClosedSessionTrade> _trades = [];
        private DateOnly _lastHoldingDate = startDate;

        /// <summary>Идентификатор первой покупки сессии.</summary>
        public Guid Id { get; } = id;

        /// <summary>Дата первой покупки сессии.</summary>
        public DateOnly StartDate { get; } = startDate;

        /// <summary>Текущий непроданный остаток бумаг.</summary>
        public double Quantity { get; private set; }

        /// <summary>Суммарное количество всех покупок сессии, включая последующие докупки.</summary>
        public double BoughtQuantity { get; private set; }

        /// <summary>Чистая стоимость всех покупок без НКД и комиссий.</summary>
        public double BoughtCleanAmount { get; private set; }

        /// <summary>Чистая выручка всех продаж без НКД и комиссий.</summary>
        public double SoldCleanAmount { get; private set; }

        /// <summary>Сумма комиссий покупок и продаж, относящихся к сессии.</summary>
        public double Commission { get; private set; }

        /// <summary>Интервалы владения для расчёта купонов и амортизаций.</summary>
        public IReadOnlyList<ClosedSessionHolding> Holdings => _holdings;

        /// <summary>Сделки сессии, включая выделенные части продаж.</summary>
        public IReadOnlyList<ClosedSessionTrade> Trades => _trades;

        /// <summary>Увеличивает остаток и накапливает количество, чистую стоимость и комиссию покупки.</summary>
        /// <param name="purchase">Покупка, целиком относящаяся к этой сессии.</param>
        public void AddPurchase(ClosedSessionTrade purchase)
        {
            _trades.Add(purchase);
            Quantity += purchase.Quantity;
            BoughtQuantity += purchase.Quantity;
            BoughtCleanAmount += purchase.Quantity * purchase.Price;
            Commission += purchase.Commission;
        }

        /// <summary>Уменьшает остаток и накапливает чистую выручку и комиссию продажи.</summary>
        /// <param name="sale">Продажа в пределах доступного остатка; количество проверяет построитель сессий.</param>
        public void AddSale(ClosedSessionTrade sale)
        {
            _trades.Add(sale);
            Quantity -= sale.Quantity;
            SoldCleanAmount += sale.Quantity * sale.Price;
            Commission += sale.Commission;
        }

        /// <summary>Записывает владение текущим количеством на интервале от последней даты до date, не включая date.</summary>
        /// <remarks>
        /// Вызывается до обработки сделок новой даты: день покупки входит в срок владения, день продажи исключён.
        /// Например, 10 бумаг с 1 до 4 числа дают три дня владения этим количеством.
        /// </remarks>
        /// <param name="date">Дата следующего изменения остатка; правая граница интервала не включается.</param>
        public void RecordHoldingUntil(DateOnly date)
        {
            if (date > _lastHoldingDate && Quantity > 0)
                _holdings.Add(new ClosedSessionHolding(_lastHoldingDate, date, Quantity));
            _lastHoldingDate = date;
        }
    }
}
