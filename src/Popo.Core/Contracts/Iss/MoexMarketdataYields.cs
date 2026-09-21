namespace Popo.Core.Contracts.Iss;

public class MoexMarketdataYields
{
    /// <summary>
    /// Код бумаги на МОЕХ
    /// </summary>
    public string SecId { get; set; } = null!;
        
    /// <summary>
    /// Режим торгов
    /// </summary>
    public string BoardId { get; set; } = null!;
    
    /// <summary>
    /// Цена по которой была рассчитана доходность
    /// </summary>
    public double Price { get; set; }

    /// <summary>
    /// Дата, к которой рассчитывается доходность
    /// </summary>
    public DateTimeOffset? YieldDate { get; set; }       

    /// <summary>
    /// Тип даты, к которой рассчитывается доходность параметра (offer, maturity, MBS)
    /// </summary>
    public string? YieldDateType { get; set; }    
        
    /// <summary>
    /// Эффективная доходность
    /// </summary>
    public double? EffectiveYield { get; set; }

    /// <summary>
    /// Дюрация
    /// </summary>
    public int? Duration { get; set; }

    /// <summary>
    /// Z-спред, базисные пункты
    /// </summary>
    public double? ZSpread { get; set; }

    /// <summary>
    /// G-спред, базисные пункты
    /// </summary>
    public double? GSpread { get; set; }

    /// <summary>
    /// Средневзвешенная цена
    /// </summary>
    public double WaPrice { get; set; }

    /// <summary>
    /// Эффективная доходность по средневзвешенной цене
    /// </summary>
    public double? EffectiveYieldWaPrice { get; set; }

    /// <summary>
    /// Дюрация по средневзвешенной цене, дней
    /// </summary>
    public double? DurationWaPrice { get; set; }

    /// <summary>
    /// Момент совершения сделки
    /// </summary>
    public DateTimeOffset? TradeMoment { get; set; }
    
    /// <summary>
    /// Порядковый номер записи
    /// </summary>
    public long SeqNum { get; set; }    
}



