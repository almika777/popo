namespace Popo.Core.Contracts.Iss;

public class MoexMarketdata
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
    /// Эффективная доходность по последней реальной сделке внутри дня.
    /// </summary>
    public double Yield { get; set; }

    /// <summary>
    /// Спрос
    /// </summary>
    public double? Bid { get; set; }

    /// <summary>
    /// Цена последней сделки, %
    /// </summary>
    public double? Last { get; set; }

    /// <summary>
    /// Текущая цена
    /// </summary>
    public double? LCurrentPrice { get; set; }

    /// <summary>
    /// Рыночная цена 2
    /// </summary>
    public double? MarketPrice2 { get; set; }

    /// <summary>
    /// Рыночная цена
    /// </summary>
    public double? MarketPrice { get; set; }

    /// <summary>
    /// Цена закрытия
    /// </summary>
    public double? LClosePrice { get; set; }

    /// <summary>
    /// Цена закрытия
    /// </summary>
    public double? ClosePrice { get; set; }
    
    /// <summary>
    /// Дюрация, дни
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Время загрузки данных системой
    /// </summary>
    public DateTime? SysTime { get; set; }

    
    /// <summary>
    /// Текущая цена
    /// </summary>
    public double? CurrentPrice => LCurrentPrice
                                   ?? MarketPrice2
                                   ?? MarketPrice
                                   ?? LClosePrice
                                   ?? ClosePrice
                                   ?? Last;

}
