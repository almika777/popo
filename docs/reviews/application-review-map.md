# Карта ревью контроллеров и методов приложения

Срез актуализирован по структуре рабочей копии на 19.10.2026 после удаления кеша со страницы «Ликвидность» и изменения сводки портфеля. Карта описывает маршруты вызовов; состояние ревью и замечания фиксируются в этом же файле.
Включены публичные production-методы достижимых от HTTP-входов и связанных фоновых producer-ов классов; DTO/record-конструкторы и сгенерированный EF-код не являются отдельными пунктами ревью.

## Этапы и состояние

- [x] **Этап 1. Инвентаризация и карта зависимостей** — актуализирован 19.10.2026: 8 контроллеров и 35 HTTP-методов; учтены текущие application/Core/Storage/HttpClients-узлы и фоновые производители данных.
- [ ] **Этап 2. Ревью HTTP-методов контроллеров** — начат 17.10.2026; C01 и связанные методы проверены, R01–R03 исправлены.
- [ ] **Этап 3. Ревью уникальных application- и Core-методов** — после этапа 2.
- [ ] **Этап 4. Ревью providers, HTTP-клиентов и фоновых производителей данных** — общие узлы отмечать только один раз.
- [ ] **Этап 5. Сводка замечаний и решение по исправлениям**.

**Текущий статус:** этап 2; `BondsController.Search`, `BondsService.SearchAsync` и `BondsProvider.SearchAsync` проверены и выделены в схеме. R01–R03 исправлены; запуск Storage-интеграционных тестов ожидает доступа к Docker API. Денежные остатки и снимки кеша удалены; фонды, cash-рекомендации и денежные потоки портфеля остаются отдельными действующими ветками.

## Как пользоваться картой

1. Начинать с маршрутов в дереве контроллеров. Отмечать endpoint после проверки binding, request/response, валидации, статусов и поведения ошибок. Галочка означает, что ревью проведено; открытые замечания фиксируются отдельно и не снимают галочку.
2. Обозначения `Sxx`, `Dxx`, `Kxx` и `Jxx` — ссылки на канонические узлы ниже. Общий узел раскрывается и ревьюится только в одном месте; на остальных ветках повторяется только его ID.
3. При проверке маршрута не отмечать повторно сервисы и providers. Их чекбоксы находятся в своих разделах.
4. Лист дерева — публичный метод класса. Для provider-метода проверить контракт Core, реализацию Storage и используемые им запросы/транзакции. Для общего клиента или калькулятора метод проверяется один раз, на остальных ветках остаётся ссылка на его ID.
5. В конце фиксировать найденное в разделе «Замечания». Непроверенные узлы оставлять пустыми.
6. Пропуски в ID оставлены намеренно: удалённые узлы не перенумеровываются, чтобы не менять ссылки и историю уже проведённого ревью.

## Дерево HTTP-входов

```text
Popo.Api
├── C01 BondsController (/api/bonds)
│   └── GET /search → S01
├── C02 LiquidityController (/api/liquidity)
│   ├── GET    /money-market-funds/page       → S02
│   ├── POST   /money-market-funds            → S03
│   ├── PUT    /money-market-funds/{id}       → S03
│   ├── DELETE /money-market-funds/{id}       → S03
│   └── POST   /money-market-fund-operations  → S03
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
└── GET /health → Popo.Api/Program.cs
```

## Этап 2 — контроллеры и HTTP-контракт

Проверять endpoint-методы отдельно; ID после стрелки ведёт к единственному пункту ревью реализации.

### C01 — `BondsController`

Файл: [`BondsController.cs`](../../src/Popo.Api/Controllers/BondsController.cs)

- [x] `GET /api/bonds/search` — `Search` → `S01.BondsService.SearchAsync`; проверено 17.10.2026, связанные R01–R03 исправлены.

### C02 — `LiquidityController`

Файл: [`LiquidityController.cs`](../../src/Popo.Api/Controllers/LiquidityController.cs)

- [ ] `GET /api/liquidity/money-market-funds/page` — `GetMoneyMarketFundsPage` → `S02.MoneyMarketFundsPageService.GetAsync`.
- [ ] `POST /api/liquidity/money-market-funds` — `AddMoneyMarketFund` → `S03.AddMoneyMarketFundAsync`.
- [ ] `PUT /api/liquidity/money-market-funds/{id}` — `UpdateMoneyMarketFund` → `S03.UpdateMoneyMarketFundAsync`.
- [ ] `DELETE /api/liquidity/money-market-funds/{id}` — `DeleteMoneyMarketFund` → `S03.DeleteMoneyMarketFundAsync`.
- [ ] `POST /api/liquidity/money-market-fund-operations` — `AddMoneyMarketFundOperation` → `S03.AddMoneyMarketFundOperationAsync`.

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

- [ ] `GET /api/closed-position-sessions` — `Get` → `S13.GetAsync`.
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

- [ ] `FluentValidationActionFilter.OnActionExecutionAsync` — [`FluentValidationActionFilter.cs`](../../src/Popo.Api/Filters/FluentValidationActionFilter.cs).
- [ ] Валидаторы `BondSearchRequest`, strategy settings/preset, fund/operation, valuation/cash-flow/return query и trade — папка [`Validators`](../../src/Popo.Api/Validators/); проверить правила в их `AbstractValidator`-конструкторах.
- [ ] `ApiExceptionHandler.TryHandleAsync`, типизированные ошибки Core и соответствие статусов ADR-0004 — [`ApiExceptionHandler.cs`](../../src/Popo.Api/ApiExceptionHandler.cs), [`ADR-0004`](../adr/0004-обработка-ошибок-api-вне-контроллеров.md).
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
- [ ] Сверить `docs/modules/api.md` с действующими `[Route]`/`[Http*]`. В документации перечислены дополнительные маршруты `/api/bonds/currencies`, `/api/bonds/face-units`, `/api/portfolio/summary` и `/api/positions/recommendation`; подтвердить их наличие или отметить документацию к обновлению.

## Этап 3 — канонические application/Core-узлы

Каждый метод приложения отмечается здесь ровно один раз. Ветви контроллеров ссылаются на эти пункты по ID.

### S01 — каталог облигаций, Core `BondsService`

Используется C01, S05, S07 и S12. Реализация: [`BondsService.cs`](../../src/Popo.Core/Bonds/BondsService.cs).

- [x] `SearchAsync` — проверен 17.10.2026; согласован с API-контрактом nullable НКД.
- [ ] `GetHistoricalFaceValuesAsync`.
- [ ] `GetTradingCurrenciesAsync`.
- [ ] `GetFaceUnitsAsync`.

### S02 — страница фондов, `MoneyMarketFundsPageService`

Файл: [`MoneyMarketFundsPageService.cs`](../../src/Popo.Api/Services/MoneyMarketFundsPageService.cs). Связи: D03, S11.

- [ ] `GetAsync`; проверить параллельную загрузку и согласованность всех частей ответа.

### S03 — изменение позиций и операций фондов, `MoneyMarketFundManagementService`

Файл: [`MoneyMarketFundManagementService.cs`](../../src/Popo.Api/Services/MoneyMarketFundManagementService.cs). Связи: D03.

- [ ] `AddMoneyMarketFundAsync`.
- [ ] `UpdateMoneyMarketFundAsync`.
- [ ] `DeleteMoneyMarketFundAsync`.
- [ ] `AddMoneyMarketFundOperationAsync`.

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

Файлы: [`CashRecommendationService.cs`](../../src/Popo.Core/Recommendations/CashRecommendationService.cs), [`BondAssessmentService.cs`](../../src/Popo.Core/Recommendations/BondAssessmentService.cs). Источники: D01, D09–D13, H01; денежные потоки и доходность — K07.

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

Файл: [`PortfolioPositionsService.cs`](../../src/Popo.Api/Services/Position/PortfolioPositionsService.cs). Использует D03–D04, D07–D09, D14, D19, H01 и K02–K03. Также вызывается S02 и S08.

- [ ] `GetPageAsync`.
- [ ] `GetPositionsAsync` — проверить контракт; прямого production-вызова из контроллеров в текущей карте нет.
- [ ] `GetOverviewAsync(asOf)` — портфельная сводка.
- [ ] `GetMoneyMarketFundsAsync` — котировки и оценка фондов для C02.
- [ ] Загрузка источников, выбор калькулятора, цены, валют, купонов и fallback-пути — проверить внутри соответствующих методов выше.

### S12 — журнал сделок, `PortfolioTradesService`

Файл: [`PortfolioTradesService.cs`](../../src/Popo.Api/Services/Trades/PortfolioTradesService.cs). Зависимости D01, D03, H01, K03.

- [ ] `GetTradesAsync`.
- [ ] `AddTradeAsync`.
- [ ] `UpdateTradeAsync`.
- [ ] `DeleteTradeAsync`.
- [ ] `UseFaceValueAtTradeDateAsync` — приватный helper; проверять внутри Add/Update, не заводить отдельную ветку.

### S13 — закрытые сессии, `ClosedPositionSessionsPageService`

Файл: [`ClosedPositionSessionsPageService.cs`](../../src/Popo.Api/Services/ClosedPositions/ClosedPositionSessionsPageService.cs). Чтение D15, проверка состояния D16, постановка/статус Hangfire-задачи через D20, пересчёт J04.

- [ ] `GetAsync`.
- [ ] `RecalculateAsync`.
- [ ] `GetStatus(jobId)`.

### S14 — инициализация, `InitializationService`

Файл: [`InitializationService.cs`](../../src/Popo.Api/Services/InitializationService.cs). Состояние D16, оркестратор первичной загрузки J01.

- [ ] `GetStatusAsync`.
- [ ] `RetryJobAsync` — проверить whitelist ключей, переход состояний и фактическое поведение retry.

## Этап 4 — общие providers, клиенты и расчётные узлы

### Dxx — Storage/источники. Каждый публичный метод проверяется один раз

- **D01 `BondsProvider`** (`IBondsProvider`) — S01, S07, S12 через S01; справочники и исторические номиналы.
  - [x] `SearchAsync` — проверен 17.10.2026; НКД сохраняет null, тесты расширены.
  - [ ] `GetHistoricalFaceValuesAsync`.
  - [ ] `GetTradingCurrenciesAsync`.
  - [ ] `GetFaceUnitsAsync`.
  - [ ] `GetSecurities`.
- **D03 `PortfolioMoneyMarketFundsProvider`** (`IPortfolioMoneyMarketFundsProvider`) — S02, S03, S11.
  - [ ] `GetMoneyMarketFundsAsync`.
  - [ ] `GetMoneyMarketFundOperationsAsync`.
  - [ ] `AddMoneyMarketFundOperationAsync`.
  - [ ] `AddMoneyMarketFundAsync`.
  - [ ] `UpdateMoneyMarketFundAsync`.
  - [ ] `DeleteMoneyMarketFundAsync`.
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
  - [ ] `GetYieldSchedule`.
- **D14 `PositionRecommendationStore`** (`IPositionRecommendationStore`) — S11 читает снимок, J03 пишет.
  - [ ] `GetAsync`.
  - [ ] `StartAsync`.
  - [ ] `CompleteAsync`.
  - [ ] `FailAsync`.
- **D15 `ClosedPositionSessionsProvider`** (`IClosedPositionSessionsProvider`) — S13 читает, J04 атомарно заменяет снимок.
  - [ ] `GetAsync`.
  - [ ] `ReplaceAsync`.
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
- **D19 Прямой запрос валютных курсов через `IDbContextFactory<PopoDbContext>`** — часть `S11.GetMoneyMarketFundsAsync`; отдельного Storage provider нет.
- **D20 Hangfire `IBackgroundJobClient` / `JobStorage`** — внешние методы постановки/чтения статуса в S13; J04 исполняет задачу.
- **D21 `ClosedPositionSessionSourceProvider`** — J04 читает здесь сделки, купоны, амортизации и валютные курсы для K08.
  - [ ] `GetAsync`.
- Проверить модели EF, запросы и миграции в [`Popo.Storage`](../../src/Popo.Storage/).

### Hxx — внешние клиенты и транспорт

- **H01 `IMoexHttpClient` / `MoexHttpClient` / `MoexIssClient`** — S07, S10–S12 и MOEX jobs J02; каждая строка покрывает контракт, адаптер и одноимённый транспортный метод; кэш/пагинация описаны в [`docs/modules/http-clients.md`](../modules/http-clients.md).
  - [ ] `GetActiveBondsSecuritiesAsync`.
  - [ ] `GetActiveBondsMarketdataYieldsAsync`.
  - [ ] `GetActiveBondsMarketdataAsync`.
  - [ ] `GetFutureAmortsAndCoupons`.
  - [ ] `GetBondHistoryPageAsync`.
  - [ ] `GetMoneyMarketFundMarketdataAsync`.
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
  - [ ] `MoexContractMapper.ToContract` — `MoexMarketdata`.
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
  - [ ] `MoneyMarketFundOperationCalculator.CalculatePosition`.
  - [ ] `MoneyMarketFundOperationCalculator.Amount`.
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
  - [ ] `BondYieldCashFlowBuilder.TryBuild`.
  - [ ] `BondPriceCalculator.CalculateDirtyPrice` — overload с `accruedInterest`.
  - [ ] `BondPriceCalculator.CalculateDirtyPrice` — overload с `faceUnit` и `currencyId`.
  - [ ] `YieldCalculator.TryCalculate`.
- **K08 Закрытые сессии**, вызывается J04; не смешивать с K02/K03.
  - [ ] `ClosedPositionSessionsService.RecalculateAsync`.
  - [ ] `ClosedPositionSessionCalculator.Calculate`.
  - [ ] `ClosedSessionBondCalculatorFactory.Create`.
  - [ ] `DefaultClosedSessionBondCalculator.CalculateCouponIncome`.
  - [ ] `FloaterClosedSessionBondCalculator.CalculateCouponIncome`.
  - [ ] `ZoClosedSessionBondCalculator.CalculateCouponIncome`.
  - [ ] `ClosedSessionCouponAccrualCalculator.Calculate`.
  - [ ] `ClosedSessionAmortizationCalculator.Calculate`.
  - [ ] `ClosedSessionCurrencyConverter.IsRuble`.
  - [ ] `ClosedSessionCurrencyConverter.Convert`.

- **K09 Общие Core-правила**, методы используются несколькими ветками и проверяются здесь один раз.
  - [ ] `BondCouponClassifier.IsFloater` — K02 и K08.
  - [ ] `MoscowTime.AddTimeToToday`.
  - [ ] `MoscowTime.ToUtc(DateTime)`.
  - [ ] `MoscowTime.ToUtc(DateTimeOffset)`.
  - [ ] `MoscowTime.ToMoscow(DateTimeOffset)`.
  - [ ] `MoscowTime.ToMoscow(DateTime)`.
  - [ ] `MoscowTime.MoscowMidnightToUtc`.
  - [ ] `MoscowTime.AtMoscowDateTime(DateOnly, TimeSpan)`.
  - [ ] `MoscowTime.AtMoscowDateTime(DateOnly, DateTime)`.
  - [ ] Свойства `MoscowTime.Zone`, `Now`, `Today` — общие источники времени.

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
| S01 / D01 `BondsService` / `BondsProvider` | C01, S05, S07, S12 | Ревью сервиса и provider один раз; call sites проверять в своих endpoint-ветках. |
| S11 `PortfolioPositionsService` | C07, S02, S08 | Публичные методы сервиса проверять один раз; ветки используют нужный метод и свой контракт ответа. |
| D03 `PortfolioMoneyMarketFundsProvider` | S02, S03, S11 | Один provider; сценарии чтения/CRUD/операций остаются в сервисах. |
| D04 `PortfolioTradesProvider` | S11, S12 | Один provider; сценарии позиций и сделок не дублируют его ревью. |
| D05 `PortfolioCashFlowsProvider` | S08–S10 | Один provider; проверить разные потребители по их S-узлам. |
| D06 `PortfolioValuationsProvider` | S08–S10 | Один provider для страницы, CRUD, TWR и benchmark. |
| D09 `HistoryProvider` | S07, S11 | Общий источник объёмов; S07 оценивает кандидатов, S11 рассчитывает позиции. |
| D10 `DailyVolumeStatisticsStore` | D09, J02 | J02 обновляет статистику, D09 читает её; writer и reader сверяются без повторного ревью хранилища. |
| D11 `InvestmentStrategySettingsStore` | S05–S07 | Store один раз; CRUD и чтение активных настроек проверять на соответствующих S-узлах. |
| D14 `PositionRecommendationStore` | S11, J03 | J03 публикует снимок, S11 читает его для C07. |
| D15 `ClosedPositionSessionsProvider` | S13, J04 | S13 читает опубликованный снимок, J04 атомарно заменяет его после расчёта. |
| D16 `InitializationStateStore` | S13, S14, J01, J03, J04 | Общие состояния/переходы проверять в одном месте, затем сверить каждого caller. |
| D18 `MoexIndexHistoryStore` | S10, J02 | S10 обновляет и читает историю LQDT, J02 также синхронизирует её. |
| D21 `ClosedPositionSessionSourceProvider` | J04 → K08 | Источник входных данных пересчёта; D15 отдельно отвечает за публикацию снимка. |
| D20 Hangfire client/storage | S13, J04 | Enqueue/status проверять отдельно от реализации самого фонового расчёта. |
| H01 `MoexHttpClient` | S07, S10–S12, J02 | Общий клиент; каждый consumer дополнительно проверяет семантику конкретного запроса.
| K03 `PortfolioPositionsValidate` | D07 и сделки S12 | Алгоритм один раз; consumers проверять только на корректное использование результата.
| K09 `MoscowTime` / `BondCouponClassifier` | S07, S11, S14, K02, K08, J01–J04, H03 | Общие календарные и классификационные правила проверять один раз. |

## Замечания

Записи ниже относятся к первому проверенному endpoint и его провайдеру. Галочки показывают проведённое ревью; открытые замечания требуют отдельного решения или исправления.

| ID | Замечание | Решение/статус |
|---|---|---|
| R01 | **Клиент и API расходились по минимальной длине.** [`trades/page.tsx`](../../src/Popo.Frontend/src/app/trades/page.tsx) отправлял поиск от 2 символов, а `BondSearchRequestValidator` принимает от 3. | **Исправлено:** общий порог 3 символа; короткий ввод очищает варианты и не отправляется в API. Frontend-тесты прошли. |
| R02 | **Неизвестный НКД превращался в ноль.** Provider отображал `AccruedInt == null` в `0`, и клиент терял различие между нулём и отсутствующими данными. | **Исправлено:** Core/API возвращают nullable `accruedInterest`, Storage сохраняет null, форма требует ручной ввод при неизвестном НКД. Добавлен Storage-тест; запуск заблокирован отказом Docker API. |
| R03 | **Покрытие поиска было частичным.** Не было успешного теста действия контроллера, поиска по `ShortName`, пустого результата, null НКД и изоляции тестовых наборов. Тест валюты не проверял fallback. | **Исправлено в тестовом наборе:** добавлены тесты controller и provider; Storage-тесты используют отдельную схему и уникальные SECID на тест; тест валюты переименован и проверяет прямое значение. Frontend: 42 теста пройдены; .NET/Storage-тесты не подтверждены из-за отказа доступа к Docker API и сбоя запуска `dotnet test`. |
