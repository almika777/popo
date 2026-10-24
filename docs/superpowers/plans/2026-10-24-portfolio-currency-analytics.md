# Аналитика портфеля по валюте — план реализации

> **Для исполнителей:** использовать `superpowers:executing-plans` и выполнять задачи по порядку с циклом TDD.

**Цель:** добавить на `/portfolio` вкладку «Аналитика» с диаграммой долей всего портфеля по валюте и временными ручными входными данными.

**Архитектура:** чистый калькулятор в Core агрегирует оценки по валютам и формирует неполные результаты с причинами неоценённости. API-служба собирает текущие позиции, фонды, валютные курсы и параметры запроса; ручные данные не сохраняются. Frontend добавляет отдельный компонент вкладки, хранит ввод только в состоянии страницы и отправляет параметры на stateless endpoint.

**Стек:** .NET 10, ASP.NET Core, EF Core, NUnit, Next.js 16.4.2, React 19, Ant Design 6, Recharts 3.10.1, встроенный Node.js test runner.

**Спецификация:** [2026-10-24-portfolio-currency-analytics-design.md](../specs/2026-10-24-portfolio-currency-analytics-design.md)

## Глобальные ограничения

- Денежные остатки и ручные оценки используются только как параметры расчёта и нигде не сохраняются.
- Валюта группы позиции — валюта номинала бумаги (`FaceUnit`), если она задана; иначе используется валюта торгов (`CurrencyId`).
- Наличие валюты номинала определяет группу, но рыночная оценка пересчитывается в RUB по курсу валюты торгов.
- Ручная цена — денежная стоимость одной бумаги в валюте торгов и применяется только при недоступной рыночной цене.
- Ручной курс задаётся как количество RUB за единицу валюты и применяется только при недоступном актуальном курсе.
- Для текущих фондов LQDT/TMON валюта инструмента — RUB.
- Неполный срез явно показывает оценённую часть и неоценённые активы с причинами.
- Перезагрузка или уход со страницы сбрасывает денежные остатки и ручные параметры.
- При ошибке загрузки или расчёта не показывать нулевой портфель как успешный результат.

## Review Focus

- Номинал CNY при торгах в RUB группирует стоимость в CNY, но использует оценку и курс RUB — тест `PositionCurrencyGroupUsesFaceUnitButValuationUsesTradingCurrency` (Task 1).
- Пустые цена и/или курс должны оставить актив видимым и исключить его стоимость из суммы и знаменателя долей; ручные цена и курс должны разрешать этот случай — тесты `UnvaluedAssetReportsEachMissingInput` и `ManualPriceAndRateResolveUnvaluedAsset` (Task 1).
- Фонд ликвидности без котировки нельзя потерять из среза — тест `UnquotedFundCanBeManuallyValued` (Task 1).
- Пустой портфель, нулевая сумма и нулевой денежный остаток не должны давать `NaN`/бесконечную долю — тесты `EmptyPortfolioReturnsNoSlices` и `ZeroCashBalanceDoesNotRequireCurrencyRate` (Task 1).
- Повторяющиеся валюты/идентификаторы, пустые коды и отрицательные, нулевые либо не конечные ручные значения должны отклоняться в API — тесты валидатора в Task 3.
- Рублёвые алиасы `SUR`/`RUR` должны объединяться с RUB; совпадающие FaceUnit и валюта торгов позволяют рассчитать денежную цену без FX.
- После ручной оценки поля цены и курса должны оставаться редактируемыми; выбранная вкладка должна быть целиком видимой на экранах шириной 320 px.

---

### Task 1: Чистый расчёт состава по валютам

**Файлы:**
- Create: `src/Popo.Core/Portfolio/PortfolioCurrencyAnalyticsContracts.cs`
- Create: `src/Popo.Core/Portfolio/PortfolioCurrencyAnalyticsCalculator.cs`
- Test: `tests/Popo.Tests/PortfolioCurrencyAnalyticsCalculatorTests.cs`

**Интерфейсы:**
- `PortfolioCurrencyAnalyticsAsset` содержит стабильный `AssetId`, тип актива, название, торговую валюту, необязательную валюту номинала, количество, текущую цену в валюте торгов и доступный курс этой торговой валюты в RUB.
- `PortfolioCurrencyAnalyticsInput` содержит активы, ручные остатки денег и курсы этих валют, ручные цены по `AssetId` и ручные курсы по коду валюты.
- `PortfolioCurrencyAnalyticsCalculator.Calculate(PortfolioCurrencyAnalyticsInput)` возвращает оценённую сумму в RUB, доли по валютам и элементы, которые пока нельзя оценить.
- Для позиции код группы — `FaceUnit ?? CurrencyId`; для фонда и денег — валюта инструмента/остатка. Курс всегда относится к валюте торгов/денег, а не к валюте группы. Доли хранятся как значения от `0` до `1`; если оценённая сумма равна нулю, список долей пуст.

- [x] **Шаг 1: добавить падающие тесты калькулятора**

  Проверить `PositionCurrencyGroupUsesFaceUnitButValuationUsesTradingCurrency`, `PositionFallsBackToTradingCurrencyWhenFaceUnitMissing`, `CalculateIncludesPositionsCashAndFundsInTotalAndShares`, `CurrentMarketPriceTakesPrecedenceOverManualPrice`, `ManualPriceUsesQuantityAndTradingCurrency`, `ManualRateIsUsedOnlyWhenCurrentRateIsMissing`, `UnvaluedAssetReportsEachMissingInput`, `ManualPriceAndRateResolveUnvaluedAsset`, `UnquotedFundCanBeManuallyValued`, `EmptyPortfolioReturnsNoSlices`, `ZeroCashBalanceDoesNotRequireCurrencyRate` и регистронезависимую группировку валют.

- [x] **Шаг 2: запустить новый набор и убедиться, что он падает на отсутствующем калькуляторе/контракте**

  Запуск: `dotnet test tests/Popo.Tests/Popo.Tests.csproj --filter FullyQualifiedName~PortfolioCurrencyAnalyticsCalculatorTests`

- [x] **Шаг 3: реализовать контракты и `PortfolioCurrencyAnalyticsCalculator.Calculate`**

  Использовать доступную рыночную цену прежде ручной; ручную цену умножать на количество; затем применять актуальный курс, а при его отсутствии — ручной. Неоценённые элементы возвращать с отдельными признаками отсутствующей цены и/или курса. Для нулевого общего знаменателя не формировать доли.

- [x] **Шаг 4: повторно выполнить набор калькулятора и убедиться, что он проходит**

  Запуск тот же, что в шаге 2; ожидается `Passed` для всех перечисленных сценариев.

- [x] **Шаг 5: сохранить завершённый шаг отдельным коммитом**

### Task 2: Stateless API и источник текущих оценок

**Файлы:**
- Create: `src/Popo.Core/Portfolio/PortfolioCurrencyRateContracts.cs`
- Create: `src/Popo.Core/Portfolio/IPortfolioCurrencyRatesProvider.cs`
- Create: `src/Popo.Storage/Providers/Portfolio/PortfolioCurrencyRatesProvider.cs`
- Modify: `src/Popo.Storage/ServiceCollectionExtensions.cs`
- Create: `tests/Popo.Storage.Tests/PortfolioCurrencyRatesProviderTests.cs`

**Интерфейсы:**
- `IPortfolioCurrencyRatesProvider.GetLatestRatesAsync(IReadOnlyCollection<string> currencyIds, DateOnly asOf, CancellationToken)` возвращает последнюю сохранённую ставку каждой запрошенной валюты с `RateDate <= asOf`; неизвестные валюты отсутствуют в результате.
- `IPortfolioCurrencyRatesProvider.GetAvailableCurrencyIdsAsync(DateOnly asOf, CancellationToken)` возвращает коды валют с курсом не позже даты расчёта для выбора денежного остатка.

- [x] **Шаг 1: добавить падающие Storage-тесты provider-а**

  Проверить выбор наиболее позднего курса не позже даты расчёта, исключение будущего курса и других кодов валют, пустой ответ для неизвестной валюты, список доступных валют и стабильность тестов в отдельной временной схеме по ADR-0001.

- [x] **Шаг 2: запустить новые Storage-тесты и убедиться, что они падают на отсутствующем контракте/provider-е**

  Запуск: `dotnet test tests/Popo.Storage.Tests/Popo.Storage.Tests.csproj --filter FullyQualifiedName~PortfolioCurrencyRatesProviderTests`

- [x] **Шаг 3: реализовать контракт, read-only provider и DI-регистрацию**

  Provider читает только `CurrencyRates`, выбирает последнюю ставку на указанную дату и не изменяет строки.

- [x] **Шаг 4: повторно выполнить Storage-тесты provider-а**

  Запустить команду шага 2; ожидается `Passed` для всех сценариев.

- [x] **Шаг 5: сохранить завершённый шаг отдельным коммитом**

### Task 3: Stateless API и сбор текущих оценок

**Файлы:**
- Create: `src/Popo.Api/Models/PortfolioCurrencyAnalyticsModels.cs`
- Create: `src/Popo.Api/Services/PortfolioCurrencyAnalyticsService.cs`
- Create: `src/Popo.Api/Validators/PortfolioCurrencyAnalyticsRequestValidator.cs`
- Modify: `src/Popo.Api/Services/Position/PortfolioPositionsService.cs`
- Modify: `src/Popo.Api/Controllers/PortfolioController.cs`
- Modify: `src/Popo.Api/ApiServiceCollectionExtensions.cs`
- Create: `tests/Popo.Tests/PortfolioCurrencyAnalyticsServiceTests.cs`
- Modify: `tests/Popo.Tests/PortfolioPositionsServiceTests.cs`
- Modify: `tests/Popo.Tests/PortfolioReturnsApiContractTests.cs`
- Modify: `tests/Popo.Tests/ApiRequestValidatorsTests.cs`
- Modify: `tests/Popo.Tests/ApiDependencyRegistrationTests.cs`
- Modify: `tests/Popo.Tests/PageApiContractTests.cs`

**Интерфейсы:**
- `IPortfolioPositionsService.GetCurrencyAnalyticsAssetsAsync(CancellationToken)` возвращает для каждой текущей позиции цену в валюте торгов, количество, `FaceUnit` и уже выбранный сервисом курс торговой валюты к RUB; расчёт существующей страницы позиций не меняется.
- `POST /api/portfolio/analytics/currency` принимает массив остатков `(CurrencyId, Amount)`, ручных цен `(AssetId, Price)` и ручных курсов `(CurrencyId, RateToRub)` и возвращает `PortfolioCurrencyAnalyticsResponse`.
- `PortfolioCurrencyAnalyticsResponse.AvailableCurrencies` объединяет RUB, валюты торгов и номинала из `IBondsService` и коды с актуальным курсом из `IPortfolioCurrencyRatesProvider`.
- Стабильный `AssetId` позиции строится из `SecId + BoardId + CurrencyId`, фонда — из `SecId + BoardId`; идентификатор вместе с типом актива возвращается в списке неоценённых элементов.
- `PortfolioCurrencyAnalyticsService.CalculateAsync` получает текущие позиции и фонды через `IPortfolioPositionsService`, варианты валют через `IBondsService` и `IPortfolioCurrencyRatesProvider`, актуальные курсы денег через `IPortfolioCurrencyRatesProvider`, затем вызывает Core-калькулятор. LQDT/TMON передаются в калькулятор с валютой `RUB`.
- Запрос валидирует конечные неотрицательные денежные остатки, конечные положительные цены/курсы, непустые коды валют, непустые идентификаторы активов и отсутствие повторяющихся валют/идентификаторов без учёта регистра. Параметры запроса не проходят через provider записи и не сохраняются.

- [x] **Шаг 1: написать падающие API/service тесты**

  Добавить `PortfolioPositionsServiceTests.CurrencyAnalyticsSourceReturnsPositionWithSeparateNominalAndTradingCurrencyFields`; в `PortfolioCurrencyAnalyticsServiceTests` проверить `CalculateAsyncCombinesPositionsCashAndLiquidity`, `CalculateAsyncUsesManualRateWhenLatestRateIsMissing`, `CalculateAsyncReturnsPartialItemsForMissingMarketData`, `CalculateAsyncDoesNotRetainManualInputsBetweenRequests`, `CalculateAsyncIncludesFaceAndTradingCurrenciesInCashOptions`; в `ApiRequestValidatorsTests` проверить `PortfolioCurrencyAnalyticsRequestValidatorRejectsNegativeCashAndNonPositiveManualPriceOrRate` и `PortfolioCurrencyAnalyticsRequestValidatorRejectsNonFiniteAndBlankOrDuplicateIdentifiers`; в contract tests проверить `PortfolioController_ExposesCurrencyAnalyticsEndpoint`, форму ответа и регистрацию validator/service.

- [x] **Шаг 2: запустить затронутые тесты и убедиться, что новые сценарии падают**

  Запуски: `dotnet test tests/Popo.Tests/Popo.Tests.csproj --filter FullyQualifiedName~PortfolioCurrencyAnalytics` и `dotnet test tests/Popo.Tests/Popo.Tests.csproj --filter FullyQualifiedName~ApiRequestValidatorsTests`.

- [x] **Шаг 3: добавить endpoint, сбор источников и валидацию запроса**

  Контроллер только связывает HTTP-запрос со службой и возвращает успешный ответ. Для валюты RUB/SUR использовать курс `1`; для других валют выбирать последнюю сохранённую ставку не позже даты расчёта. Не менять `GetPageAsync`, текущую сводку или хранение данных.

- [x] **Шаг 4: повторно выполнить API/service тесты**

  Запустить команды шага 2; ожидаются `Passed` для новых сценариев, валидатора, DI и маршрута.

- [x] **Шаг 5: сохранить завершённый шаг отдельным коммитом**

### Task 4: Вкладка «Аналитика» для веба и телефона

**Файлы:**
- Create: `src/Popo.Frontend/src/lib/portfolio-currency-analytics-api.ts`
- Create: `src/Popo.Frontend/src/lib/portfolio-currency-analytics-api.test.ts`
- Create: `src/Popo.Frontend/src/lib/portfolio-currency-analytics-presentation.ts`
- Create: `src/Popo.Frontend/src/lib/portfolio-currency-analytics-presentation.test.ts`
- Create: `src/Popo.Frontend/src/app/portfolio/currency-analytics-tab.tsx`
- Modify: `src/Popo.Frontend/src/app/portfolio/page.tsx`
- Modify: `src/Popo.Frontend/src/app/globals.css`
- Modify: `src/Popo.Frontend/README.md`

**Интерфейсы:**
- `portfolioCurrencyAnalyticsApi.calculate(payload)` вызывает только `POST /api/portfolio/analytics/currency` и передаёт текущие значения из страницы.
- `buildPortfolioCurrencyAnalyticsPresentation(response)` подготавливает безопасные данные круговой диаграммы и списка, не создавая долей для нулевого знаменателя.
- `PortfolioCurrencyAnalyticsTab` содержит ввод денежных остатков, ручной цены одной бумаги и курса валюты, кнопку пересчёта, общую оценённую сумму, donut-диаграмму, доли и список неоценённых активов с причинами.
- В `page.tsx` добавить третью вкладку «Аналитика» рядом с «Стоимость портфеля» и «Индексы». Состояние ручного ввода живёт только внутри компонента страницы; переход между вкладками его сохраняет, уход со страницы/перезагрузка сбрасывает.

- [x] **Шаг 1: добавить падающие тесты API-клиента и presentation-функции**

  Проверить `CalculatePostsTransientPayloadToCurrencyAnalyticsEndpoint`, `CalculateSurfacesNetworkAndHttpErrors`, `BuildPresentationMapsCurrencySlicesAndIncompleteItems`, `BuildPresentationReturnsNoNaNForEmptyAndZeroValues`.

- [x] **Шаг 2: запустить новые frontend тесты и увидеть ожидаемое падение**

  Запуск: `npm test` из `src/Popo.Frontend` после добавления тестов; новые тесты должны падать до реализации.

- [x] **Шаг 3: реализовать API-клиент и presentation-функции; затем компонент и вкладку**

  Перед кодом прочитать актуальные руководства Next.js из `src/Popo.Frontend/node_modules/next/dist/docs/`. Использовать уже установленный Recharts без новых зависимостей; для адаптивности добавить CSS grid/flex и breakpoint-ы, ручные вводы держать только в React state.

- [x] **Шаг 4: выполнить frontend тесты и проверить вкладку в браузере**

  `npm test` (54 теста) и production build прошли. В опубликованном Docker-приложении проверены ширины 1440, 390 и 320 px, отсутствие горизонтального overflow страницы и полная видимость активной вкладки. Запрос analytics возвращает реальные доли; POST с временным RUB-остатком увеличивает итог на введённую сумму, следующий запрос без остатка возвращает исходный итог. В текущем портфеле нет неоценённых активов, поэтому ручные поля цены/курса не появляются для live UI-проверки; их доступность после оценки или очистки покрыта frontend helper tests, применение ручных значений — backend calculator/service tests.

- [x] **Шаг 5: сохранить завершённый шаг отдельным коммитом**

### Task 5: Документация, полная проверка и независимое ревью

**Файлы:**
- Modify: `docs/modules/api.md`
- Modify: `docs/modules/core.md`
- Modify: `docs/modules/storage.md`
- Modify: `docs/modules/tests.md`
- Modify: `src/Popo.Frontend/README.md`

**Интерфейсы:**
- Документация описывает endpoint, состав запроса/ответа, смысл валют номинала и торгов, ручную оценку и отсутствие сохранения вводов.
- Независимый reviewer получает весь diff и результаты тестов, проверяет соответствие спецификации, расчёты, ошибки, изоляцию ручных данных и покрытие тестами; замечания исправляются и перепроверяются.

- [x] **Шаг 1: обновить документацию модулей без изменения посторонних контрактов**

- [x] **Шаг 2: выполнить полный backend test suite**

  Запуск: `dotnet test Popo.sln`; ожидаются `Passed` без пропущенных тестов, кроме явно отмеченных тестов, требующих недоступного Docker.

- [x] **Шаг 3: выполнить полный frontend test suite и production build**

  Запуски из `src/Popo.Frontend`: `npm test`, `npm run build`; ожидается успешный exit code обоих процессов.

- [x] **Шаг 4: запросить независимое ревью всей фичи**

  Первое независимое ревью выявило сценарии смешения SUR/RUB, исчезновения применённых ручных полей и требования вводить уже известную цену при отсутствующем FX. Финальный review выявил обработку пустой котировки фонда, конкурентное изменение вводов во время расчёта, дубликаты валютных алиасов и пробелы в валюте денежного остатка. Все замечания исправлены и покрыты тестами; финальный прогон: 915 backend-тестов, 42 Storage-теста, 55 frontend-тестов и production-сборка Next.js через Docker прошли.

- [x] **Шаг 5: сохранить документацию и исправления ревью в текущей ветке `anal`**
