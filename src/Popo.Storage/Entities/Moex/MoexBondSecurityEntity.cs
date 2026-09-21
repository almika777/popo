namespace Popo.Storage.Entities.Moex;

public class MoexBondSecurityEntity
{
    /// <summary>Код инструмента. Идентификатор финансового инструмента</summary>
    public string SecId { get; set; } = string.Empty;

    /// <summary>Кратк. наим. Краткое наименование ценной бумаги</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Средневзвешенная цена предыдущего дня, % к номиналу</summary>
    public double? PrevWaPrice { get; set; }

    /// <summary>Доходность по оценке пред. дня</summary>
    public double? YieldAtPrevWaPrice { get; set; }

    /// <summary>Сумма купона, в валюте номинала</summary>
    public double CouponValue { get; set; }

    /// <summary>Дата окончания купона</summary>
    public DateOnly? NextCouponDate { get; set; }

    /// <summary>НКД на дату расчетов, в валюте расчетов</summary>
    public double? AccruedInt { get; set; }

    /// <summary>Цена последней сделки пред. дня, % к номиналу</summary>
    public double? PrevPrice { get; set; }

    /// <summary>Размер лота, ц.б.</summary>
    public long LotSize { get; set; }

    /// <summary>Непогашенный долг</summary>
    public double FaceValue { get; set; }

    /// <summary>Режим торгов</summary>
    public string BoardName { get; set; } = string.Empty;

    /// <summary>Идентификатор режима торгов</summary>
    public string BoardId { get; set; } = string.Empty;

    /// <summary>Статус</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Дата погашения</summary>
    public DateOnly? MatDate { get; set; }

    /// <summary>Точность, знаков после запятой</summary>
    public int Decimals { get; set; }

    /// <summary>Длительность купона</summary>
    public int CouponPeriod { get; set; }

    /// <summary>Объем выпуска, штук</summary>
    public long? IssueSize { get; set; }

    /// <summary>Официальная цена закрытия предыдущего дня</summary>
    public double? PrevLegalClosePrice { get; set; }

    /// <summary>Дата предыдущего торгового дня</summary>
    public DateOnly? PrevTradeDate { get; set; }

    /// <summary>Наименование финансового инструмента</summary>
    public string? SecName { get; set; }

    /// <summary>Примечание</summary>
    public string? Remarks { get; set; }

    /// <summary>Рынок</summary>
    public string? MarketCode { get; set; }

    /// <summary>Группа инструментов</summary>
    public string? InstrId { get; set; }

    /// <summary>Сектор (Устарело)</summary>
    public string? SectorId { get; set; }

    /// <summary>Мин. шаг цены</summary>
    public double? MinStep { get; set; }

    /// <summary>Валюта номинала облигации. Не является валютой торгов.</summary>
    public string FaceUnit { get; set; } = string.Empty;

    /// <summary>Цена оферты</summary>
    public double? BuybackPrice { get; set; }

    /// <summary>Дата, к которой рассчитывается доходность</summary>
    public DateOnly? BuybackDate { get; set; }

    /// <summary>ISIN</summary>
    public string Isin { get; set; } = null!;

    /// <summary>Англ. наименование</summary>
    public string? LatName { get; set; }

    /// <summary>Регистрационный номер</summary>
    public string? RegNumber { get; set; }

    /// <summary>Валюта торгов и расчётов по сделкам. Для денежных потоков использовать это поле, а не FaceUnit.</summary>
    public string? CurrencyId { get; set; }

    /// <summary>Количество ценных бумаг в обращении</summary>
    public long? IssueSizePlaced { get; set; }

    /// <summary>Уровень листинга</summary>
    public int? ListLevel { get; set; }

    /// <summary>Тип ценной бумаги</summary>
    public string? SecType { get; set; }

    /// <summary>Ставка купона, %</summary>
    public double? CouponPercent { get; set; }

    /// <summary>Дата Оферты</summary>
    public DateOnly? OfferDate { get; set; }

    /// <summary>Дата расчетов сделки</summary>
    public DateOnly? SettleDate { get; set; }

    /// <summary>Номинальная стоимость лота, в валюте номинала</summary>
    public double? LotValue { get; set; }

    /// <summary>Номинальная стоимость на дату расчетов</summary>
    public double? FaceValueOnSettleDate { get; set; }

    /// <summary>Дата колл-опциона</summary>
    public DateOnly? CallOptionDate { get; set; }

    /// <summary>Дата пут-опциона</summary>
    public DateOnly? PutOptionDate { get; set; }

    /// <summary>Дата, указанная Эмитентом для расчета доходности</summary>
    public DateOnly? DateYieldFromIssuer { get; set; }

    /// <summary>Вид облигации</summary>
    public string? BondType { get; set; }

    /// <summary>Подвид облигации</summary>
    public string? BondSubType { get; set; }

    /// <summary>Дата и время обновления записи</summary>
    public DateTimeOffset Updated { get; set; }
}

