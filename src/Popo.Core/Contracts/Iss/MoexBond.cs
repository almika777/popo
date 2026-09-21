namespace Popo.Core.Contracts.Iss;

/// <summary>
/// Модель для информации по облигации (данные MOEX)
/// </summary>
public class MoexBond
{
    /// <summary>
    /// Код ценной бумаги
    /// </summary>
    public string SecId { get; set; } = string.Empty;

    /// <summary>
    /// Наименование ценной бумаги
    /// </summary>
    public string IssueName { get; set; } = string.Empty;

    /// <summary>
    /// Полное наименование
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Краткое наименование
    /// </summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// ISIN код
    /// </summary>
    public string Isin { get; set; } = string.Empty;

    /// <summary>
    /// Дата начала торгов
    /// </summary>
    public DateOnly? IssueDate { get; set; }

    /// <summary>
    /// Дата погашения
    /// </summary>
    public DateOnly? MatDate { get; set; }

    /// <summary>
    /// Первоначальная номинальная стоимость
    /// </summary>
    public double InitialFaceValue { get; set; }

    /// <summary>
    /// Валюта номинала
    /// </summary>
    public string FaceUnit { get; set; } = string.Empty;

    /// <summary>
    /// Английское наименование
    /// </summary>
    public string LatName { get; set; } = string.Empty;

    /// <summary>
    /// Дата начала торгов на Московской Бирже
    /// </summary>
    public DateOnly? StartDateMoex { get; set; }

    /// <summary>
    /// Наличие проспекта
    /// </summary>
    public bool? HasProspectus { get; set; }

    /// <summary>
    /// Дата принятия решения организатором торговли о включении ценной бумаги в Список
    /// </summary>
    public DateOnly? DecisionDate { get; set; }

    /// <summary>
    /// Облигации размещены с целью финансирования соглашений о партнерстве
    /// </summary>
    public bool? IsConcessionAgreement { get; set; }

    /// <summary>
    /// Допущен дефолт
    /// </summary>
    public bool? HasDefault { get; set; }

    /// <summary>
    /// Допущен технический дефолт
    /// </summary>
    public bool? HasTechnicalDefault { get; set; }

    /// <summary>
    /// Эмитент не соответствует требованию на текущий Список
    /// </summary>
    public int? EmitentMismatchCur { get; set; }

    /// <summary>
    /// Уровень листинга
    /// </summary>
    public int ListLevel { get; set; }

    /// <summary>
    /// ИЦБ допущена к орг. торгам по инициативе биржи
    /// </summary>
    public bool IncludedByMoex { get; set; }

    /// <summary>
    /// Дней до погашения
    /// </summary>
    public int DaysToRedemption { get; set; }

    /// <summary>
    /// Объем выпуска
    /// </summary>
    public long IssueSize { get; set; }

    /// <summary>
    /// Номинальная стоимость
    /// </summary>
    public double FaceValue { get; set; }

    /// <summary>
    /// Бумаги для квалифицированных инвесторов
    /// </summary>
    public bool IsQualifiedInvestors { get; set; }

    /// <summary>
    /// Периодичность выплаты купона в год
    /// </summary>
    public int CouponFrequency { get; set; }

    /// <summary>
    /// Дата выплаты купона
    /// </summary>
    public DateOnly? CouponDate { get; set; }

    /// <summary>
    /// Ставка купона, %
    /// </summary>
    public double? CouponPercent { get; set; }

    /// <summary>
    /// Сумма купона, в валюте номинала
    /// </summary>
    public double CouponValue { get; set; }

    /// <summary>
    /// Допуск к утренней дополнительной торговой сессии
    /// </summary>
    public bool MorningSession { get; set; }

    /// <summary>
    /// Допуск к вечерней дополнительной торговой сессии
    /// </summary>
    public bool EveningSession { get; set; }

    /// <summary>
    /// Допуск к дополнительной торговой сессии выходного дня
    /// </summary>
    public bool WeekendSession { get; set; }

    /// <summary>
    /// Вид облигации
    /// </summary>
    public string? BondKind { get; set; }

    /// <summary>
    /// Подвид облигации
    /// </summary>
    public string? BondSubType { get; set; }

    /// <summary>
    /// Вид/категория ценной бумаги
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Код типа инструмента
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Тип бумаги
    /// </summary>
    public string? SecurityType { get; set; }

    /// <summary>
    /// Тип инструмента
    /// </summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// Код эмитента
    /// </summary>
    public long? EmitterId { get; set; }

    // ====== Поля из других режимов MOEX ======

    /// <summary>
    /// Номер государственной регистрации выпуска
    /// </summary>
    public string? RegNumber { get; set; }

    /// <summary>
    /// Полное наименование (из режима торгов)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Цена последней сделки предыдущего торгового дня
    /// </summary>
    public double? PrevPrice { get; set; }

    /// <summary>
    /// Значение оценки предыдущего торгового дня
    /// </summary>
    public double? PrevWaPrice { get; set; }

    /// <summary>
    /// Доходность по средневзвешенной цене предыдущего торгового дня
    /// </summary>
    public double? YieldAtPrevWaPrice { get; set; }

    /// <summary>
    /// Идентификатор режима торгов
    /// </summary>
    public string BoardId { get; set; } = string.Empty;

    /// <summary>
    /// Валюта
    /// </summary>
    public string CurrencyId { get; set; } = string.Empty;

    /// <summary>
    /// Дата предыдущего торгового дня
    /// </summary>
    public DateOnly? PrevTradeDate { get; set; }

    /// <summary>
    /// НКД (накопленный купонный доход)
    /// </summary>
    public double Accruedint { get; set; }

    /// <summary>
    /// Дата следующего купона
    /// </summary>
    public DateOnly? NextCouponDate { get; set; }

    /// <summary>
    /// Длительность купона, выраженная в днях
    /// </summary>
    public int CouponPeriod { get; set; }

    /// <summary>
    /// Если указана, то расчет доходности производится с использованием этой даты
    /// </summary>
    public DateOnly? BuybackDate { get; set; }

    /// <summary>
    /// Дата оферты
    /// </summary>
    public DateOnly? OfferDate { get; set; }

    /// <summary>
    /// Количество размещенных ценных бумаг, находящихся в обращении
    /// </summary>
    public long? IssueSizePlaced { get; set; }

    /// <summary>
    /// Тип облигации (например "Флоатер", "Дисконт", "Купонная")
    /// </summary>
    public string? BondType { get; set; }

    /// <summary>
    /// Размер лота, ц.б.
    /// </summary>
    public long LotSize { get; set; }

    /// <summary>
    /// Режим торгов
    /// </summary>
    public string BoardName { get; set; } = string.Empty;

    /// <summary>
    /// Статус
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Точность, знаков после запятой
    /// </summary>
    public int Decimals { get; set; }

    /// <summary>
    /// Официальная цена закрытия предыдущего дня
    /// </summary>
    public double? PrevLegalClosePrice { get; set; }

    /// <summary>
    /// Примечание
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Рынок
    /// </summary>
    public string? MarketCode { get; set; }

    /// <summary>
    /// Группа инструментов
    /// </summary>
    public string? InstrId { get; set; }

    /// <summary>
    /// Сектор (Устарело)
    /// </summary>
    public string? SectorId { get; set; }

    /// <summary>
    /// Мин. шаг цены
    /// </summary>
    public double? MinStep { get; set; }

    /// <summary>
    /// Цена оферты
    /// </summary>
    public double? BuybackPrice { get; set; }

    /// <summary>
    /// Тип ценной бумаги
    /// </summary>
    public string? SecType { get; set; }

    /// <summary>
    /// Дата расчетов сделки
    /// </summary>
    public DateOnly? SettleDate { get; set; }

    /// <summary>
    /// Номинальная стоимость лота, в валюте номинала
    /// </summary>
    public double? LotValue { get; set; }

    /// <summary>
    /// Номинальная стоимость на дату расчетов
    /// </summary>
    public double? FaceValueOnSettleDate { get; set; }

    /// <summary>
    /// Дата колл-опциона
    /// </summary>
    public DateOnly? CallOptionDate { get; set; }

    /// <summary>
    /// Дата пут-опциона
    /// </summary>
    public DateOnly? PutOptionDate { get; set; }

    /// <summary>
    /// Дата, указанная Эмитентом для расчета доходности
    /// </summary>
    public DateOnly? DateYieldFromIssuer { get; set; }
}



