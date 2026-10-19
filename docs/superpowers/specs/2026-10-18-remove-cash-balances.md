# Remove cash balance tracking

## Goal

Remove manual cash snapshots and calculated cash balances from Popo while keeping money-market funds and their operations.

## Scope

- Remove the cash tab and snapshot CRUD from the liquidity UI and API; retain the funds screen under `/liquidity`.
- Remove `CashSnapshots`, its EF entity/provider/contracts, and cash-balance calculation. Add an EF migration that drops the table.
- Remove cash balances from the portfolio response and UI. `TotalValueRub` becomes positions value plus money-market funds value.
- Keep `MoneyMarketFunds` and `MoneyMarketFundOperations`; fund operations continue to recalculate quantity and average cost.
- Keep portfolio external cash flows because TWR and benchmark calculations consume them independently of cash balances.
- Keep cash investment recommendations because they calculate bond candidates from strategy settings and market data without reading cash balances.

## Data behavior

Applying the new migration deletes rows in `CashSnapshots`. The money-market fund tables, portfolio trades, valuations, and external cash flows remain intact.

## Verification

- API contract tests assert that only fund routes remain under `/api/liquidity`.
- Schema tests assert that `CashSnapshots` is absent while both fund tables remain.
- Portfolio overview tests assert that the total contains positions and funds only.
- Run the .NET test suite and frontend production build.
