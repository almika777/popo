# Remove cash balance tracking Implementation Plan

> Исторический план сохранения журнала операций фондов superseded [планом ручного учёта текущих позиций](2026-10-19-manual-money-market-fund-positions.md).

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove cash snapshot and balance tracking while retaining money-market funds and their operations.

**Architecture:** Remove the cash ledger path through API, Core, Storage, and the portfolio summary. Keep fund operations, portfolio external cash flows, and cash investment recommendations; move the remaining liquidity UI and API paths from `/cash` to `/liquidity`.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10, PostgreSQL, Next.js, TypeScript, NUnit.

**Spec:** `docs/superpowers/specs/2026-10-18-remove-cash-balances.md`

## Global Constraints

- The original plan kept `MoneyMarketFunds` and `MoneyMarketFundOperations`; its fund scope is superseded by the 2026-10-19 manual-position plan above.
- Keep `PortfolioCashFlows` behavior used by TWR and benchmark calculations.
- Keep cash recommendation behavior; it does not depend on cash balances.
- Change the EF Core schema only through a migration, per ADR-0002.
- Do not alter the pre-existing uncommitted review-map changes unless required to keep their routes accurate.

## Review Focus

- Old cash snapshot routes must disappear while liquidity fund routes remain.
- The migration must drop only `CashSnapshots`; fund and portfolio return tables remain.
- Fund buys and sells must still affect holdings quantity and average cost after cash delta code is removed.
- Portfolio total must be exactly positions plus current money-market fund value.
- External cash flows and cash-recommendation routes must remain operational.

---

### Task 1: Remove cash ledger backend and storage

**Files:**
- Modify: `tests/Popo.Tests/PageApiContractTests.cs`
- Modify: `tests/Popo.Tests/PopoSchemaTests.cs`
- Modify: `tests/Popo.Tests/PortfolioPositionsServiceTests.cs`
- Modify: `tests/Popo.Tests/PortfolioLedgerTests.cs`
- Modify: `tests/Popo.Tests/ApiDependencyRegistrationTests.cs`
- Modify: `tests/Popo.Tests/ApiRequestValidatorsTests.cs`
- Modify: `tests/Popo.Tests/PortfolioReturnsApiContractTests.cs`
- Modify: `tests/Popo.Tests/PortfolioValuationHttpIntegrationTests.cs`
- Delete: `tests/Popo.Tests/PortfolioServiceTests.cs`
- Delete: `tests/Popo.Storage.Tests/PortfolioCashProviderTests.cs`
- Modify: `src/Popo.Api/Controllers/CashController.cs` (retain funds routes only; rename to `LiquidityController`)
- Modify: `src/Popo.Api/Services/CashManagementService.cs` (retain fund operations; rename to `MoneyMarketFundManagementService`)
- Delete: `src/Popo.Api/Services/CashPageService.cs`
- Delete: `src/Popo.Api/Services/PortfolioService.cs`
- Modify: `src/Popo.Api/Services/Position/PortfolioPositionsService.cs`
- Modify: `src/Popo.Api/Services/PortfolioPageService.cs`
- Modify: `src/Popo.Api/Models/PortfolioLedgerModels.cs`
- Modify: `src/Popo.Api/Models/PortfolioReturnModels.cs`
- Modify: `src/Popo.Api/ApiServiceCollectionExtensions.cs`
- Modify: `src/Popo.Api/Validators/CashRequestValidators.cs`
- Delete: `src/Popo.Core/Portfolio/PortfolioCashCalculator.cs`
- Delete: `src/Popo.Core/Portfolio/PortfolioCashContracts.cs`
- Modify: `src/Popo.Core/Portfolio/PortfolioLedgerContracts.cs`
- Modify: `src/Popo.Core/Portfolio/MoneyMarketFundOperationCalculator.cs`
- Modify: `src/Popo.Storage/PopoDbContext.cs`
- Modify: `src/Popo.Storage/ServiceCollectionExtensions.cs`
- Delete: `src/Popo.Storage/Entities/CashSnapshotEntity.cs`
- Delete: `src/Popo.Storage/Providers/Portfolio/PortfolioCashProvider.cs`
- Modify: `src/Popo.Storage/Migrations/PopoDbContextModelSnapshot.cs`
- Create: `src/Popo.Storage/Migrations/<timestamp>_RemoveCashSnapshots.cs` and its designer.

**Interfaces:**
- Produces: `LiquidityController` serves only the money-market fund page, fund CRUD, and fund operations; `PortfolioOverview` and `PortfolioPageResponse` contain no cash balances; portfolio summary fields are `AsOf`, `TotalValueRub`, `PositionsValueRub`, `MoneyMarketFundsValueRub`, and `UnrealizedPnlRub`.

- [ ] Add a route-contract assertion in `PageApiContractTests` that only fund endpoints remain under `api/liquidity`; run it and confirm it fails on the current snapshot routes.
- [ ] Update the schema contract to omit `CashSnapshots` and retain `MoneyMarketFunds` and `MoneyMarketFundOperations`; run it and confirm the old model fails.
- [ ] Add an overview test where there are no positions or funds but the existing cash service returns a RUB balance; assert the summary total is zero and confirm the old service fails.
- [ ] Remove cash snapshot routes, services, validators, Core contracts/calculator, storage provider/entity/DbSet, cash fields from response contracts, and DI registrations. Keep fund routes and operations.
- [ ] Change `GetOverviewAsync` total to `positionsValueRub + fundsValueRub`; remove cash balance loading and the `CashValueRub` field.
- [ ] Add the EF migration that drops only `CashSnapshots`, update the model snapshot, and remove obsolete cash-ledger tests while retaining fund and cash-flow tests.
- [ ] Run: `dotnet test tests/Popo.Tests/Popo.Tests.csproj --no-restore --logger "console;verbosity=minimal"`.

### Task 2: Keep only funds in the liquidity UI and update documentation

**Files:**
- Move: `src/Popo.Frontend/src/app/cash/page.tsx` to `src/Popo.Frontend/src/app/liquidity/page.tsx`
- Modify: `src/Popo.Frontend/src/app/app-navigation.tsx`
- Modify: `src/Popo.Frontend/src/app/portfolio/page.tsx`
- Modify: `src/Popo.Frontend/src/lib/portfolio-ledger-api.ts`
- Modify: `src/Popo.Frontend/src/lib/portfolio-return-api.ts`
- Modify: `src/Popo.Frontend/src/app/globals.css`
- Modify: `tests/Popo.Tests/PortfolioValuationHttpIntegrationTests.cs`
- Modify: `docs/project.md`, `docs/conventions.md`, `docs/modules/api.md`, `docs/modules/core.md`, `docs/modules/storage.md`, `docs/modules/tests.md`

**Interfaces:**
- Consumes: the funds-only `/api/liquidity/money-market-funds` routes from Task 1.
- Produces: `/liquidity` shows funds and fund operations; the portfolio page displays no cash metric and its total breakdown matches the positions-plus-funds total.

- [ ] Update frontend route and API contract references to `/liquidity`; remove cash snapshot forms, balances, tab state, and refresh calls.
- [ ] Remove the portfolio cash card and `balances` response state; keep external cash-flow controls and return calculations.
- [ ] Correct docs to describe the funds-only liquidity page and the removed cash snapshot table/calculation.
- [ ] Run: `npm run build` from `src/Popo.Frontend`.
- [ ] Run: `dotnet test Popo.sln --no-restore --logger "console;verbosity=minimal"`.
