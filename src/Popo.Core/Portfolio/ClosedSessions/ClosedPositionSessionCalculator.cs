namespace Popo.Core.Portfolio.ClosedSessions;

public sealed class ClosedPositionSessionCalculator
{
    public IReadOnlyList<ClosedPositionSession> Calculate(ClosedSessionInstrument instrument,
        DateTimeOffset calculatedAt)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        var results = new List<ClosedPositionSession>();
        Session? session = null;
        foreach (var day in instrument.Trades.OrderBy(x => x.Date).ThenBy(x => x.Id).GroupBy(x => x.Date))
        {
            foreach (var trade in day)
            {
                if (trade.Quantity <= 0 || trade.Price < 0 || trade.Commission < 0
                    || trade.Side is not (TradeSide.Buy or TradeSide.Sell))
                    throw new InvalidOperationException($"Некорректная сделка {trade.Id} по {instrument.SecId}.");
            }

            session?.Advance(day.Key);
            var deferredSales = new List<ClosedSessionTrade>();
            foreach (var sale in day.Where(x => x.Side == TradeSide.Sell))
            {
                var available = Math.Min(session?.Quantity ?? 0, sale.Quantity);
                if (available > 0)
                {
                    ApplySale(sale with
                    {
                        Quantity = available,
                        Commission = sale.Commission * available / sale.Quantity
                    });
                }
                if (available < sale.Quantity)
                    deferredSales.Add(sale with
                    {
                        Quantity = sale.Quantity - available,
                        Commission = sale.Commission * (sale.Quantity - available) / sale.Quantity
                    });
            }
            foreach (var buy in day.Where(x => x.Side == TradeSide.Buy))
            {
                session ??= new Session(buy.Id, buy.Date);
                session.Trades.Add(buy);
                session.Quantity += buy.Quantity;
                session.BoughtQuantity += buy.Quantity;
                session.BoughtCleanAmount += buy.Quantity * buy.Price;
                session.Commission += buy.Commission;
            }
            foreach (var sale in deferredSales)
                ApplySale(sale);
        }
        return results;

        void ApplySale(ClosedSessionTrade sale)
        {
            if (session is null || sale.Quantity > session.Quantity)
                throw new InvalidOperationException(
                    $"Продажа {instrument.SecId} на {sale.Date:yyyy-MM-dd} превышает доступное количество.");
            session.Trades.Add(sale);
            session.Quantity -= sale.Quantity;
            session.SoldCleanAmount += sale.Quantity * sale.Price;
            session.Commission += sale.Commission;
            if (session.Quantity != 0)
                return;
            results.Add(Complete(instrument, session, sale.Date, calculatedAt));
            session = null;
        }
    }

    private static ClosedPositionSession Complete(ClosedSessionInstrument instrument, Session session,
        DateOnly endDate, DateTimeOffset calculatedAt)
    {
        var calculator = new ClosedSessionBondCalculatorFactory().Create(instrument);
        var sessionInstrument = instrument with { Trades = session.Trades };
        var coupon = calculator.CalculateCouponIncome(sessionInstrument, session.Holdings);
        var amortization = new ClosedSessionAmortizationCalculator().Calculate(sessionInstrument, session.Holdings);
        var body = session.SoldCleanAmount + amortization.Amount - session.BoughtCleanAmount;
        var total = body + coupon.Amount - session.Commission;
        var percent = session.BoughtCleanAmount > 0 ? total / session.BoughtCleanAmount * 100 : null;
        var days = endDate.DayNumber - session.StartDate.DayNumber;
        var annualized = days > 0 ? percent * 365 / days : null;
        var reasons = new[] { coupon.IncompleteReason, amortization.IncompleteReason }
            .Where(x => !string.IsNullOrEmpty(x));
        return new ClosedPositionSession(session.Id, instrument.SecId, instrument.BoardId,
            instrument.CurrencyId, instrument.ShortName, session.StartDate, endDate, days,
            session.BoughtQuantity, session.BoughtCleanAmount, session.SoldCleanAmount, session.Commission,
            amortization.Amount, body, coupon.Amount, total, percent, annualized,
            coupon.Amount.HasValue && amortization.Amount.HasValue,
            string.Join(" ", reasons) is { Length: > 0 } reason ? reason : null,
            calculator.Kind, calculatedAt);
    }

    private sealed class Session(Guid id, DateOnly startDate)
    {
        public Guid Id { get; } = id;
        public DateOnly StartDate { get; } = startDate;
        public decimal Quantity { get; set; }
        public decimal BoughtQuantity { get; set; }
        public decimal BoughtCleanAmount { get; set; }
        public decimal SoldCleanAmount { get; set; }
        public decimal Commission { get; set; }
        public List<ClosedSessionHolding> Holdings { get; } = [];
        public List<ClosedSessionTrade> Trades { get; } = [];
        private DateOnly _lastDate = startDate;

        public void Advance(DateOnly date)
        {
            if (date > _lastDate && Quantity > 0)
                Holdings.Add(new ClosedSessionHolding(_lastDate, date, Quantity));
            _lastDate = date;
        }
    }
}
