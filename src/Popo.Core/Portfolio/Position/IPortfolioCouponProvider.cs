namespace Popo.Core.Portfolio.Position;

/// <summary>Читает сохранённые купонные периоды облигаций.</summary>
public interface IPortfolioCouponProvider
{
    /// <summary>Возвращает историю и текущий период выбранных выпусков на дату расчёта.</summary>
    Task<IReadOnlyList<OpenPositionCoupon>> GetCouponScheduleAsync(
        IReadOnlyCollection<string> secIds, DateOnly asOf, CancellationToken cancellationToken);
}
