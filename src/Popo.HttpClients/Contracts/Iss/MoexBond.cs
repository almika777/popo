using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

/// <summary>
/// Модель для информации по облигации (данные MOEX)
/// </summary>
public class MoexBond
{
    /// <summary>
    /// Код ценной бумаги
    /// </summary>
    [JsonPropertyName("SECID")]
    public string SecId { get; set; } = string.Empty;

    /// <summary>
    /// Наименование ценной бумаги
    /// </summary>
    [JsonPropertyName("ISSUENAME")]
    public string IssueName { get; set; } = string.Empty;

    /// <summary>
    /// Полное наименование
    /// </summary>
    [JsonPropertyName("NAME")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Краткое наименование
    /// </summary>
    [JsonPropertyName("SHORTNAME")]
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// ISIN код
    /// </summary>
    [JsonPropertyName("ISIN")]
    public string Isin { get; set; } = string.Empty;

    /// <summary>
    /// Дата начала торгов
    /// </summary>
    [JsonPropertyName("ISSUEDATE")]
    public DateOnly? IssueDate { get; set; }

    /// <summary>
    /// Дата погашения
    /// </summary>
    [JsonPropertyName("MATDATE")]
    public DateOnly? MatDate { get; set; }

    /// <summary>
    /// Первоначальная номинальная стоимость
    /// </summary>
    [JsonPropertyName("INITIALFACEVALUE")]
    public double InitialFaceValue { get; set; }

    /// <summary>
    /// Валюта номинала
    /// </summary>
    [JsonPropertyName("FACEUNIT")]
    public string FaceUnit { get; set; } = string.Empty;

    /// <summary>
    /// Английское наименование
    /// </summary>
    [JsonPropertyName("LATNAME")]
    public string LatName { get; set; } = string.Empty;

    /// <summary>
    /// Дата начала торгов на Московской Бирже
    /// </summary>
    [JsonPropertyName("STARTDATEMOEX")]
    public DateOnly? StartDateMoex { get; set; }

    /// <summary>
    /// Наличие проспекта
    /// </summary>
    [JsonPropertyName("HASPROSPECTUS")]
    public bool? HasProspectus { get; set; }

    /// <summary>
    /// Дата принятия решения организатором торговли о включении ценной бумаги в Список
    /// </summary>
    [JsonPropertyName("DECISIONDATE")]
    public DateOnly? DecisionDate { get; set; }

    /// <summary>
    /// Облигации размещены с целью финансирования соглашений о партнерстве
    /// </summary>
    [JsonPropertyName("ISCONCESSIONAGREEMENT")]
    public bool? IsConcessionAgreement { get; set; }

    /// <summary>
    /// Допущен дефолт
    /// </summary>
    [JsonPropertyName("HASDEFAULT")]
    public bool? HasDefault { get; set; }

    /// <summary>
    /// Допущен технический дефолт
    /// </summary>
    [JsonPropertyName("HASTECHNICALDEFAULT")]
    public bool? HasTechnicalDefault { get; set; }

    /// <summary>
    /// Эмитент не соответствует требованию на текущий Список
    /// </summary>
    [JsonPropertyName("EMITENTMISMATCHCUR")]
    public int? EmitentMismatchCur { get; set; }

    /// <summary>
    /// Уровень листинга
    /// </summary>
    [JsonPropertyName("LISTLEVEL")]
    public int ListLevel { get; set; }

    /// <summary>
    /// ИЦБ допущена к орг. торгам по инициативе биржи
    /// </summary>
    [JsonPropertyName("INCLUDEDBYMOEX")]
    public bool IncludedByMoex { get; set; }

    /// <summary>
    /// Дней до погашения
    /// </summary>
    [JsonPropertyName("DAYSTOREDEMPTION")]
    public int DaysToRedemption { get; set; }

    /// <summary>
    /// Объем выпуска
    /// </summary>
    [JsonPropertyName("ISSUESIZE")]
    public long IssueSize { get; set; }

    /// <summary>
    /// Номинальная стоимость
    /// </summary>
    [JsonPropertyName("FACEVALUE")]
    public double FaceValue { get; set; }

    /// <summary>
    /// Бумаги для квалифицированных инвесторов
    /// </summary>
    [JsonPropertyName("ISQUALIFIEDINVESTORS")]
    public bool IsQualifiedInvestors { get; set; }

    /// <summary>
    /// Периодичность выплаты купона в год
    /// </summary>
    [JsonPropertyName("COUPONFREQUENCY")]
    public int CouponFrequency { get; set; }

    /// <summary>
    /// Дата выплаты купона
    /// </summary>
    [JsonPropertyName("COUPONDATE")]
    public DateOnly? CouponDate { get; set; }

    /// <summary>
    /// Ставка купона, %
    /// </summary>
    [JsonPropertyName("COUPONPERCENT")]
    public double? CouponPercent { get; set; }

    /// <summary>
    /// Сумма купона, в валюте номинала
    /// </summary>
    [JsonPropertyName("COUPONVALUE")]
    public double CouponValue { get; set; }

    /// <summary>
    /// Допуск к утренней дополнительной торговой сессии
    /// </summary>
    [JsonPropertyName("MORNINGSESSION")]
    public bool MorningSession { get; set; }

    /// <summary>
    /// Допуск к вечерней дополнительной торговой сессии
    /// </summary>
    [JsonPropertyName("EVENINGSESSION")]
    public bool EveningSession { get; set; }

    /// <summary>
    /// Допуск к дополнительной торговой сессии выходного дня
    /// </summary>
    [JsonPropertyName("WEEKENDSESSION")]
    public bool WeekendSession { get; set; }

    /// <summary>
    /// Вид облигации
    /// </summary>
    [JsonPropertyName("BOND_TYPE")]
    public string? BondKind { get; set; }

    /// <summary>
    /// Подвид облигации
    /// </summary>
    [JsonPropertyName("BOND_SUBTYPE")]
    public string? BondSubType { get; set; }

    /// <summary>
    /// Вид/категория ценной бумаги
    /// </summary>
    [JsonPropertyName("TYPENAME")]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Код типа инструмента
    /// </summary>
    [JsonPropertyName("GROUP")]
    public string? Group { get; set; }

    /// <summary>
    /// Тип бумаги
    /// </summary>
    [JsonPropertyName("TYPE")]
    public string? SecurityType { get; set; }

    /// <summary>
    /// Тип инструмента
    /// </summary>
    [JsonPropertyName("GROUPNAME")]
    public string? GroupName { get; set; }

    /// <summary>
    /// Код эмитента
    /// </summary>
    [JsonPropertyName("EMITTER_ID")]
    public long? EmitterId { get; set; }

    // ====== Поля из других режимов MOEX ======

    /// <summary>
    /// Номер государственной регистрации выпуска
    /// </summary>
    [JsonPropertyName("REGNUMBER")]
    public string? RegNumber { get; set; }

    /// <summary>
    /// Полное наименование (из режима торгов)
    /// </summary>
    [JsonPropertyName("SECNAME")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Цена последней сделки предыдущего торгового дня
    /// </summary>
    [JsonPropertyName("PREVPRICE")]
    public double? PrevPrice { get; set; }

    /// <summary>
    /// Значение оценки предыдущего торгового дня
    /// </summary>
    [JsonPropertyName("PREVWAPRICE")]
    public double? PrevWaPrice { get; set; }

    /// <summary>
    /// Доходность по средневзвешенной цене предыдущего торгового дня
    /// </summary>
    [JsonPropertyName("YIELDATPREVWAPRICE")]
    public double? YieldAtPrevWaPrice { get; set; }

    /// <summary>
    /// Идентификатор режима торгов
    /// </summary>
    [JsonPropertyName("BOARDID")]
    public string BoardId { get; set; } = string.Empty;

    /// <summary>
    /// Валюта
    /// </summary>
    [JsonPropertyName("CURRENCYID")]
    public string CurrencyId { get; set; } = string.Empty;

    /// <summary>
    /// Дата предыдущего торгового дня
    /// </summary>
    [JsonPropertyName("PREVDATE")]
    public DateOnly? PrevTradeDate { get; set; }

    /// <summary>
    /// НКД (накопленный купонный доход)
    /// </summary>
    [JsonPropertyName("ACCRUEDINT")]
    public double Accruedint { get; set; }

    /// <summary>
    /// Дата следующего купона
    /// </summary>
    [JsonPropertyName("NEXTCOUPON")]
    public DateOnly? NextCouponDate { get; set; }

    /// <summary>
    /// Длительность купона, выраженная в днях
    /// </summary>
    [JsonPropertyName("COUPONPERIOD")]
    public int CouponPeriod { get; set; }

    /// <summary>
    /// Если указана, то расчет доходности производится с использованием этой даты
    /// </summary>
    [JsonPropertyName("BUYBACKDATE")]
    public DateOnly? BuybackDate { get; set; }

    /// <summary>
    /// Дата оферты
    /// </summary>
    [JsonPropertyName("OFFERDATE")]
    public DateOnly? OfferDate { get; set; }

    /// <summary>
    /// Количество размещенных ценных бумаг, находящихся в обращении
    /// </summary>
    [JsonPropertyName("ISSUESIZEPLACED")]
    public long? IssueSizePlaced { get; set; }

    /// <summary>
    /// Тип облигации (например "Флоатер", "Дисконт", "Купонная")
    /// </summary>
    [JsonPropertyName("BONDTYPE")]
    public string? BondType { get; set; }

    /// <summary>
    /// Размер лота, ц.б.
    /// </summary>
    [JsonPropertyName("LOTSIZE")]
    public long LotSize { get; set; }

    /// <summary>
    /// Режим торгов
    /// </summary>
    [JsonPropertyName("BOARDNAME")]
    public string BoardName { get; set; } = string.Empty;

    /// <summary>
    /// Статус
    /// </summary>
    [JsonPropertyName("STATUS")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Точность, знаков после запятой
    /// </summary>
    [JsonPropertyName("DECIMALS")]
    public int Decimals { get; set; }

    /// <summary>
    /// Официальная цена закрытия предыдущего дня
    /// </summary>
    [JsonPropertyName("PREVLEGALCLOSEPRICE")]
    public double? PrevLegalClosePrice { get; set; }

    /// <summary>
    /// Примечание
    /// </summary>
    [JsonPropertyName("REMARKS")]
    public string? Remarks { get; set; }

    /// <summary>
    /// Рынок
    /// </summary>
    [JsonPropertyName("MARKETCODE")]
    public string? MarketCode { get; set; }

    /// <summary>
    /// Группа инструментов
    /// </summary>
    [JsonPropertyName("INSTRID")]
    public string? InstrId { get; set; }

    /// <summary>
    /// Сектор (Устарело)
    /// </summary>
    [JsonPropertyName("SECTORID")]
    public string? SectorId { get; set; }

    /// <summary>
    /// Мин. шаг цены
    /// </summary>
    [JsonPropertyName("MINSTEP")]
    public double? MinStep { get; set; }

    /// <summary>
    /// Цена оферты
    /// </summary>
    [JsonPropertyName("BUYBACKPRICE")]
    public double? BuybackPrice { get; set; }

    /// <summary>
    /// Тип ценной бумаги
    /// </summary>
    [JsonPropertyName("SECTYPE")]
    public string? SecType { get; set; }

    /// <summary>
    /// Дата расчетов сделки
    /// </summary>
    [JsonPropertyName("SETTLEDATE")]
    public DateOnly? SettleDate { get; set; }

    /// <summary>
    /// Номинальная стоимость лота, в валюте номинала
    /// </summary>
    [JsonPropertyName("LOTVALUE")]
    public double? LotValue { get; set; }

    /// <summary>
    /// Номинальная стоимость на дату расчетов
    /// </summary>
    [JsonPropertyName("FACEVALUEONSETTLEDATE")]
    public double? FaceValueOnSettleDate { get; set; }

    /// <summary>
    /// Дата колл-опциона
    /// </summary>
    [JsonPropertyName("CALLOPTIONDATE")]
    public DateOnly? CallOptionDate { get; set; }

    /// <summary>
    /// Дата пут-опциона
    /// </summary>
    [JsonPropertyName("PUTOPTIONDATE")]
    public DateOnly? PutOptionDate { get; set; }

    /// <summary>
    /// Дата, указанная Эмитентом для расчета доходности
    /// </summary>
    [JsonPropertyName("DATEYIELDFROMISSUER")]
    public DateOnly? DateYieldFromIssuer { get; set; }
}



