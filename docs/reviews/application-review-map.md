# Карта ревью контроллеров и методов приложения

Срез заново сверен с исходниками рабочей копии на 20.10.2026 после удаления кеша со страницы «Ликвидность» и перехода фондов на хранение текущего количества паёв и средней цены вместо журнала операций. Текстовые деревья ниже показывают полные цепочки до публичных методов providers и клиентов; одинаковый метод имеет один канонический ID.

SVG/PNG — направленный граф тех же вызовов: стрелка показывает caller → callee, общий метод нарисован одним узлом с несколькими входящими стрелками. Зелёным отмечено ранее просмотренное, серым — открытое; пунктиром показан поток данных от фонового обновления к читающему его provider.
Включены публичные production-методы достижимых от HTTP-входов и связанных фоновых producer-ов классов; DTO/record-конструкторы и сгенерированный EF-код не являются отдельными пунктами ревью.

## Этапы и состояние

- [x] **Этап 1. Инвентаризация и карта зависимостей** — заново сверена 20.10.2026: 8 контроллеров и 34 HTTP-метода; цепочки перепроверены по вызовам, внедрению зависимостей и реализациям интерфейсов; общие методы имеют единый ID.
- [ ] **Этап 2. Ревью HTTP-методов контроллеров** — начат 17.10.2026; C01 и все четыре маршрута C02 проверены; все четыре метода S01 и все связанные с ними методы D01 проверены; R01–R03 исправлены, R04 и R05 открыты.
- [ ] **Этап 3. Ревью уникальных application- и Core-методов** — после этапа 2.
- [ ] **Этап 4. Ревью providers, HTTP-клиентов и фоновых производителей данных** — общие узлы отмечать только один раз.
- [ ] **Этап 5. Сводка замечаний и решение по исправлениям**.

**Текущий статус:** этап 2; сохранены отметки ревью `BondsController.Search`, всех четырёх методов `BondsService` и соответствующих четырёх методов `BondsProvider`, всех маршрутов `LiquidityController` и пройденного ими стека. `BondsProvider.GetSecurities` остаётся отдельным непроверенным методом: Core вызывает его напрямую, минуя `BondsService`. 20.10.2026 проверены `ClosedPositionSessionCalculator` со всеми внутренними помощниками и весь вызываемый им K08-стек; общий `BondCouponClassifier.IsFloater` отмечен один раз в K09. 20.10.2026 проверены оба метода D15. 21.10.2026 проверены D21 и весь вызываемый им стек; вслед за этим проверены `ClosedPositionSessionsService.RecalculateAsync`, `ClosedPositionSessionsPageService.GetAsync`, HTTP-метод C04.Get и используемая дата `MoscowTime.Today` с конвертацией в московскую зону. `S13.RecalculateAsync`, `S13.GetStatus`, остальные маршруты C04 и J04 остаются открытыми из-за непроверенных D16/D20 и собственной логики постановки/выполнения задачи. R01–R03 исправлены; R04 — гонка Add/Update фонда, R05 — нет прямых Storage-тестов выборок валют и валют номинала; R11 — точность преобразования чисел в миграции D15. Денежные остатки/снимки кеша и журнал операций фондов удалены; cash-рекомендации и денежные потоки портфеля остаются действующими отдельными ветками.

## Как пользоваться картой

1. Начинать с маршрутов в дереве контроллеров. Отмечать endpoint после проверки binding, request/response, валидации, статусов и поведения ошибок. Галочка означает, что ревью проведено; открытые замечания фиксируются отдельно и не снимают галочку.
2. Обозначения `Cxx`, `Sxx`, `Dxx`, `Kxx`, `Hxx` и `Jxx` — канонические узлы. Один и тот же публичный метод указан один раз в реестре; маршрут может ссылаться на его ID из нескольких мест.
3. При проверке маршрута не отмечать повторно сервисы и providers. Их чекбоксы находятся в своих разделах.
4. После проверки стека проследить вызовы вверх. Отмечать родительский метод, только если он делегирует работу проверенным нижним компонентам; класс целиком отмечать, только когда проверены все его публичные методы.
5. Лист дерева — публичный метод класса. Для provider-метода проверить контракт Core, реализацию Storage и используемые им запросы/транзакции. Для общего клиента или калькулятора метод проверяется один раз, на остальных ветках остаётся ссылка на его ID.
6. В конце фиксировать найденное в разделе «Замечания». Непроверенные узлы оставлять пустыми.
7. Пропуски в ID оставлены намеренно: удалённые узлы не перенумеровываются, чтобы не менять ссылки и историю уже проведённого ревью.

## Дерево HTTP-входов

```text
Popo.Api
├── C01 BondsController (/api/bonds)
│   └── GET /search → S01
├── C02 LiquidityController (/api/liquidity)
│   ├── GET    /money-market-funds/page       → S02
│   ├── POST   /money-market-funds            → S03
│   ├── PUT    /money-market-funds/{id}       → S03
│   └── DELETE /money-market-funds/{id}       → S03
├── C03 CashRecommendationsController (/api/recommendations)
│   ├── GET    /cash/page                     → S05
│   ├── GET    /cash                          → S07
│   ├── GET    /cash/settings                 → S06
│   ├── GET    /cash/presets                  → S06
│   ├── POST   /cash/presets                  → S06
│   ├── PUT    /cash/presets/{id}             → S06
│   ├── DELETE /cash/presets/{id}             → S06
│   ├── POST   /cash/presets/{id}/activate    → S06
│   ├── PUT    /cash/settings                 → S06
│   └── POST   /cash/recalculate              → S07
├── C04 ClosedPositionSessionsController (/api/closed-position-sessions)
│   ├── GET    /                              → S13
│   ├── POST   /recalculate                   → S13 → J04
│   └── GET    /recalculation/{jobId}         → S13
├── C05 InitializationController (/api/initialization)
│   ├── GET    /status                        → S14
│   └── POST   /jobs/{jobKey}/retry           → S14
├── C06 PortfolioController (/api/portfolio)
│   ├── GET    /                              → S08
│   ├── POST   /valuations                    → S09
│   ├── PUT    /valuations/{id}               → S09
│   ├── DELETE /valuations/{id}               → S09
│   ├── POST   /cash-flows                    → S09
│   ├── PUT    /cash-flows/{id}               → S09
│   ├── DELETE /cash-flows/{id}               → S09
│   ├── GET    /return                        → S09 → K05
│   └── GET    /benchmark                     → S09 → S10 → K06
├── C07 PositionsController (/api/positions)
│   └── GET    /                              → S11
└── C08 TradesController (/api/trades)
    ├── GET    /                              → S12
    ├── POST   /                              → S12
    ├── PUT    /{id}                          → S12
    └── DELETE /{id}                          → S12

Дополнительный HTTP-вход вне контроллеров:
├── GET /health → Popo.Api/Program.cs
└── GET /health → Popo.Jobs/Program.cs (отдельный host)
```

## Полные цепочки вызовов: HTTP → публичные методы

Каждая строка ниже восстановлена по фактическому вызову, а не по полю зависимости класса. Ветвление показывает параллельные или последовательные вызовы. `↪ ID` означает ссылку на тот же канонический метод, который уже назван в другой ветке; проверять его повторно не нужно. Реализации providers и клиентов указаны на конце ветки.

### C01 — BondsController

```text
GET /api/bonds/search
└─ BondsController.Search → S01 BondsService.SearchAsync → D01 BondsProvider.SearchAsync
```

### C02 — LiquidityController

```text
GET /money-market-funds/page
└─ GetMoneyMarketFundsPage → S02 MoneyMarketFundsPageService.GetAsync
   └─ S11 PortfolioPositionsService.GetMoneyMarketFundsAsync [узел общий с C06]
      ├─ D03 PortfolioMoneyMarketFundsProvider.GetMoneyMarketFundsAsync
      └─ H01 IMoexHttpClient.GetMoneyMarketFundMarketdataAsync
         └─ H03 MoexContractMapper.ToContract(MoexMarketdata)

POST /money-market-funds
└─ AddMoneyMarketFund → S03 MoneyMarketFundManagementService.AddMoneyMarketFundAsync
   ├─ D03 PortfolioMoneyMarketFundsProvider.GetMoneyMarketFundsAsync [проверка дубля]
   └─ D03 PortfolioMoneyMarketFundsProvider.AddMoneyMarketFundAsync

PUT /money-market-funds/{id}
└─ UpdateMoneyMarketFund → S03 MoneyMarketFundManagementService.UpdateMoneyMarketFundAsync
   ├─ D03 PortfolioMoneyMarketFundsProvider.GetMoneyMarketFundsAsync [проверка дубля]
   └─ D03 PortfolioMoneyMarketFundsProvider.UpdateMoneyMarketFundAsync

DELETE /money-market-funds/{id}
└─ DeleteMoneyMarketFund → S03 MoneyMarketFundManagementService.DeleteMoneyMarketFundAsync
   └─ D03 PortfolioMoneyMarketFundsProvider.DeleteMoneyMarketFundAsync
```

### C03 — CashRecommendationsController

```text
GET /cash/page
└─ GetPage → S05 CashRecommendationsPageService.GetAsync
   ├─ D11 InvestmentStrategySettingsStore.GetPresetsAsync
   ├─ S01 BondsService.GetTradingCurrenciesAsync → D01 BondsProvider.GetTradingCurrenciesAsync
   └─ S01 BondsService.GetFaceUnitsAsync → D01 BondsProvider.GetFaceUnitsAsync

GET /cash ── GetCurrent ─┐
                         ├─ S07 CashRecommendationService.GetRecommendations
POST /cash/recalculate ─ Recalculate ─┘
   ├─ D11 InvestmentStrategySettingsStore.GetSettingsAsync
   └─ S07 BondAssessmentService.GetAssessmentsAsync [также вызывается J03]
      ├─ D01 BondsProvider.GetSecurities [прямой вызов IBondsProvider, без BondsService]
      ├─ D12 RatingsProvider.GetBondRatingsAsync
      ├─ D09 HistoryProvider.GetDailyVolumeStatistics
      ├─ H01 IMoexHttpClient.GetActiveBondsMarketdataAsync
      └─ [x] K07 расчёт YTM
         ├─ [x] D13 CashFlowProvider.GetYieldSchedule
         │  └─ [x] BondYieldCashFlowBuilder.TryBuild
         ├─ [x] BondPriceCalculator.CalculateDirtyPrice
         └─ [x] YieldCalculator.TryCalculate

GET /cash/settings ── GetSettings → S06 CashRecommendationSettingsService.GetSettingsAsync
   └─ D11 InvestmentStrategySettingsStore.GetSettingsAsync [общий с S07 и J03]

GET /cash/presets ── GetPresets → S06 CashRecommendationSettingsService.GetPresetsAsync
   └─ D11 InvestmentStrategySettingsStore.GetPresetsAsync [общий с S05]

POST /cash/presets ── CreatePreset → S06.CreatePresetAsync → D11.SavePresetAsync
PUT /cash/presets/{id} ── UpdatePreset → S06.UpdatePresetAsync
   ├─ D11.GetPresetAsync
   └─ D11.SavePresetAsync
DELETE /cash/presets/{id} ── DeletePreset → S06.DeletePresetAsync → D11.DeletePresetAsync
POST /cash/presets/{id}/activate ── ActivatePreset → S06.ActivatePresetAsync → D11.ActivatePresetAsync
PUT /cash/settings ── UpdateSettings → S06.UpdateSettingsAsync → D11.SaveSettingsAsync
```

### C04 — ClosedPositionSessionsController

```text
GET / ── Get → S13 ClosedPositionSessionsPageService.GetAsync → D15 ClosedPositionSessionsProvider.GetAsync

POST /recalculate ── Recalculate → S13.RecalculateAsync
   ├─ D16 InitializationStateStore.IsCompleteAsync
   └─ D20 Hangfire IBackgroundJobClient.Create → J04 ClosedPositionSessionsJob.UpdateAsync
      ├─ D16.IsCompleteAsync
      └─ K08 ClosedPositionSessionsService.RecalculateAsync
         ├─ K09.MoscowTime.Today → ToMoscow(DateTimeOffset) → Zone
         ├─ D21 ClosedPositionSessionSourceProvider.GetAsync
         ├─ K08 ClosedPositionSessionCalculator.Calculate → K08 типовые калькуляторы
         └─ D15 ClosedPositionSessionsProvider.ReplaceAsync

GET /recalculation/{jobId} ── GetStatus → S13.GetStatus
   └─ D20 JobStorage.GetConnection → GetJobData / GetStateData
```

### C05 — InitializationController

```text
GET /status ── GetStatus → S14 InitializationService.GetStatusAsync
   └─ D16 InitializationStateStore.GetStatusAsync

POST /jobs/{jobKey}/retry ── RetryJob → S14.InitializationService.RetryJobAsync
   ├─ J01 InitializationBootstrap.TryGetJob [проверка допустимого ключа]
   ├─ D16.InitializationStateStore.RetryJobAsync
   └─ D16.InitializationStateStore.GetStatusAsync
```

### C06 — PortfolioController

```text
GET /
└─ GetPage → S08 PortfolioPageService.GetAsync
   ├─ D06 PortfolioValuationsProvider.GetValuationsAsync
   ├─ D05 PortfolioCashFlowsProvider.GetCashFlowsAsync
   └─ S11 PortfolioPositionsService.GetOverviewAsync
      ├─ D07 PortfolioPositionsProvider.GetPositionsAsync → K03 PortfolioPositionCalculator
      ├─ S11.GetMoneyMarketFundsAsync [общий метод с C02]
      │  ├─ D03 PortfolioMoneyMarketFundsProvider.GetMoneyMarketFundsAsync
      │  └─ H01 IMoexHttpClient.GetMoneyMarketFundMarketdataAsync → H03 MoexContractMapper.ToContract
      ├─ H01 IMoexHttpClient.GetActiveBondsMarketdataAsync
      ├─ H01 IMoexHttpClient.GetActiveBondsSecuritiesAsync
      └─ D19 CurrencyRates через IDbContextFactory<PopoDbContext>

POST /valuations ── AddValuation → S09.AddValuationAsync
   ├─ D06.GetValuationsAsync [проверка даты]
   └─ D06.AddValuationAsync
PUT /valuations/{id} ── UpdateValuation → S09.UpdateValuationAsync
   ├─ D06.GetValuationsAsync [проверка id и даты]
   └─ D06.UpdateValuationAsync
DELETE /valuations/{id} ── DeleteValuation → S09.DeleteValuationAsync → D06.DeleteValuationAsync

POST /cash-flows ── AddCashFlow → S09.AddCashFlowAsync → D05.AddCashFlowAsync
PUT /cash-flows/{id} ── UpdateCashFlow → S09.UpdateCashFlowAsync → D05.UpdateCashFlowAsync
DELETE /cash-flows/{id} ── DeleteCashFlow → S09.DeleteCashFlowAsync → D05.DeleteCashFlowAsync

GET /return ── CalculateReturn → S09.CalculateReturnAsync
   ├─ D06.GetValuationsAsync
   ├─ D05.GetCashFlowsAsync
   └─ K05 PortfolioReturnCalculator.Calculate

GET /benchmark ── CalculateBenchmark → S09.CalculateBenchmarkAsync → S10.PortfolioBenchmarkService.CalculateAsync
   ├─ D06.GetValuationsAsync [общий с S08/S09]
   ├─ D05.GetCashFlowsAsync [общий с S08/S09]
   ├─ H01 IMoexHttpClient.GetLqdtPriceHistoryAsync
   ├─ D17 PortfolioBondBenchmarkHistoryProvider.GetHistoryAsync
   ├─ D18 MoexIndexHistoryStore.UpsertAsync
   └─ K06 PortfolioBenchmarkCalculator / series calculators
```

### C07 — PositionsController

```text
GET / ── GetPositions → S11 PortfolioPositionsService.GetPageAsync
   ├─ D14 PositionRecommendationStore.GetAsync
   ├─ D07 PortfolioPositionsProvider.GetPositionsAsync → K03 PortfolioPositionCalculator.Calculate / ToRecord
   ├─ D04 PortfolioTradesProvider.GetTradesAsync [общий метод с C08]
   ├─ H01 IMoexHttpClient.GetActiveBondsMarketdataAsync [общий с S07/S08]
   ├─ H01 IMoexHttpClient.GetActiveBondsSecuritiesAsync [общий с S08/S12/jobs]
   ├─ D09 HistoryProvider.GetDailyVolumeStatistics
   ├─ D08 PortfolioCouponProvider.GetCouponScheduleAsync
   ├─ D19 CurrencyRates через IDbContextFactory<PopoDbContext>
   └─ K02 position calculators, K03 open-lot/income calculations, K09 shared time/rules
```

### C08 — TradesController

```text
GET / ── GetTrades → S12 PortfolioTradesService.GetTradesAsync
   ├─ D04 PortfolioTradesProvider.GetTradesAsync [общий с C07]
   └─ H01 IMoexHttpClient.GetActiveBondsSecuritiesAsync [если найдены сделки]

POST / ── AddTrade → S12.AddTradeAsync
   ├─ S01 BondsService.GetHistoricalFaceValuesAsync → D01.BondsProvider.GetHistoricalFaceValuesAsync
   └─ D04 PortfolioTradesProvider.AddTradeAsync → K03 PortfolioPositionsValidate.ValidateTradeSequence

PUT /{id} ── UpdateTrade → S12.UpdateTradeAsync
   ├─ S01.GetHistoricalFaceValuesAsync → D01.GetHistoricalFaceValuesAsync
   └─ D04.PortfolioTradesProvider.UpdateTradeAsync → K03.ValidateTradeSequence

DELETE /{id} ── DeleteTrade → S12.DeleteTradeAsync
   └─ D04.PortfolioTradesProvider.DeleteTradeAsync → K03.ValidateTradeSequence
```

Публичный `PortfolioPositionsService.GetPositionsAsync` сейчас не имеет production-вызова из HTTP/Jobs; он оставлен в реестре как непроверенный публичный метод, но не изображён как достижимая ветка контроллера.

### Публичные методы без production-вызовов

Поиск по production-коду не нашёл callers для перечисленных методов. Они остаются в реестре как непроверенные; в графе от контроллеров не соединяются, пока у них нет действующего вызывающего пути.

- `PortfolioTradesProvider.GetTradeAsync`.
- `PortfolioValuationsProvider.GetValuationAsync`.
- `PortfolioCashFlowsProvider.GetCashFlowAsync`.
- `PortfolioCouponProvider.GetPublishedHistoryAsync`.
- `HistoryProvider.GetMedianDailyVolume`.
- `MoexHttpClient.GetActiveBondsMarketdataYieldsAsync` и `GetBondTradesAsync` (также без caller в `Popo.Api`/`Popo.Jobs`).

## Фоновые производители, пересекающиеся с HTTP-ветками

```text
J03 PositionRecommendationJob.UpdateAsync
├─ D16.InitializationStateStore.IsCompleteAsync
├─ D14.PositionRecommendationStore.StartAsync → CompleteAsync / FailAsync
└─ K04 PositionRecommendationService.CalculateAsync
   ├─ D11.InvestmentStrategySettingsStore.GetSettingsAsync [общий с C03]
   ├─ D07.PortfolioPositionsProvider.GetPositionsAsync [общий источник с S11]
   ├─ S07.BondAssessmentService.GetAssessmentsAsync [общий метод с C03]
   │  └─ D01.GetSecurities, D09, D12, H01.GetActiveBondsMarketdataAsync, D13, K07
   └─ K04 PositionRecommendationPolicy.Evaluate

J04 ClosedPositionSessionsJob.UpdateAsync
├─ D16.InitializationStateStore.IsCompleteAsync [общий с C04/C05/J01/J03]
└─ K08 ClosedPositionSessionsService.RecalculateAsync
   ├─ K09.MoscowTime.Today → ToMoscow(DateTimeOffset) → Zone
   ├─ D21.ClosedPositionSessionSourceProvider.GetAsync
   ├─ K08 ClosedPositionSessionCalculator.Calculate → типовые калькуляторы
   └─ D15.ClosedPositionSessionsProvider.ReplaceAsync [C04 читает D15.GetAsync]

J02 — синхронизационные jobs
├─ MOEX jobs → H01 IMoexHttpClient / MoexHttpClient / MoexIssClient → H03 mapping → таблицы MOEX
├─ BondRatingUpdateJob → H02 ICbondClient.GetRatingsAsync → BondRatings
├─ CbrCurrencyRatesUpdateJob → H02 ICbrHttpClient.GetCurrenciesAsync / GetCurrencyRatesAsync → CurrencyRates
├─ MoexHistoryPricesUpdateJob → D10.DailyVolumeStatisticsStore.RefreshAsync [после записи истории]
└─ MoexIndexHistoryUpdateJob → D18.MoexIndexHistoryStore.UpsertAsync [общий с C06 benchmark]

J01 InitializationBootstrapService/RecurringJobsScheduler запускают источники J02; расписание и реестр retry — отдельные пункты J01.
```

## Этап 2 — контроллеры и HTTP-контракт

Проверять endpoint-методы отдельно; ID после стрелки ведёт к единственному пункту ревью реализации.

### C01 — `BondsController`

Файл: [`BondsController.cs`](../../src/Popo.Api/Controllers/BondsController.cs)

- [x] `GET /api/bonds/search` — `Search` → `S01.BondsService.SearchAsync`; проверено 17.10.2026, связанные R01–R03 исправлены.

### C02 — `LiquidityController`

Файл: [`LiquidityController.cs`](../../src/Popo.Api/Controllers/LiquidityController.cs)

- [x] `GET /api/liquidity/money-market-funds/page` — `GetMoneyMarketFundsPage` → `S02.MoneyMarketFundsPageService.GetAsync`; проверено 19.10.2026 вместе со стеком чтения позиций фондов.
- [x] `POST /api/liquidity/money-market-funds` — `AddMoneyMarketFund` → `S03.AddMoneyMarketFundAsync`; стек добавления проверен 20.10.2026, открыто замечание R04.
- [x] `PUT /api/liquidity/money-market-funds/{id}` — `UpdateMoneyMarketFund` → `S03.UpdateMoneyMarketFundAsync`; проверено 20.10.2026: валидирует запрос, возвращает обновлённую запись с `200 OK`; HTTP-тест проверяет ответ и сохранённые значения.
- [x] `DELETE /api/liquidity/money-market-funds/{id}` — `DeleteMoneyMarketFund` → `S03.DeleteMoneyMarketFundAsync`; проверено 20.10.2026: при успехе возвращает `204 NoContent`, отсутствующая запись проходит как 404.

### C03 — `CashRecommendationsController`

Файл: [`CashRecommendationsController.cs`](../../src/Popo.Api/Controllers/CashRecommendationsController.cs)

- [ ] `GET /api/recommendations/cash/page` — `GetPage` → `S05.CashRecommendationsPageService.GetAsync`.
- [ ] `GET /api/recommendations/cash` — `GetCurrent` → `S07.CashRecommendationService.GetRecommendations`.
- [ ] `GET /api/recommendations/cash/settings` — `GetSettings` → `S06.GetSettingsAsync`.
- [ ] `GET /api/recommendations/cash/presets` — `GetPresets` → `S06.GetPresetsAsync`.
- [ ] `POST /api/recommendations/cash/presets` — `CreatePreset` → `S06.CreatePresetAsync`.
- [ ] `PUT /api/recommendations/cash/presets/{id}` — `UpdatePreset` → `S06.UpdatePresetAsync`.
- [ ] `DELETE /api/recommendations/cash/presets/{id}` — `DeletePreset` → `S06.DeletePresetAsync`.
- [ ] `POST /api/recommendations/cash/presets/{id}/activate` — `ActivatePreset` → `S06.ActivatePresetAsync`.
- [ ] `PUT /api/recommendations/cash/settings` — `UpdateSettings` → `S06.UpdateSettingsAsync`.
- [ ] `POST /api/recommendations/cash/recalculate` — `Recalculate` → тот же `S07`, что и `GetCurrent`.

### C04 — `ClosedPositionSessionsController`

Файл: [`ClosedPositionSessionsController.cs`](../../src/Popo.Api/Controllers/ClosedPositionSessionsController.cs)

- [x] `GET /api/closed-position-sessions` — `Get` → `S13.GetAsync`; метод контроллера и метод сервиса только передают вызов проверенному нижнему стеку.
- [ ] `POST /api/closed-position-sessions/recalculate` — `Recalculate` → `S13.RecalculateAsync` → `J04`.
- [ ] `GET /api/closed-position-sessions/recalculation/{jobId}` — `GetStatus` → `S13.GetStatus`.

### C05 — `InitializationController`

Файл: [`InitializationController.cs`](../../src/Popo.Api/Controllers/InitializationController.cs)

- [ ] `GET /api/initialization/status` — `GetStatus` → `S14.GetStatusAsync`.
- [ ] `POST /api/initialization/jobs/{jobKey}/retry` — `RetryJob` → `S14.RetryJobAsync`.

### C06 — `PortfolioController`

Файл: [`PortfolioController.cs`](../../src/Popo.Api/Controllers/PortfolioController.cs)

- [ ] `GET /api/portfolio` — `GetPage` → `S08.PortfolioPageService.GetAsync`.
- [ ] `POST /api/portfolio/valuations` — `AddValuation` → `S09.AddValuationAsync`.
- [ ] `PUT /api/portfolio/valuations/{id}` — `UpdateValuation` → `S09.UpdateValuationAsync`.
- [ ] `DELETE /api/portfolio/valuations/{id}` — `DeleteValuation` → `S09.DeleteValuationAsync`.
- [ ] `POST /api/portfolio/cash-flows` — `AddCashFlow` → `S09.AddCashFlowAsync`.
- [ ] `PUT /api/portfolio/cash-flows/{id}` — `UpdateCashFlow` → `S09.UpdateCashFlowAsync`.
- [ ] `DELETE /api/portfolio/cash-flows/{id}` — `DeleteCashFlow` → `S09.DeleteCashFlowAsync`.
- [ ] `GET /api/portfolio/return` — `CalculateReturn` → `S09.CalculateReturnAsync` → `K05.PortfolioReturnCalculator.Calculate`.
- [ ] `GET /api/portfolio/benchmark` — `CalculateBenchmark` → `S09.CalculateBenchmarkAsync` → `S10.CalculateAsync` → `K06`.

### C07 — `PositionsController`

Файл: [`PositionsController.cs`](../../src/Popo.Api/Controllers/PositionsController.cs)

- [ ] `GET /api/positions` — `GetPositions` → `S11.GetPageAsync`; сохранённые рекомендации производит `J03`.

### C08 — `TradesController`

Файл: [`TradesController.cs`](../../src/Popo.Api/Controllers/TradesController.cs)

- [ ] `GET /api/trades` — `GetTrades` → `S12.GetTradesAsync`.
- [ ] `POST /api/trades` — `AddTrade` → `S12.AddTradeAsync`.
- [ ] `PUT /api/trades/{id}` — `UpdateTrade` → `S12.UpdateTradeAsync`.
- [ ] `DELETE /api/trades/{id}` — `DeleteTrade` → `S12.DeleteTradeAsync`.

### Общий HTTP-контур — один раз для всех Cxx

- [x] `FluentValidationActionFilter.OnActionExecutionAsync` — проверен 20.10.2026: находит зарегистрированный валидатор аргумента, возвращает 400 при ошибках и не вызывает action; [`FluentValidationActionFilter.cs`](../../src/Popo.Api/Filters/FluentValidationActionFilter.cs).
- [x] `UpsertMoneyMarketFundRequestValidator` — проверен 20.10.2026 для POST/PUT: разрешает только LQDT/TMON, положительное целое количество и конечную положительную среднюю цену.
- [ ] Остальные валидаторы (`BondSearchRequest`, strategy settings/preset, valuation/cash-flow/return query и trade) — папка [`Validators`](../../src/Popo.Api/Validators/); проверять правила в их `AbstractValidator`-конструкторах.
- [x] `ApiExceptionHandler.TryHandleAsync`, типизированные ошибки Core и соответствие статусов ADR-0004 — проверены 20.10.2026; 400/404/409 и общий 500 покрыты тестом [`ApiExceptionHandlerTests.cs`](../../tests/Popo.Tests/ApiExceptionHandlerTests.cs). Необработанная ошибка уникального индекса при гонке POST остаётся R04; [`ApiExceptionHandler.cs`](../../src/Popo.Api/ApiExceptionHandler.cs), [`ADR-0004`](../adr/0004-обработка-ошибок-api-вне-контроллеров.md).
- [ ] `Program.cs`: регистрацию, миграции, CORS, `MapControllers`, OpenAPI/Scalar и `GET /health` — [`Program.cs`](../../src/Popo.Api/Program.cs).
- [ ] `ApiServiceCollectionExtensions.AddPopoApiServices` — [`ApiServiceCollectionExtensions.cs`](../../src/Popo.Api/ApiServiceCollectionExtensions.cs).
- [ ] `Popo.Core.ServiceCollectionExtensions.AddPopoCore` — регистрация Core-сервисов.
- [ ] `Popo.Storage.ServiceCollectionExtensions.AddPopoStorage` — регистрация провайдеров; общий вызов API и Jobs.
- [ ] `ClosedPositionSessionsRegistration.AddClosedPositionSessionsApi` — DI и Hangfire storage для C04.
- [ ] `PopoDatabaseInitializer.Initialize` — миграции при старте.
- [ ] `CashInvestmentRecommendationApiMapper.ToResponse(InvestmentStrategyPreset)` — C03.
- [ ] `CashInvestmentRecommendationApiMapper.ToResponse(InvestmentStrategySettings)` — C03.
- [ ] `InvestmentStrategySettingsRequest.ToDomain` — C03.
- [ ] `CashRecommendationApiMapper.ToResponse` — C03.
- [ ] `PositionRecommendationApiMapper.ToResponse` — C07.
- [x] Сверены `docs/modules/api.md` и действующие `[Route]`/`[Http*]`: актуальные контроллеры не объявляют документированные `/api/bonds/currencies`, `/api/bonds/face-units` и `/api/positions/recommendation`; это зафиксировано как R06. Отдельный `/api/portfolio/summary` в текущей документации не найден.

## Этап 3 — канонические application/Core-узлы

Каждый метод приложения отмечается здесь ровно один раз. Ветви контроллеров ссылаются на эти пункты по ID.

### S01 — каталог облигаций, Core `BondsService`

Класс вызывают C01, S05 и S12. Все четыре метода проходят к одноимённым операциям D01. `BondAssessmentService` из S07 вызывает `IBondsProvider.GetSecurities` напрямую, минуя этот сервис. Реализация: [`BondsService.cs`](../../src/Popo.Core/Bonds/BondsService.cs).

- [x] `SearchAsync` — проверен 17.10.2026; согласован с API-контрактом nullable НКД.
- [x] `GetHistoricalFaceValuesAsync` — проверено 20.10.2026: передаёт пакет запросов в D01; для каждой сделки возвращается последнее положительное значение номинала на дату сделки или раньше, с точным совпадением SECID и доски; без истории значение остаётся null.
- [x] `GetTradingCurrenciesAsync` — проверено 20.10.2026: делегирует D01 получение уникальных непустых кодов валют торгов, отсортированных по возрастанию; прямого Storage-теста нет (R05).
- [x] `GetFaceUnitsAsync` — проверено 20.10.2026: делегирует D01 получение уникальных непустых валют номинала, отсортированных по возрастанию; прямого Storage-теста нет (R05).

### S02 — страница фондов, `MoneyMarketFundsPageService`

Файл: [`MoneyMarketFundsPageService.cs`](../../src/Popo.Api/Services/MoneyMarketFundsPageService.cs). Вызывает S11; список вариантов формирует из `RelationHelper.MoneyMarketFunds`.

- [x] `GetAsync` — проверено 19.10.2026: загружает оценённые позиции через S11 и добавляет варианты фондов из общего справочника.

### S03 — изменение текущих позиций фондов, `MoneyMarketFundManagementService`

Файл: [`MoneyMarketFundManagementService.cs`](../../src/Popo.Api/Services/MoneyMarketFundManagementService.cs). Для Add используются `RelationHelper.MoneyMarketFunds`, `D03.GetMoneyMarketFundsAsync` и `D03.AddMoneyMarketFundAsync`.

- [x] `AddMoneyMarketFundAsync` — проверено 20.10.2026: нормализует SECID, разрешает только фонды из справочника, проверяет существующую позицию и передаёт количество/среднюю цену в D03. Гонка между проверкой и INSERT — R04.
- [x] `UpdateMoneyMarketFundAsync` — проверено 20.10.2026: проверяет поддерживаемый фонд и дубликат (с исключением текущего `id`), передаёт канонический `BoardId`, количество и среднюю цену в D03; отсутствие записи превращает в 404. Проверка дубликата использует уже проверенный D03.Get.
- [x] `DeleteMoneyMarketFundAsync` — проверено 20.10.2026: вызывает D03.Delete и преобразует результат `false` в 404.

### S05 — данные страницы рекомендаций, `CashRecommendationsPageService`

Файл: [`CashRecommendationsPageService.cs`](../../src/Popo.Api/Services/CashRecommendationsPageService.cs). Связи: D11, S01.

- [ ] `GetAsync`.

### S06 — настройки и пресеты, `CashRecommendationSettingsService`

Файл: [`CashRecommendationSettingsService.cs`](../../src/Popo.Api/Services/CashRecommendationSettingsService.cs). Хранилище D11.

- [ ] `GetSettingsAsync`.
- [ ] `GetPresetsAsync`.
- [ ] `CreatePresetAsync`.
- [ ] `UpdatePresetAsync`.
- [ ] `DeletePresetAsync`.
- [ ] `ActivatePresetAsync`.
- [ ] `UpdateSettingsAsync`.

### S07 — расчёт кандидатов cash, Core recommendation services

Файлы: [`CashRecommendationService.cs`](../../src/Popo.Core/Recommendations/CashRecommendationService.cs), [`BondAssessmentService.cs`](../../src/Popo.Core/Recommendations/BondAssessmentService.cs). `CashRecommendationService` получает настройки из D11 и вызывает `BondAssessmentService`; он же вызывается фоновым J03. `BondAssessmentService` напрямую читает D01.GetSecurities, а также использует D09, D12, D13 и H01; денежные потоки и доходность — K07.

- [ ] `CashRecommendationService.GetRecommendations` — фильтры и состав кандидатов.
- [ ] `BondAssessmentService.GetAssessmentsAsync` — сбор данных, полнота, рейтинг, ликвидность, YTM.
- [ ] `TryCreateYieldRequest`, выбор рейтинга и создание calculation details — проверять как части S07, не отдельными повторами.

### S08 — сводка страницы портфеля, `PortfolioPageService`

Файл: [`PortfolioPageService.cs`](../../src/Popo.Api/Services/PortfolioPageService.cs). Использует D05, D06 и `S11.GetOverviewAsync` для сборки данных страницы.

- [ ] `GetAsync(asOf)`; проверить параллельное выполнение и состав `PortfolioPageResponse`: денежные потоки и оценки для графика, сводные значения позиций/фондов/общего объёма из S11. Автоматическую доходность на странице не рассчитывает.

### S09 — оценки, денежные потоки, доходность, `PortfolioDataService`

Файл: [`PortfolioDataService.cs`](../../src/Popo.Api/Services/PortfolioDataService.cs). Зависимости D05, D06; возврат — K05; benchmark делегируется S10.

- [ ] `AddValuationAsync`.
- [ ] `UpdateValuationAsync`.
- [ ] `DeleteValuationAsync`.
- [ ] `AddCashFlowAsync`.
- [ ] `UpdateCashFlowAsync`.
- [ ] `DeleteCashFlowAsync`.
- [ ] `CalculateReturnAsync`.
- [ ] `CalculateBenchmarkAsync`.

### S10 — сервис сравнения с инструментами, `PortfolioBenchmarkService`

Файл: [`PortfolioBenchmarkService.cs`](../../src/Popo.Api/Services/PortfolioBenchmarkService.cs). Зависимости D05, D06, D17, D18, H01; расчёты K06.

- [ ] `CalculateAsync(from, to, includeLqdt, includeIndexedBond)`; проверить загрузку свежей истории и публикацию LQDT-кэша.

### S11 — открытые позиции, `PortfolioPositionsService`

Файл: [`PortfolioPositionsService.cs`](../../src/Popo.Api/Services/Position/PortfolioPositionsService.cs). Зависимости различаются по методу: `GetPageAsync` использует D04, D07–D09, D14, D19, H01 и K02–K03; `GetOverviewAsync` использует D03, D07, D19 и H01; `GetMoneyMarketFundsAsync` использует D03 и H01. Вызовы: C07 → `GetPageAsync`, S08 → `GetOverviewAsync`, S02 и `GetOverviewAsync` → один общий `GetMoneyMarketFundsAsync`.

- [ ] `GetPageAsync`.
- [ ] `GetPositionsAsync` — проверить контракт; прямого production-вызова из контроллеров в текущей карте нет.
- [ ] `GetOverviewAsync(asOf)` — портфельная сводка.
- [x] `GetMoneyMarketFundsAsync` — проверено 19.10.2026 для C02: читает сохранённые позиции через D03, запрашивает котировку MOEX через H01 и рассчитывает текущую стоимость/результат; сделки, купоны и валютные курсы этому пути не нужны.
- [ ] Загрузка источников, выбор калькулятора, цены, валют, купонов и fallback-пути — проверить внутри соответствующих методов выше.

### S12 — журнал сделок, `PortfolioTradesService`

Файл: [`PortfolioTradesService.cs`](../../src/Popo.Api/Services/Trades/PortfolioTradesService.cs). Зависимости D01, D04 и H01; D04 при изменении журнала использует K03 для проверки последовательности сделок.

- [ ] `GetTradesAsync`.
- [ ] `AddTradeAsync`.
- [ ] `UpdateTradeAsync`.
- [ ] `DeleteTradeAsync`.
- [ ] `UseFaceValueAtTradeDateAsync` — приватный helper; проверять внутри Add/Update, не заводить отдельную ветку.

### S13 — закрытые сессии, `ClosedPositionSessionsPageService`

Файл: [`ClosedPositionSessionsPageService.cs`](../../src/Popo.Api/Services/ClosedPositions/ClosedPositionSessionsPageService.cs). Чтение D15, проверка состояния D16, постановка/статус Hangfire-задачи через D20, пересчёт J04.

- [x] `GetAsync` — напрямую возвращает `D15.ClosedPositionSessionsProvider.GetAsync`; метод и его endpoint проверены по нижнему стеку.
- [ ] `RecalculateAsync`.
- [ ] `GetStatus(jobId)`.

### S14 — инициализация, `InitializationService`

Файл: [`InitializationService.cs`](../../src/Popo.Api/Services/InitializationService.cs). Состояние D16, оркестратор первичной загрузки J01.

- [ ] `GetStatusAsync`.
- [ ] `RetryJobAsync` — проверить whitelist ключей, переход состояний и фактическое поведение retry.

## Этап 4 — общие providers, клиенты и расчётные узлы

### Dxx — Storage/источники. Каждый публичный метод проверяется один раз

- **D01 `BondsProvider`** (`IBondsProvider`) — S01 и S12 через S01; D01.GetSecurities вызывается напрямую из S07.BondAssessmentService; справочники и исторические номиналы.
  - [x] `SearchAsync` — проверен 17.10.2026; НКД сохраняет null, тесты расширены.
  - [x] `GetHistoricalFaceValuesAsync` — проверено 20.10.2026: одна выборка истории по SECID и максимальной дате; для каждого входного ключа возвращает последнее конечное положительное значение на дату или раньше, совпадая также по доске; сохраняет порядок запросов и null при отсутствии истории. Storage-тест проверяет даты, доски и нормализацию SECID.
  - [x] `GetTradingCurrenciesAsync` — проверено 20.10.2026: no-tracking выборка непустых `CurrencyId`, уникальная и отсортированная; прямого Storage-теста нет (R05).
  - [x] `GetFaceUnitsAsync` — проверено 20.10.2026: no-tracking выборка непустых `FaceUnit`, уникальная и отсортированная; прямого Storage-теста нет (R05).
  - [ ] `GetSecurities`.
- **D03 `PortfolioMoneyMarketFundsProvider`** (`IPortfolioMoneyMarketFundsProvider`) — S03, S11; S02 reaches it through S11.
  - [x] `GetMoneyMarketFundsAsync` — проверено 19.10.2026: no-tracking чтение с сортировкой по `SecId` и проекцией сохранённого количества/средней цены; также используется S03 для проверки дубликата перед добавлением.
  - [x] `AddMoneyMarketFundAsync` — проверено 20.10.2026: валидирует данные, нормализует SECID, вставляет позицию и сохраняет её; уникальный индекс защищает бизнес-ключ, но конфликт гонки не преобразуется в 409 (R04).
  - [x] `UpdateMoneyMarketFundAsync` — проверено 20.10.2026: валидирует поля, загружает запись по `id`, сохраняет новые значения по шаблону `Attach`/`Modified` из ADR-0003 и возвращает сохранённую запись; Storage-тест перечитывает и проверяет обновлённые данные.
  - [x] `DeleteMoneyMarketFundAsync` — проверено 20.10.2026: удаляет позицию по `id` через `ExecuteDeleteAsync`, результат показывает, найдена ли запись; Storage-тест подтверждает отсутствие строки после удаления.
- **D04 `PortfolioTradesProvider`** (`IPortfolioTradesProvider`) — S11, S12; закрытые сессии читают сделки через отдельный source provider.
  - [ ] `GetTradesAsync`.
  - [ ] `GetTradeAsync`.
  - [ ] `AddTradeAsync`.
  - [ ] `UpdateTradeAsync`.
  - [ ] `DeleteTradeAsync`.
- **D05 `PortfolioCashFlowsProvider`** (`IPortfolioCashFlowsProvider`) — S08–S10.
  - [ ] `GetCashFlowsAsync`.
  - [ ] `GetCashFlowAsync`.
  - [ ] `AddCashFlowAsync`.
  - [ ] `UpdateCashFlowAsync`.
  - [ ] `DeleteCashFlowAsync`.
- **D06 `PortfolioValuationsProvider`** (`IPortfolioValuationsProvider`) — S08–S10.
  - [ ] `GetValuationsAsync`.
  - [ ] `GetValuationAsync`.
  - [ ] `AddValuationAsync`.
  - [ ] `UpdateValuationAsync`.
  - [ ] `DeleteValuationAsync`.
- **D07 `PortfolioPositionsProvider`** (`IPortfolioPositionsProvider`) — S11; агрегация сделок и валидация последовательности.
  - [ ] `GetPositionsAsync`.
- **D08 `PortfolioCouponProvider`** (`IPortfolioCouponProvider`) — S11; история купонов открытых позиций.
  - [ ] `GetCouponScheduleAsync`.
  - [ ] `GetPublishedHistoryAsync`.
- **D09 `HistoryProvider`** (`IHistoryProvider`) — S07, S11; J02 готовит статистику через D10.
  - [ ] `GetMedianDailyVolume`.
  - [ ] `GetDailyVolumeStatistics`.
- **D10 `DailyVolumeStatisticsStore`** — D09 читает готовые показатели; J02 обновляет их после синхронизации истории.
  - [ ] `RefreshAsync`.
- **D11 `InvestmentStrategySettingsStore`** (`ICashInvestmentRecommendationStore`) — S05–S07.
  - [ ] `GetSettingsAsync`.
  - [ ] `GetPresetsAsync`.
  - [ ] `GetPresetAsync`.
  - [ ] `SavePresetAsync`.
  - [ ] `DeletePresetAsync`.
  - [ ] `ActivatePresetAsync`.
  - [ ] `SaveSettingsAsync`.
- **D12 `RatingsProvider`** (`IRatingsProvider`) — S07.
  - [ ] `GetBondRatingsAsync`.
- **D13 `CashFlowProvider`** (`ICashFlowProvider`) — S07; расписание для YTM.
  - [x] `GetYieldSchedule` — проверен 20.10.2026 в составе K07; загружает историю купонов/амортизаций, переводит суммы в валюту расчётов и строит график потоков. Используемые для этого `TryGetCurrencyConversionFactor`, `TryGetRubRate` и `NormalizeCurrency` также просмотрены.
- **D14 `PositionRecommendationStore`** (`IPositionRecommendationStore`) — S11 читает снимок, J03 пишет.
  - [ ] `GetAsync`.
  - [ ] `StartAsync`.
  - [ ] `CompleteAsync`.
  - [ ] `FailAsync`.
- **D15 `ClosedPositionSessionsProvider`** (`IClosedPositionSessionsProvider`) — S13 читает, J04 атомарно заменяет снимок.
  - [x] `GetAsync` — проверено 20.10.2026: использует отдельный контекст и `AsNoTracking`, проецирует все поля контракта и сортирует по дате закрытия, SECID и ID; Storage-тест проверяет полный round-trip.
  - [x] `ReplaceAsync` — проверено 20.10.2026: в одной транзакции удаляет старый снимок, добавляет рассчитанные строки и фиксирует замену; тесты проверяют удаление устаревших строк, повторную публикацию без дублей и rollback при ошибке вставки. Параллельные пересчёты сериализует распределённая блокировка `ClosedPositionSessionsJob`.
- **D16 `InitializationStateStore`** (`IInitializationStateStore`) — S13, S14, J01, J03, J04.
  - [ ] `EnsureCurrentBootstrapAsync`.
  - [ ] `RecoverUnfinishedJobsAsync`.
  - [ ] `GetStatusAsync`.
  - [ ] `IsCompleteAsync`.
  - [ ] `GetNextPendingJobKeyAsync`.
  - [ ] `StartJobAsync`.
  - [ ] `UpdateProgressAsync`.
  - [ ] `MarkSucceededAsync`.
  - [ ] `MarkFailedAsync`.
  - [ ] `RetryFailedJobAutomaticallyAsync`.
  - [ ] `RetryJobAsync`.
- **D17 `PortfolioBondBenchmarkHistoryProvider`** (`IPortfolioBondBenchmarkHistoryProvider`) — S10; история RU000A10F504.
  - [ ] `GetHistoryAsync`.
- **D18 `MoexIndexHistoryStore`** (`IMoexIndexHistoryStore`) — S10 обновляет кэш LQDT; также используется J02.
  - [ ] `UpsertAsync`.
  - [ ] `GetHistoryAsync`.
- **D19 Прямое чтение `CurrencyRates` через `IDbContextFactory<PopoDbContext>`** — два отдельных вызова S11 и один вызов D21; это общий источник данных, а не общий метод. Фильтр D21 по валютам исходных сделок/купонов/амортизаций и `RateDate <= asOf` проверен в составе D21; вызовы S11 остаются открытыми.
- **D20 Hangfire `IBackgroundJobClient` / `JobStorage`** — внешние методы постановки/чтения статуса в S13; J04 исполняет задачу.
- **D21 `ClosedPositionSessionSourceProvider`** — J04 читает здесь сделки, облигации, режимы торгов, купоны, амортизации и валютные курсы для K08.
  - [x] `GetAsync` — проверено 21.10.2026 вместе с `LoadSourceDataAsync`, `BuildInstruments`, `SelectSecurity`, `BuildClosedSessionInstrument` и запросами EF к `PortfolioTrades`, `MoexBonds`, `MoexBondSecurities`, `MoexCouponsEntities`, `MoexAmortsEntities`, `CurrencyRates`. Источники читаются в `RepeatableRead`; D19 остаётся общим узлом, потому что S11 выполняет отдельные незавершённые ревью выборки курсов.
- Проверить модели EF, запросы и миграции в [`Popo.Storage`](../../src/Popo.Storage/).

### Hxx — внешние клиенты и транспорт

- **H01 `IMoexHttpClient` / `MoexHttpClient` / `MoexIssClient`** — S07, S10–S12 и MOEX jobs J02; каждая строка покрывает контракт, адаптер и одноимённый транспортный метод; кэш/пагинация описаны в [`docs/modules/http-clients.md`](../modules/http-clients.md).
  - [ ] `GetActiveBondsSecuritiesAsync`.
  - [ ] `GetActiveBondsMarketdataYieldsAsync`.
  - [ ] `GetActiveBondsMarketdataAsync`.
  - [ ] `GetFutureAmortsAndCoupons`.
  - [ ] `GetBondHistoryPageAsync`.
  - [x] `GetMoneyMarketFundMarketdataAsync` — проверено 19.10.2026: контракт, адаптер и транспорт запрашивают котировку по `SecId + BoardId`; ответ кэшируется на час, отсутствие строки приводит к явной ошибке.
  - [ ] `GetLqdtIndexHistoryAsync`.
  - [ ] `GetLqdtPriceHistoryAsync`.
  - [ ] `GetBondTradesAsync`.
  - [ ] `GetActiveSecAsync`.
  - [ ] `GetSecurityDescriptionAsync`.
  - [ ] `GetAllEmitentDescriptionAsync`.
  - [ ] `MoexIssClient.GetBondHistoryAsync` — низкоуровневая страница MOEX ISS.
  - [ ] Проверить однозначное соответствие adapter-методов `MoexHttpClient` и транспорта `MoexIssClient`.
- **H02 `ICbondClient` и `ICbrHttpClient`** — источники фоновых J02; retry, маппинг и формат данных.
  - [ ] `CbondClient.GetRatingsAsync`.
  - [ ] `CbrHttpClient.GetCurrenciesAsync`.
  - [ ] `CbrHttpClient.GetCurrencyRatesAsync`.
- **H03 транспортное преобразование и даты**, используется H01/H02.
  - [ ] `MoexMapper.Map<T>`.
  - [ ] `MoexContractMapper.ToContract` — `MoexBond`.
  - [ ] `MoexContractMapper.ToContract` — `MoexHistoryYields`.
  - [ ] `MoexContractMapper.ToContract` — `MoexIndexHistory`.
  - [ ] `MoexContractMapper.ToContract` — `MoexMarketdataYields`.
  - [x] `MoexContractMapper.ToContract` — `MoexMarketdata`; проверено 19.10.2026 для передачи котировки фонда из ISS-модели в Core.
  - [ ] `MoexContractMapper.ToContract` — `MoexTrade`.
  - [ ] `MoexContractMapper.ToContract` — `SecDescription`.
  - [ ] `MoexContractMapper.ToContract` — `MoexBondDescription`.
  - [ ] `MoexContractMapper.ToContract` — `MoexEmitentDescription`.
  - [ ] `MoexContractMapper.ToContract` — `MoexBondization.MoexBondCoupons`.
  - [ ] `MoexContractMapper.ToContract` — `MoexBondization.MoexBondAmortizations`.
  - [ ] `MoexContractMapper.ToContract` — `MoexBondization`.
  - [ ] `MoexResponse.GetTypedData`.
  - [ ] `MoexDateTimeParser.ParseUtc`.
  - [ ] `MoexDateTimeParser.ParseDateOnly`.
  - [ ] `Serialization.MoexDateTimeJsonConverter.Read` / `Write`.
  - [ ] `Serialization.MoexDateOnlyJsonConverter.Read` / `Write`.
  - [ ] `DateOnlyConvertor.Read` / `Write`.
  - [ ] `Contracts.Iss.MoexDateTimeJsonConverter.TryParse` / `Read` / `Write`.
  - [ ] `Contracts.Iss.MoexDateOnlyDateTimeOffsetJsonConverter.TryParse` / `Parse` / `Read` / `Write`.
  - [ ] `AddPopoHttpClients` — регистрация клиентов и сериализации.

### Kxx — Core-калькуляторы и бизнесовые правила

- **K02 Калькуляторы открытой позиции, используются S11.** Проверки открытых лотов и расчёта сделок учтены один раз в K03.
  - [ ] `PositionCalculatorFactory.Create`.
  - [ ] `DefaultPositionCalculator.Calculate`.
  - [ ] `FloaterPositionCalculator.Calculate`.
  - [ ] `ZoPositionCalculator.Calculate`.
  - [ ] `PortfolioMarketPriceCalculator.CalculateTradePricePercent`.
  - [ ] `PortfolioMarketPriceCalculator.CalculatePricePercent`.
  - [ ] `PortfolioMarketPriceCalculator.Calculate`.
  - [ ] `PositionCouponHistoryCalculator.Calculate`.
  - [ ] `ZoPositionCouponEntitlementPolicy.Evaluate`.
  - [ ] `FloaterPositionCouponEntitlementPolicy.Evaluate`.
- **K03 Расчёт и проверка сделок, используются S11, S12 и D07.** Общие для K02/K03 методы лотов и сделок учитываются здесь один раз.
  - [ ] `PositionOpenLotCalculator.Calculate`.
  - [ ] `PortfolioPositionCalculator.Calculate(IReadOnlyCollection<PortfolioTrade>)`.
  - [ ] `PortfolioPositionCalculator.ToRecord`.
  - [ ] `PortfolioPositionCalculator.Calculate(IReadOnlyCollection<PositionIncomeTrade>, ...)` — доход открытой позиции.
  - [ ] `PortfolioPositionCalculator.Calculate(PortfolioPositionRecord, marketPrice)` — оценка позиции.
  - [ ] `PortfolioPositionCalculator.Calculate(PortfolioPositionRecord, marketPrice, averageBuyPriceAtCurrentFaceValue)` — оценка позиции.
  - [ ] `PortfolioPositionsValidate.ValidateTrades`.
  - [ ] `PortfolioPositionsValidate.ValidateTradeSequence`.
  - [ ] `PortfolioPositionsValidate.ValidateTrade`.
  - [ ] `TradeSettlementCalculator.Calculate`.
- **K04 Рекомендации по открытым позициям**, результат читает C07, производит J03.
  - [ ] `PositionRecommendationService.CalculateAsync`.
  - [ ] `PositionRecommendationPolicy.Evaluate`.
- **K05 TWR, вызывается S09.**
  - [ ] `PortfolioReturnCalculator.Calculate`.
- **K06 Benchmark series, вызывается S10.**
  - [ ] `PortfolioBenchmarkCalculator.Calculate`.
  - [ ] `PortfolioBenchmarkSeriesCalculator.Calculate`.
  - [ ] `PortfolioBenchmarkMath.CalculateProfitPercent`.
  - [ ] `PortfolioBenchmarkMath.BuildAssetPoints`.
  - [ ] `PortfolioBenchmarkMath.SimulatePriceSeries`.
  - [ ] `PortfolioBenchmarkMath.EnsureFlowDateValuations`.
  - [ ] `PortfolioBenchmarkMath.FindValuation`.
  - [ ] `PortfolioBenchmarkMath.GetExternalCashMovement`.
  - [ ] `LqdtBenchmarkSeriesCalculator.Calculate`.
  - [ ] `Ru000A10F504BenchmarkSeriesCalculator.FindFirstObservationDate`.
  - [ ] `Ru000A10F504BenchmarkSeriesCalculator.Calculate`.
- **K07 Доходность облигации/YTM, вызывается S07.**
  - `BondAssessmentService.GetAssessmentsAsync` отмечается один раз в S07; здесь проверяются только расчётные методы.
  - [x] `BondYieldCashFlowBuilder.TryBuild` и приватные `IsValidRequest`, `ResolvePrincipalAmount`, `FaceBeforeDate`, `ResolveAnnualRate`.
  - [x] `BondPriceCalculator.CalculateDirtyPrice` — оба overload-а; приватные `CurrenciesMatch` и `NormalizeCurrency`.
  - [x] `YieldCalculator.TryCalculate` — локальные `PresentValue` и `Npv`, приватный `Expm1`.
  - [x] `CashFlow.ValueInRub` — просмотрено как часть контракта K07; фактическое значение может быть в валюте расчётов, см. R10.
- **K08 Закрытые сессии**, вызывается J04; не смешивать с K02/K03.
  - [x] `ClosedPositionSessionsService.RecalculateAsync` — оркестрирует чтение D21, расчёт K08 и атомарную публикацию D15; все три зависимости проверены. Дата передаётся как `MoscowTime.Today`; замечание R07 о некорректных сделках остаётся открытым в калькуляторе.
  - [x] `ClosedPositionSessionCalculator.Calculate` — проверены внутренние `BuildClosedSessions`, `OrderDailyTrades`, `CreateSalePortion`, `CalculateSessionResult` и методы вложенного `Session`: `AddPurchase`, `AddSale`, `RecordHoldingUntil`.
  - [x] `ClosedSessionBondCalculatorFactory.Create`.
  - [x] `DefaultClosedSessionBondCalculator.CalculateCouponIncome`.
  - [x] `FloaterClosedSessionBondCalculator.CalculateCouponIncome`.
  - [x] `ZoClosedSessionBondCalculator.CalculateCouponIncome`.
  - [x] `ClosedSessionCouponAccrualCalculator.Calculate`.
  - [x] `ClosedSessionAmortizationCalculator.Calculate`.
  - [x] `ClosedSessionCurrencyConverter.IsRuble`.
  - [x] `ClosedSessionCurrencyConverter.Convert`.
  - Приватные `GetRate` и `Normalize` проверены в составе `ClosedSessionCurrencyConverter.Convert`.

- **K09 Общие Core-правила**, методы используются несколькими ветками и проверяются здесь один раз.
  - [x] `BondCouponClassifier.IsFloater` — проверен в K08; K02 остаётся отдельным непроверенным caller.
  - [ ] `MoscowTime.AddTimeToToday`.
  - [ ] `MoscowTime.ToUtc(DateTime)`.
  - [ ] `MoscowTime.ToUtc(DateTimeOffset)`.
  - [x] `MoscowTime.ToMoscow(DateTimeOffset)` — использует `TimeZoneInfo.ConvertTime` и `Zone`; проверено в стеке `ClosedPositionSessionsService`.
  - [ ] `MoscowTime.ToMoscow(DateTime)`.
  - [ ] `MoscowTime.MoscowMidnightToUtc`.
  - [ ] `MoscowTime.AtMoscowDateTime(DateOnly, TimeSpan)`.
  - [ ] `MoscowTime.AtMoscowDateTime(DateOnly, DateTime)`.
  - [x] `MoscowTime.Zone` и `MoscowTime.Today` — проверены в стеке `ClosedPositionSessionsService`.
  - [ ] `MoscowTime.Now`.

### Jxx — фоновые производители данных, связанных с ответами API

Один `UpdateAsync` или hosted entry point — один пункт. Повторное упоминание источника в HTTP-ветках не создаёт повторного ревью job.

- **J01 Первичная загрузка и расписание.**
  - [ ] `InitializationBootstrapService.ExecuteAsync`.
  - [ ] `InitializationBootstrap.TryGetJob` — используется S14 для whitelist ключей.
  - [ ] `RecurringJobsScheduler.ScheduleAll`.
  - [ ] `RecurringJobsScheduler.RemoveAll`.
  - [ ] `InitializationProgressReporter.Begin`.
  - [ ] `InitializationProgressReporter.End`.
  - [ ] `InitializationProgressReporter.ReportAsync`.
  - [ ] `ServiceCollectionExtensions.AddPopoDataServices` — регистрация Jobs.
  - [ ] `MapConfig.Register` — AutoMapper profiles.
- **J02 MOEX справочник и история.**
  - [ ] `SecUpdateJob.UpdateAsync`.
  - [ ] `MoexBondSecuritiesUpdateJob.UpdateAsync`.
  - [ ] `MoexBondUpdateJob.UpdateAsync`.
  - [ ] `MoexEmitentUpdateJob.UpdateAsync`.
  - [ ] `MoexHistoryPricesUpdateJob.UpdateAsync`.
  - [ ] `MoexIndexHistoryUpdateJob.UpdateAsync`.
  - [ ] `MoexAmortsAndCouponsUpdateJob.UpdateAsync`.
- **J02 рейтинги и курсы.**
  - [ ] `BondRatingUpdateJob.UpdateAsync`.
  - [ ] `CbrCurrencyRatesUpdateJob.UpdateAsync`.
- [ ] **J03 Рекомендации открытых позиций** — `PositionRecommendationJob.UpdateAsync` → K04 → D14; статус/ошибки и stale-поведение.
- [ ] **J04 Пересчёт закрытых сессий** — `ClosedPositionSessionsJob.UpdateAsync` → D21 → K08 → D15; очередь, блокировка, готовность initialization.
- [ ] Сверить регистрации Jobs и фактический порядок/расписание в [`Popo.Jobs/Program.cs`](../../src/Popo.Jobs/Program.cs), [`RecurringJobsScheduler.cs`](../../src/Popo.Jobs/RecurringJobsScheduler.cs), [`ServiceCollectionExtensions.cs`](../../src/Popo.Jobs/Jobs/ServiceCollectionExtensions.cs).

## Индекс пересечений

| Канонический узел | Вызовы/читатели | Правило |
|---|---|---|
| S01 / D01 `BondsService` / `BondsProvider` | C01, C03 через S05, C08 через S12; `GetSecurities` вызывается напрямую из S07.BondAssessmentService, который используется C03/J03 | Методы `BondsService` и четыре соответствующих метода `BondsProvider` проверяются по одному разу. `GetSecurities` — отдельная непроверенная операция `IBondsProvider`, не метод `BondsService`. |
| S11.GetMoneyMarketFundsAsync | C02 через S02; C06 через S08 → S11.GetOverviewAsync | Это один метод `PortfolioPositionsService`, общий для обеих страниц; его зависимости D03/H01 раскрыты один раз. |
| D03.GetMoneyMarketFundsAsync | S03.AddMoneyMarketFundAsync и UpdateMoneyMarketFundAsync для проверки дубля; S11.GetMoneyMarketFundsAsync для чтения/оценки позиции | Один provider-метод используется операциями изменения и обеими страницами. |
| H01.GetMoneyMarketFundMarketdataAsync | S11.GetMoneyMarketFundsAsync → C02; тот же S11-метод → C06 | Одна котировка фонда на двух HTTP-путях. |
| D11.GetPresetsAsync | C03 через S05 и через S06.GetPresetsAsync | Один storage-метод, два разных вызова. |
| D11.GetSettingsAsync | C03 через S06/S07; J03 через PositionRecommendationService | Один storage-метод, используется настройками и обеими рекомендационными ветками. |
| S07.BondAssessmentService.GetAssessmentsAsync | C03 через CashRecommendationService; J03 через PositionRecommendationService | Общий Core-метод расчёта оценок; провайдеры и клиенты внутри него не дублируют ревью. |
| S11 `PortfolioPositionsService` | C07, S02, S08 | Публичные методы сервиса проверять один раз; ветки используют нужный метод и свой контракт ответа. |
| D03 `PortfolioMoneyMarketFundsProvider` | S03, S11 (S02 транзитивно) | Один provider; чтение и CRUD текущих позиций проверяются в своих сервисных ветках. |
| D04 `PortfolioTradesProvider` | S11, S12 | Один provider; сценарии позиций и сделок не дублируют его ревью. |
| D04.GetTradesAsync | S11.GetPageAsync и S12.GetTradesAsync | Один запрос журнала используется расчётом позиции и страницей сделок. |
| D05 `PortfolioCashFlowsProvider` | S08–S10 | Один provider; проверить разные потребители по их S-узлам. |
| D05.GetCashFlowsAsync | S08.GetAsync, S09.CalculateReturnAsync, S10.CalculateAsync | Чтение одних денежных потоков для страницы, TWR и сравнения бенчмарков. |
| D06 `PortfolioValuationsProvider` | S08–S10 | Один provider для страницы, CRUD, TWR и benchmark. |
| D06.GetValuationsAsync | S08.GetAsync, S09.AddValuationAsync/UpdateValuationAsync/CalculateReturnAsync, S10.CalculateAsync | Один список снимков используется страницей, проверками CRUD, TWR и бенчмарком. |
| D07.GetPositionsAsync | S11.GetPageAsync/GetOverviewAsync, J03.PositionRecommendationService | Один расчёт позиций используется HTTP-позицией, сводкой портфеля и рекомендациями. |
| D09 `HistoryProvider` | S07, S11 | Общий источник объёмов; S07 оценивает кандидатов, S11 рассчитывает позиции. |
| D10 `DailyVolumeStatisticsStore` | D09, J02 | J02 обновляет статистику, D09 читает её; writer и reader сверяются без повторного ревью хранилища. |
| D11 `InvestmentStrategySettingsStore` | S05–S07, J03 | Store один раз; `GetPresetsAsync` разделяется S05/S06, `GetSettingsAsync` — S06/S07/J03. |
| D14 `PositionRecommendationStore` | S11, J03 | J03 публикует снимок, S11 читает его для C07. |
| D15 `ClosedPositionSessionsProvider` | S13, J04 | S13.GetAsync читает опубликованный снимок и проверен; K08 service вызывает проверенный ReplaceAsync. Оркестрация J04 остаётся открытой. |
| D16 `InitializationStateStore` | S13, S14, J01, J03, J04 | Общие состояния/переходы проверять в одном месте, затем сверить каждого caller. |
| D18 `MoexIndexHistoryStore` | S10, J02 | S10 обновляет и читает историю LQDT, J02 также синхронизирует её. |
| D19 чтение `CurrencyRates` | S11.GetPageAsync/GetOverviewAsync, D21.GetAsync | Одну таблицу читают три независимых call sites; проверять каждый фильтр в его вызывающей ветке, не считать это общим методом. |
| D21 `ClosedPositionSessionSourceProvider` | J04 → K08 | Источник входных данных пересчёта; D15 отдельно отвечает за публикацию снимка. |
| D20 Hangfire client/storage | S13, J04 | Enqueue/status проверять отдельно от реализации самого фонового расчёта. |
| H01 `MoexHttpClient` | S07, S10–S12, J02 | Общий клиент; каждый consumer дополнительно проверяет семантику конкретного запроса.
| K03 `PortfolioPositionsValidate` | D07 и сделки S12 | Алгоритм один раз; consumers проверять только на корректное использование результата.
| K09 `MoscowTime` / `BondCouponClassifier` | S07, S11, S14, K02, K08, J01–J04, H03 | Проверены BondCouponClassifier.IsFloater и используемая D21 дата: MoscowTime.Today → ToMoscow(DateTimeOffset) → Zone; остальные методы времени остаются отдельными. |

## Замечания

Записи ниже относятся к проверенным endpoint-ам и общим узлам. Галочки показывают проведённое ревью; открытые замечания требуют отдельного решения или исправления.

| ID | Замечание | Решение/статус |
|---|---|---|
| R01 | **Клиент и API расходились по минимальной длине.** [`trades/page.tsx`](../../src/Popo.Frontend/src/app/trades/page.tsx) отправлял поиск от 2 символов, а `BondSearchRequestValidator` принимает от 3. | **Исправлено:** общий порог 3 символа; короткий ввод очищает варианты и не отправляется в API. Frontend-тесты прошли. |
| R02 | **Неизвестный НКД превращался в ноль.** Provider отображал `AccruedInt == null` в `0`, и клиент терял различие между нулём и отсутствующими данными. | **Исправлено:** Core/API возвращают nullable `accruedInterest`, Storage сохраняет null, форма требует ручной ввод при неизвестном НКД. Storage-тест добавлен и прошёл. |
| R03 | **Покрытие поиска было частичным.** Не было успешного теста действия контроллера, поиска по `ShortName`, пустого результата, null НКД и изоляции тестовых наборов. Тест валюты не проверял fallback. | **Исправлено и проверено:** добавлены тесты controller и provider; Storage-тесты используют отдельную схему и уникальные SECID на тест; тест валюты проверяет прямое значение. Успешно прошли .NET (406), Storage (37) и Frontend (43) теста; `dotnet ef migrations has-pending-model-changes` подтвердил отсутствие несозданной миграции. |
| R04 | **Гонка при добавлении или изменении фонда.** `MoneyMarketFundManagementService.AddMoneyMarketFundAsync` и `UpdateMoneyMarketFundAsync` сначала читают список и проверяют дубликат, затем выполняют INSERT/UPDATE. Параллельные запросы Add/Add или Add/Update могут пройти pre-check; уникальный индекс `SecId` отклонит вторую запись как `DbUpdateException`. | **Открыто:** `ApiExceptionHandler` не распознаёт нарушение уникальности БД и вернёт 500 вместо 409. HTTP-тест PUT проверяет успешное сохранение; интеграционного теста успешного POST/повторного POST и теста конкурентного Add/Update нет. Рассмотреть перевод конкретного конфликта в `ApplicationConflictException` и покрытие конкурирующих запросов. |
| R05 | **Не проверены Storage-запросы справочников валют.** `BondsProvider.GetTradingCurrenciesAsync` и `GetFaceUnitsAsync` фильтруют пустые значения, удаляют дубликаты и сортируют результат, но прямых Storage-тестов этих запросов нет. | **Открыто:** добавить изолированные тесты провайдера для null/пустых значений, дубликатов и порядка выдачи; текущий контракт страницы проверяет только ответный DTO, не EF-запросы. |
| R06 | **Документация API перечисляет несуществующие маршруты.** `docs/modules/api.md` заявляет `/api/bonds/currencies`, `/api/bonds/face-units` и `/api/positions/recommendation`, но актуальные контроллеры их не объявляют. | **Открыто:** актуализировать список маршрутов в документации API. |
| R07 | **Некорректные сделки могут быть молча пропущены при расчёте закрытой сессии.** `ClosedPositionSessionCalculator.OrderDailyTrades` не создаёт порции для неизвестного `TradeSide` и некорректного отрицательного количества продажи. | **Открыто, P3:** обычный API-путь валидирует сделки, но повреждённые/напрямую добавленные данные могут дать неполный результат без ошибки; решить, нужно ли явно отклонять такие строки и покрыть этот путь тестом. |
| R08 | **Корень YTM может не находиться при очень малых денежных суммах.** В `YieldCalculator.TryCalculate` проверка одинакового знака через произведение `lowerNpv * upperNpv` может получить underflow в ноль и пропустить расширение скобки. | **Открыто, P3:** сравнивать знаки без перемножения и добавить тест масштаба, при котором произведение underflow-ится. Пример из ревью: цена `1e-200`, поток `2e-200` через год; ожидаемая доходность 100%. |
| R09 | **Нулевой далёкий поток может сорвать корректный YTM.** `YieldCalculator.PresentValue` проверяет переполнение экспоненты до умножения на сумму потока; нулевой поток на достаточно далёкой дате может вернуть `+Infinity`. | **Открыто, P3:** пропускать нулевые потоки или учитывать сумму при проверке переполнения и покрыть крайний срок. Пример из ревью: dirty price 3000, поток 1000 через год и нулевой поток через 1000 лет; корень соответствует доходности −66,67%. |
| R10 | **Имя `CashFlow.ValueInRub` не соответствует валюте данных в K07.** `CashFlowProvider.GetYieldSchedule` конвертирует потоки в `request.SettlementCurrency`, которая может быть не RUB. | **Открыто, P3:** переименовать в `ValueInSettlementCurrency` либо явно документировать инвариант. В текущем пути цена и потоки имеют одну валюту, поэтому это проблема ясности контракта, а не подтверждённая ошибка результата. |
| R11 | **Изменяется точность хранения закрытых сессий.** Миграция `20261020180243_ClosedPositionSessionFieldsUseDouble` меняет `numeric(28,8)` на `double precision`; при преобразовании возможна потеря десятичной точности. | **Открыто:** до применения в рабочей БД подтвердить приемлемость округления; `ClosedPositionSessions` — производный снимок, его можно пересчитать из исходных данных. Миграция пока не применялась. |

## Проверено

Галочки отражают завершённое ревью, включая тонкие предки, проверенные после прохода снизу вверх. Проверка одного метода не выставляет отметку всему классу, если у него остаются непроверенные методы.

- [x] **C01 `BondsController.Search`.** HTTP-вход и маршрут проверены.
- [x] **S01 `BondsService` — все четыре публичных метода:** `SearchAsync`, `GetHistoricalFaceValuesAsync`, `GetTradingCurrenciesAsync`, `GetFaceUnitsAsync`.
- [x] **D01 `BondsProvider` — четыре соответствующих метода:** `SearchAsync`, `GetHistoricalFaceValuesAsync`, `GetTradingCurrenciesAsync`, `GetFaceUnitsAsync`. `GetSecurities` не входит в стек `BondsService` и остаётся непроверенным.
- [x] **C02 `LiquidityController` — все четыре маршрута:** получение страницы, добавление, изменение и удаление фонда.
- [x] **S02 `MoneyMarketFundsPageService.GetAsync`; S03 `MoneyMarketFundManagementService` — `AddMoneyMarketFundAsync`, `UpdateMoneyMarketFundAsync`, `DeleteMoneyMarketFundAsync`; S11 `PortfolioPositionsService.GetMoneyMarketFundsAsync`.**
- [x] **D03 `PortfolioMoneyMarketFundsProvider` — все четыре CRUD-метода.**
- [x] **H01 `GetMoneyMarketFundMarketdataAsync` и H03 `MoexContractMapper.ToContract(MoexMarketdata)`.**
- [x] **Общий HTTP-контур:** `FluentValidationActionFilter.OnActionExecutionAsync`, `UpsertMoneyMarketFundRequestValidator`, `ApiExceptionHandler.TryHandleAsync`.
- [x] **K08 `ClosedPositionSessionCalculator` и весь вызываемый им стек:** `Calculate` и внутренние помощники; выбор типа калькулятора, расчёт купонов/амортизации/валют; общий `BondCouponClassifier.IsFloater`. Проверено 20.10.2026; открытое замечание по некорректным сделкам записано как R07.
- [x] **K07 `BondPriceCalculator`, `YieldCalculator` и нижняя ветка до D13:** оба overload-а dirty price; YTM solver и его helpers; `BondYieldCashFlowBuilder` и helpers; `CashFlowProvider.GetYieldSchedule` и вызываемые им конвертеры валют. Проверено 20.10.2026; численные граничные дефекты и неоднозначное имя контракта оставлены открытыми как R08–R10. Проверка стека не означает, что эти замечания исправлены или что верхняя ветка S07/C03 проверена.
- [x] **D15 `ClosedPositionSessionsProvider` — `GetAsync` и `ReplaceAsync` до таблицы `ClosedPositionSessions`:** проверены чтение, упорядочивание, полное отображение полей, транзакционная замена и тесты round-trip/rollback. R11 отдельно фиксирует точность преобразования при миграции.
- [x] **D21 `ClosedPositionSessionSourceProvider.GetAsync` и весь нижний стек:** проверены private helpers, согласованное чтение шести исходных таблиц, фильтры по `asOf`, подбор режима торгов и группировка данных по `SecId`. Будущие купоны сохраняются для расчёта периода, амортизации и курсы ограничены `asOf`; тесты покрывают границу даты, раздельные ключи инструмента и валютную конвертацию. Запросы CurrencyRates в S11 не отмечены: это отдельные call sites D19.
- [x] **K08 `ClosedPositionSessionsService.RecalculateAsync` и его нижний стек:** проверены передача московской даты, общий `CalculatedAt`, расчёт всех инструментов до публикации и замена D15 только после успешного расчёта; тесты покрывают публикацию всех результатов и отсутствие частичной публикации при ошибке.
- [x] **Автопроверка предков:** C04 `GET /api/closed-position-sessions` и S13 `ClosedPositionSessionsPageService.GetAsync` — тонкие делегаты проверенного D15.GetAsync. Остальные методы S13, маршруты C04 и J04 не отмечены: у них остаются самостоятельные проверки готовности, валидация статуса, работа с Hangfire и распределённой блокировкой.

`CashRecommendationsController.GetPage` и его стек теперь явно показаны в C03, но эта ветка остаётся непроверенной: ранее по ней запрашивалось документирование методов, а не отметка ревью.
