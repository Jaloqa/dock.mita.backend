# Overview and Reports Implementation Plan

**Goal:** Real `GET /v1/overview` and `GET /v1/reports` on the backend, exactly as the frontend contract and mocks define them, so that after this plan the frontend serves only the demo endpoints (`/v1/demo/*`) from mocks.

**Architecture:**
- **Read-only slices:** `Features/Overview/GetOverview` and `Features/Reports/GetReport`. No new tables, no migration; both read transactions (`BaseAmount`, always in the current base currency), categories, accounts, budgets and goals.
- **Shared parts:** pure share math in `Common/Reporting/Shares.cs`; the transaction filter both slices count with (user, date range, not the `transfer` category) and the spending-by-category query in `Common/Reporting/ReportingQueries.cs`; `FinancialPeriod.Last` in the domain for the report periods. The overview reuses `BudgetQueries.BudgetsInAsync`, `BudgetView.Summarize`, `GoalQueries.GoalsAsync`, `AccountQueries.ProjectAccounts` and `RateTable`.
- **Consistency:** the overview runs its reads inside one `REPEATABLE READ` transaction, so balance, totals, budgets and goals come from one snapshot.

**Tech Stack:** .NET 10, EF Core (Npgsql) LINQ, MediatR 12.5.0, FluentValidation, xUnit v3, Shouldly, Testcontainers. Frontend task: Nuxt, Playwright. No new packages.

**Spec:** This plan argues from these documents:
- The backend project guide in the repository root, `docs/STACK.md`, `docs/api-contract.md` and the merged plans in `docs/superpowers/plans/` (the "What later plans need" sections of `2026-10-19-categories-and-transactions.md` and `2026-10-19-budgets-and-goals.md`).
- The frontend contract in the frontend repo:
  - `docs/superpowers/specs/2026-10-15-frontend-mocks-design.md` (aggregates come from the backend: `GET /v1/overview?period=`, `GET /v1/reports?range=3m|6m|12m`; transfers are neither income nor expense and are excluded from reports; overview, budgets and reports work on financial periods that follow `monthStartDay`);
  - `app/shared/api/contract.ts` (`OverviewRange`, `OverviewTotal`, `SpendingSlice`, `OverviewMonth`, `OverviewBudgetDto`, `OverviewTransactionDto`, `OverviewGoalDto`, `OverviewDto`, `ReportRange`, `reportRangePeriods`, `ReportPeriodDto`, `ReportStructureItemDto`, `ReportStructureDto`, `ReportDto`);
  - `mocks/handlers/overview.ts`, `mocks/overview.ts`, `mocks/handlers/reports.ts`, `mocks/reports.ts`, `mocks/budgets.ts` (`spentByCategory`, `budgetsOf`, `summarize`), `mocks/goals.ts` (`amountIn`, `goalsOf`), `mocks/ledger.ts` (`accountBalances`), `mocks/transactions.ts` (`baseAmountOf`, `transactionsIn`);
  - `tests/unit/mocks/overview.test.ts`, `tests/unit/mocks/reports.test.ts` (the expected values below come from these);
  - `app/features/overview/api/overviewApi.ts` (`GET /v1/overview?period=<range>`), `app/features/reports/api/reportsApi.ts` (`GET /v1/reports?range=<range>`). Reports have no CSV export (PDF through print only).
- The ledgers of the previous backend plans (`dock.mita-work/categories-transactions-ledger.md`, `dock.mita-work/budgets-goals-ledger.md`).

## Endpoint inventory

Every path the frontend calls (`app/**/api/*.ts`) against what the backend serves after this plan:

| Frontend call | Backend |
|---|---|
| `/v1/auth/{register,login,refresh,logout}`, `GET/PUT/DELETE /v1/me`, `GET/PUT /v1/settings`, `GET /v1/rates` | served |
| `/v1/accounts`, `/v1/transfers`, `/v1/categories`, `/v1/transactions` (list, get, create, update, delete) | served |
| `/v1/budgets` (list, create, update, delete, copy), `/v1/goals` (list, create, update, delete, contributions) | served |
| `GET /v1/overview?period=` | this plan |
| `GET /v1/reports?range=` | this plan |
| `GET /v1/demo/state`, `POST /v1/demo/reset`, `POST /v1/demo/switch-user`, `PUT /v1/demo/simulate-errors` | demo only, stay mock-only |

## Decisions

| Topic | Decision |
|---|---|
| Scope | `GET /v1/overview` and `GET /v1/reports`, their docs and OpenAPI, the Docker smoke, the frontend hybrid default. Switching the frontend off mocks entirely is a later plan |
| Today | `UtcCalendar.Today(TimeProvider)`, as budgets and goals |
| Counted transactions | `ReportingQueries.CountedTransactions`: the user's transactions with `From ≤ Date ≤ To` whose category is not `SystemCategory.Transfer` (a `Join` on `db.Categories`; the backend already forbids transactions in `transfer`, the filter keeps the mock rule explicit). Amounts are `BaseAmount` |
| Overview ranges | `week`: today − 6 … today, previous today − 13 … today − 7. `month`: the period of today (`FinancialPeriod.For(today, monthStart)`), previous `Shift(key, −1)`. `year`: `Shift(key, −11).From` … current `To`, previous `Shift(key, −23).From` … `Shift(key, −12).To`. Default `month` |
| Totals | `income` and `expense` = `{ amount: Σ BaseAmount, count }` per kind over the counted transactions of the range; `change` = percent change against the previous range, null when the previous amount is 0. `transactionCount` = income count + expense count. `averageExpense` = `round(expense.amount / expense.count)` (half away from zero), null when the count is 0 |
| Percent change | The mock computes `Math.round(((current − previous) × 1000) / previous) / 10`, and JS `Math.round` rounds halves up (towards +∞). Exact integer form: `tenths = floor((2000 × (current − previous) + previous) / (2 × previous))` with floor division; `change = tenths / 10.0`, never `-0` |
| Shares | `Shares.Tenths(amounts)`: largest remainder over 1000 tenths; all zeros when the total is ≤ 0; ties of the remainder go to the lower index; exact integer arithmetic (`Int128`), where the mock uses doubles (same results for every realistic input) |
| Spending slices | Expense spent per category in the overview range (`ReportingQueries.SpendingAsync`), amounts > 0, `transfer` excluded. `other` (either kind's system `other`; only the expense one can appear) goes to the bucket. The rest is ordered by amount desc, then name (invariant culture comparison, the closest to JS `localeCompare`), the top 5 become slices, the others join the bucket. The bucket (`categoryId: null, name: null`) is appended when > 0. `percent = Tenths(amounts of slices and bucket) / 10.0` |
| Balance | Every account of the user except archived accounts with balance 0; `amount` = Σ of each balance (`AccountQueries.ProjectAccounts`) converted into the base currency at today's rate (`PlnValuesAsync(today)`, `RateTable.Rate`, `MinorUnits.Convert` per account, then summed); `accounts` = their count. A missing rate answers 503 `rates.unavailable` |
| Month and top budgets | Always the period of today, whatever the range: `BudgetsInAsync(period, base, today)` (null → 503); `month = { period, from, to, planned, spent, left, budgets: count }` from `BudgetView.Summarize`. `topBudgets`: ordered by usage `spent / limit` desc (exact comparison, `limit` is always ≥ 1), then `spent` desc, then the budget order; first 5; `name` = the category name |
| Recent | The counted transactions of the range ordered by `Date` desc, `CreatedAt` desc, `Id` desc (the transactions `-date` order); first 6; `amount = BaseAmount`; `categoryName`, `accountName` from the user's categories and accounts; `kind` through `CategoryWire.ToWire` |
| Goals preview | `GoalsAsync(base, today)` (null → 503): goals not reached first, then reached, each in the goal order; first 3 as `{ id, name, target, saved, percent, status, deadline }`; `goalCount` = all goals |
| hasTransactions | Any transaction of the user, any date |
| Report periods | `FinancialPeriod.Last(today, monthStart, count)`: the `count` periods ending with the period of today, oldest first; `count` = 3, 6, 12 for `3m`, `6m`, `12m`, default `6m`. `from` = first `From`, `to` = current `To` |
| Report figures | One grouped query over the counted transactions from `from` to `to` (`GroupBy(Date, Kind)` sums), bucketed into the periods in memory. `totals` = sums; `averageIncome`/`averageExpense` = `round(total / count)` (half away from zero); `savingsRate` = `round_half_away((income − expense) × 1000 / income) / 10.0`, null when income ≤ 0, never `-0` |
| Report structure | Expense spending per category of the current period (`SpendingAsync`), amounts > 0, `transfer` excluded, `other` included as a normal item; ordered by amount desc, then name (invariant culture); every category (no top-N); `{ categoryId, name, color, amount, percent }` with `Tenths` / 10.0; `total` = Σ amounts |
| Rates in reports | Reports read only `BaseAmount`, so they never need a rate and never answer 503 |
| Validation | `period` must be exactly `week`, `month` or `year` when present (case-sensitive; `?period=` is present and invalid), else 400 `period: ["validation.invalidValue"]`; `range` exactly `3m`, `6m`, `12m`, else 400 `range: ["validation.invalidValue"]` |
| Status codes | `GET /v1/overview` 200, 400, 401, 503; `GET /v1/reports` 200, 400, 401. Tags `Overview` and `Reports`, `RequireAuthorization("User")`, v1 group |
| Wire numbers | `percent`, `change` and `savingsRate` are `double` built from integer tenths (`tenths / 10.0`), so they serialize as `42.5`, `100`, `-10`, never `-0` |
| Pagination | Not paginated. The overview lists are capped by the contract (5 slices + bucket, 5 budgets, 6 transactions, 3 goals), report periods by 12, and report structure items by the user's expense categories; the contract has no paging for them and the chart needs every item |
| Caching | Not cached, as budgets and goals: the figures depend on today, rates, and every write to accounts, transfers, categories, transactions, budgets, goals and settings; tagging all of those commands costs more than the reads |
| Dapper | Not added. Every query is a grouped LINQ query EF translates; `Directory.Packages.props` has no Dapper today, and adding it is not needed |
| Snapshot | The overview handler opens `db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct)` around its reads and disposes it without committing |
| Hybrid frontend | `/v1/overview` and `/v1/reports` become real in the hybrid default (twelve prefixes). Only `/v1/demo/*` stays mocked in hybrid mode; the frontend project guide says so |

## Global Constraints

- All foundation, auth, accounts, categories and budgets constraints apply: warnings are errors; central packages; `Api → Infrastructure → Domain`; records, `sealed`, no comments, English; `CancellationToken` everywhere; every endpoint in the v1 group with `RequireAuthorization("User")`; ProblemDetails for every error; a slice never references another slice (shared code in `Common/`); queries use `AsNoTracking` and project; final newline in every file.
- Contract with the frontend, byte for byte (camelCase JSON):
  - `OverviewDto { range, from, to, previousFrom, previousTo, currency, balance: { amount, accounts }, income: OverviewTotal, expense: OverviewTotal, averageExpense: number|null, transactionCount, spending: SpendingSlice[], month: OverviewMonth, topBudgets: OverviewBudgetDto[], recent: OverviewTransactionDto[], goals: OverviewGoalDto[], goalCount, hasTransactions }`;
  - `OverviewTotal { amount, count, change: number|null }`; `SpendingSlice { categoryId: string|null, name: string|null, amount, percent }`; `OverviewMonth { period, from, to, planned, spent, left, budgets }`;
  - `OverviewBudgetDto { id, categoryId, name, limit, spent, remaining, status }`; `OverviewTransactionDto { id, kind, date, title, categoryName, accountName, amount }`; `OverviewGoalDto { id, name, target, saved, percent, status, deadline }`;
  - `ReportDto { range, from, to, currency, periods: ReportPeriodDto[], totals: { income, expense }, averageIncome, averageExpense, savingsRate: number|null, structure: ReportStructureDto, hasTransactions }`;
  - `ReportPeriodDto { period, from, to, income, expense }`; `ReportStructureDto { period, from, to, total, items }`; `ReportStructureItemDto { categoryId, name, color, amount, percent }`.
- Endpoints: `GET /v1/overview?period=week|month|year`, `GET /v1/reports?range=3m|6m|12m`.
- Dates are `YYYY-MM-DD` (`IsoDate.ToWire`), periods `YYYY-MM`, currencies through `CurrencyWire`.
- Every figure is scoped to the current user.
- Integration tests: registration seeds the starter categories, and category names are unique per user and kind, so test categories use names outside the starter set (as `ListBudgetsTests` does: "Weekly food", "Coffee shops"); the names in the test rows below are illustrative and may be replaced by any non-starter names that keep the same order. Rates for foreign currencies come only from `TestData.AddRatesAsync` on `factory.WithIsolatedData()`; tests never call the real NBP API.
- Commit messages: Conventional Commits; no tool attribution and no `Co-Authored-By` trailers.

## Review Focus

- Changing the base currency must re-price every overview and report figure (totals, spending, recent amounts, balance, budgets, goals, periods, structure) into the new currency, and a balance in a currency with no stored rate must answer 503 instead of 500. Pinned in Task 2 (`Should_RepriceReport_When_BaseCurrencyChanges`) and Task 3 (`Should_RepriceOverview_When_BaseCurrencyChanges`, `Should_Return503_When_BalanceNeedsMissingRate`).
- A `monthStartDay` other than 1 (and `lastWorkingDay`) must move every range, period and the "current month" block, and a transaction on the day before the period start must land in the previous period. Pinned in Task 2 (`Should_FollowMonthStart`) and Task 3 (`Should_FollowMonthStart_ForMonthAndYear`).
- Halves and negative changes must round like the mock (`Math.round` for changes and averages, half away from zero for the savings rate), shares must always add up to 100.0, and no figure may serialize as `-0`. Pinned in Task 1 (rows below) and Task 3 (`Should_SplitSpending_IntoTopFiveAndBucket`).
- Archived accounts with money, archived accounts at zero and accounts in foreign currencies must each count the way the mock counts them. Pinned in Task 3 (`Should_TotalBalances_OfAccountsHoldingMoney`).
- Another user's data must never leak into the figures, and an empty user must get a complete zero document. Pinned in Task 2 (`Should_ReturnEmptyReport_And_IsolateUsers`) and Task 3 (`Should_ReturnEmptyOverview_And_IsolateUsers`).

---

## File map

```
src/Mita.Domain/Periods/FinancialPeriod.cs (Last)
src/Mita.Api/Common/Reporting/Shares.cs, ReportingQueries.cs, CategorySpending.cs
src/Mita.Api/Features/Reports/GetReport/{GetReportEndpoint,GetReportQuery,GetReportValidator,GetReportHandler,ReportMath,ReportResponse,ReportPeriodResponse,ReportTotalsResponse,ReportStructureResponse,ReportStructureItemResponse}.cs, Features/Reports/ReportsEndpoints.cs
src/Mita.Api/Features/Overview/GetOverview/{GetOverviewEndpoint,GetOverviewQuery,GetOverviewValidator,GetOverviewHandler,OverviewMath,OverviewRanges,OverviewResponse,OverviewBalanceResponse,OverviewTotalResponse,SpendingSliceResponse,OverviewMonthResponse,OverviewBudgetResponse,OverviewTransactionResponse,OverviewGoalResponse}.cs, Features/Overview/OverviewEndpoints.cs
src/Mita.Api/Program.cs
tests/Mita.Api.UnitTests/Periods/FinancialPeriodTests.cs (extended), Features/Reporting/SharesTests.cs, Features/Overview/{OverviewMathTests,OverviewRangesTests,GetOverviewValidatorTests}.cs, Features/Reports/{ReportMathTests,GetReportValidatorTests}.cs
tests/Mita.Api.IntegrationTests/Features/Reports/ReportsTests.cs, Features/Overview/OverviewTests.cs, Infrastructure/TestData.cs, OpenApiTests.cs
docs/api-contract.md
```

---

### Task 1: Reporting math

**Files:** `src/Mita.Domain/Periods/FinancialPeriod.cs`, `src/Mita.Api/Common/Reporting/Shares.cs`, `src/Mita.Api/Features/Overview/GetOverview/OverviewMath.cs`, `src/Mita.Api/Features/Overview/GetOverview/OverviewRanges.cs`, `src/Mita.Api/Features/Reports/GetReport/ReportMath.cs`; tests `tests/Mita.Api.UnitTests/Periods/FinancialPeriodTests.cs`, `Features/Reporting/SharesTests.cs`, `Features/Overview/OverviewMathTests.cs`, `Features/Overview/OverviewRangesTests.cs`, `Features/Reports/ReportMathTests.cs`.

**Interfaces:**
- Consumes: `FinancialPeriod`, `PeriodKey`, `MonthStart`.
- Produces:
  - `FinancialPeriod.Last(DateOnly today, MonthStart start, int count) : IReadOnlyList<FinancialPeriod>` (oldest first, ending with `For(today, start)`).
  - `Shares.Tenths(IReadOnlyList<long> amounts) : int[]` and `Shares.Percent(int tenths) : double` (`tenths / 10.0`).
  - `OverviewMath.PercentChange(long current, long previous) : double?`, `OverviewMath.RoundedAverage(long total, int count) : long` (half away from zero; callers handle count 0).
  - `sealed record DateRange(DateOnly From, DateOnly To)`, `sealed record RangePair(DateRange Current, DateRange Previous)` (in `OverviewRanges.cs`), `OverviewRanges.TryParse(string? value, out OverviewRange range)`, `OverviewRanges.ToWire(OverviewRange range)`, `enum OverviewRange { Week, Month, Year }`, `OverviewRanges.Of(OverviewRange range, DateOnly today, MonthStart start) : RangePair`.
  - `ReportMath.SavingsRate(long income, long expense) : double?`, `ReportMath.TryParseRange(string? value, out int periods)` (`3m` → 3, `6m` → 6, `12m` → 12), `ReportMath.DefaultRange = "6m"`.
- [ ] **Step 1: Failing tests**
  - `Last(2026-10-16, Day(1), 3)` = `2026-08` 08-01..08-31, `2026-09` 09-01..09-30, `2026-10` 10-01..10-31; `Last(2026-10-16, Day(10), 3)` = 08-10..09-09, 09-10..10-09, 10-10..11-09; `Last(2026-10-05, Day(10), 3)` keys `2026-07`, `2026-08`, `2026-09`; `Last(2026-02-10, Day(1), 12)` keys `2025-03` … `2026-02`.
  - `Tenths`: [210 000, 124 000, 42 000, 41 200, 31 000, 45 370] → [425, 251, 85, 84, 63, 92]; [1, 1, 1] → [334, 333, 333]; [5] → [1000]; [0, 0] → [0, 0]; [] → []; `Percent(425)` = 42.5, `Percent(1000)` = 100.
  - `PercentChange`: (5 240, 5 000) 4.8; (4 500, 5 000) −10; (5 000, 5 000) 0; (4 999, 5 000) 0 and not negative zero (`double.IsNegative` false); (1, 400) −99.7 (a negative half rounds up); (100, 0) null; (0, 0) null.
  - `RoundedAverage`: (130 001, 6) 21 667; (1 000, 3) 333; (7 000, 12) 583; (80 000, 2) 40 000; (5, 2) 3.
  - `Of`: (week, 2026-10-16, 1) current 10-10..10-16, previous 10-03..10-09; (month, 2026-10-16, 1) 10-01..10-31 / 09-01..09-30; (month, 2026-10-16, 10) 10-10..11-09 / 09-10..10-09; (year, 2026-10-16, 1) 2025-11-01..2026-10-31 / 2024-11-01..2025-10-31; (year, 2026-10-16, 10) 2025-11-10..2026-11-09 / 2024-11-10..2025-11-09. `TryParse`: `week`, `month`, `year` parse; `day`, `2026-10`, `Week`, `""`, null do not.
  - `SavingsRate`: (300 000, 200 000) 33.3; (1 000, 0) 100; (100 000, 150 000) −50; (2 000, 1 999) 0.1; (100 000, 100 040) 0 and not negative zero; (2 000, 2 001) −0.1; (2 000, 2 000) 0; (100 000, 100 004) 0; (0, 5 000) null; (0, 0) null. `TryParseRange`: `3m`, `6m`, `12m` parse; `1m`, `6M`, `year`, `6`, `""`, null do not.
- [ ] **Step 2: Run, see them fail.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/be-run.sh 'dotnet test tests/Mita.Api.UnitTests --filter "FullyQualifiedName~FinancialPeriod|FullyQualifiedName~Shares|FullyQualifiedName~OverviewMath|FullyQualifiedName~OverviewRanges|FullyQualifiedName~ReportMath"'`
- [ ] **Step 3: Implement, run, see them pass.**
- [ ] **Step 4: Commit** — `feat: share, change and savings rate math for overview and reports`

### Task 2: Reports

**Files:** `Common/Reporting/{ReportingQueries,CategorySpending}.cs`, `Features/Reports/GetReport/*`, `Features/Reports/ReportsEndpoints.cs`, `Program.cs`; tests `tests/Mita.Api.UnitTests/Features/Reports/GetReportValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Reports/ReportsTests.cs`, `Infrastructure/TestData.cs`.

**Interfaces:**
- Consumes: Task 1; `SettingsQueries.SettingsAsync`, `UtcCalendar`, `IsoDate`, `CurrencyWire`, `CategoryWire`, `TestData.AddTransactionAsync`, `TestData.AddCategoryAsync`, `TestData.AddRatesAsync`, `factory.WithIsolatedData()`.
- Produces:
  - `ReportingQueries.CountedTransactions(this AppDbContext db, Guid userId, DateOnly from, DateOnly to) : IQueryable<Transaction>` (no tracking; Decisions rule).
  - `ReportingQueries.SpendingAsync(this AppDbContext db, Guid userId, DateOnly from, DateOnly to, CancellationToken ct) : Task<IReadOnlyList<CategorySpending>>`: expense sums per category over `CountedTransactions`, amounts > 0 only, unordered; `sealed record CategorySpending(Guid CategoryId, string Name, CategoryColor Color, SystemCategory? System, long Amount)`.
  - `GetReportQuery(string? Range) : IRequest<Result<ReportResponse>>`; `GetReportValidator`: `range` invalidValue when not null and not parsable.
  - `sealed record ReportResponse(string Range, string From, string To, string Currency, IReadOnlyList<ReportPeriodResponse> Periods, ReportTotalsResponse Totals, long AverageIncome, long AverageExpense, double? SavingsRate, ReportStructureResponse Structure, bool HasTransactions)`; `ReportPeriodResponse(string Period, string From, string To, long Income, long Expense)`; `ReportTotalsResponse(long Income, long Expense)`; `ReportStructureResponse(string Period, string From, string To, long Total, IReadOnlyList<ReportStructureItemResponse> Items)`; `ReportStructureItemResponse(Guid CategoryId, string Name, string Color, long Amount, double Percent)`.
  - `GET /v1/reports` (`string? range = null` bound from the query; if minimal API binding turns `?range=` into null, the endpoint passes the raw `HttpRequest.Query["range"]` value instead so it is rejected). `MapReportsEndpoints` registered in `Program.cs`, tag `Reports`.
  - `TestData.AddTransactionAsync` gains `string title = ""`.
- [ ] **Step 1: Failing tests**
  - Validator rows: `1m`, `6M`, `year`, `6`, `""` → `range` invalidValue; null and `12m` valid.
  - Integration (dates relative to `today = DateOnly.FromDateTime(DateTime.UtcNow)`, `current = FinancialPeriod.For(today, Day(1))`, `back(n) = Shift(current.Key, −n, Day(1))`; categories created through `TestData` with distinct names):
    - `Should_SumPerPeriod_OverSixMonthsByDefault`: income 700 000 on `current.From`, expense 80 000 today (category Housing), income 500 000 on `back(5).From`, expense 40 000 on `back(5).To` (Groceries), 10 001 on `back(2).From`, 99 999 on `back(6).To` → `range` `6m`, `from` `back(5).From`, `to` `current.To`, `currency` `PLN`; six `periods` with keys `back(5)` … `current`; `periods[0]` = `{ period, from, to, income 500 000, expense 40 000 }`; `periods[3]` income 0 expense 10 001; `periods[5]` income 700 000 expense 80 000; `totals { 1 200 000, 130 001 }`, `averageIncome` 200 000, `averageExpense` 21 667, `savingsRate` 89.2, `hasTransactions` true; the top-level property set equals the contract names, and so do those of a period, `totals`, `structure` and an item.
    - `Should_CoverThreeAndTwelvePeriods`: expenses 1 000 on `back(2).From`, 2 000 on `back(3).To`, 4 000 on `back(11).From`, 8 000 on `back(12).To` → `3m`: `from` `back(2).From`, totals `{ 0, 1 000 }`, averageIncome 0, averageExpense 333, savingsRate null, 3 periods; `12m`: `from` `back(11).From`, totals expense 7 000, averageExpense 583, 12 periods.
    - `Should_BreakDownCurrentPeriod_LargestFirst`: today Rent 210 000, Food 124 000, the user's expense `Other` (seeded) 50 000, Shops 20 000, Pharmacy 20 000; Coffee 7 000 on `back(1).From`; an income 500 000 → `structure` = `{ period current, from, to, total 424 000, items: Rent 49.5, Food 29.3, Other 11.8, Pharmacy 4.7, Shops 4.7 }` (equal amounts by name, invariant culture), each item's `color` the wire value of its category's color, percents add up to 1000 tenths.
    - `Should_LeaveTransfersOut`: an expense 10 000 and a transfer between two accounts of the user → totals `{ 0, 10 000 }`, structure items only the expense category.
    - `Should_FollowMonthStart`: `PUT /v1/settings` with `monthStartDay` 10; `period = For(today, Day(10))`; expenses 30 000 on `period.From` and 20 000 on `period.From − 1 day` → last period `{ period.Key, From, To, 0, 30 000 }`, the one before ends on `period.From − 1` with expense 20 000; totals expense 50 000; structure `{ period.Key, total 30 000 }`.
    - `Should_RepriceReport_When_BaseCurrencyChanges` (isolated data, EUR 4 on today): expense 10 000 and income 50 000 today, switch to EUR → `currency` EUR, last period income 12 500 expense 2 500, structure first item 2 500.
    - `Should_ReturnEmptyReport_And_IsolateUsers`: a fresh user → totals 0/0, averages 0, savingsRate null, structure `{ period current, total 0, items [] }`, `hasTransactions` false, 6 periods all zero; another user's expense does not show up.
    - `Should_RejectUnknownRange` (`?range=1m` and `?range=` → 400 `range: ["validation.invalidValue"]`), `Should_Return401_When_NotAuthenticated`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: reports per financial period with spending structure`

### Task 3: Overview

**Files:** `Features/Overview/GetOverview/*` (endpoint, query, validator, handler, response records), `Features/Overview/OverviewEndpoints.cs`, `Program.cs`; tests `tests/Mita.Api.UnitTests/Features/Overview/GetOverviewValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Overview/OverviewTests.cs`.

**Interfaces:**
- Consumes: Task 1 (`OverviewRanges`, `OverviewMath`, `Shares`), Task 2 (`ReportingQueries`, `CategorySpending`, `TestData.AddTransactionAsync(title)`); `BudgetQueries.BudgetsInAsync`, `BudgetView.Summarize`, `GoalQueries.GoalsAsync`, `AccountQueries.ProjectAccounts`, `RateTable.PlnValuesAsync`, `RateTable.Rate`, `MinorUnits.Convert`, `SettingsQueries.SettingsAsync`, `TestData.AddBudgetAsync`, `AddGoalAsync`, `AddAccountAsync`, `AddTransferAsync`.
- Produces:
  - `GetOverviewQuery(string? Period) : IRequest<Result<OverviewResponse>>`; `GetOverviewValidator` (`period` invalidValue when not null and not parsable); endpoint as in Task 2 for the empty value.
  - `sealed record OverviewResponse(string Range, string From, string To, string PreviousFrom, string PreviousTo, string Currency, OverviewBalanceResponse Balance, OverviewTotalResponse Income, OverviewTotalResponse Expense, long? AverageExpense, int TransactionCount, IReadOnlyList<SpendingSliceResponse> Spending, OverviewMonthResponse Month, IReadOnlyList<OverviewBudgetResponse> TopBudgets, IReadOnlyList<OverviewTransactionResponse> Recent, IReadOnlyList<OverviewGoalResponse> Goals, int GoalCount, bool HasTransactions)`; `OverviewBalanceResponse(long Amount, int Accounts)`; `OverviewTotalResponse(long Amount, int Count, double? Change)`; `SpendingSliceResponse(Guid? CategoryId, string? Name, long Amount, double Percent)`; `OverviewMonthResponse(string Period, string From, string To, long Planned, long Spent, long Left, int Budgets)`; `OverviewBudgetResponse(Guid Id, Guid CategoryId, string Name, long Limit, long Spent, long Remaining, string Status)`; `OverviewTransactionResponse(Guid Id, string Kind, string Date, string Title, string CategoryName, string AccountName, long Amount)`; `OverviewGoalResponse(Guid Id, string Name, long Target, long Saved, int Percent, string Status, string? Deadline)`. Every nullable is serialized as `null` (not omitted).
  - Handler per Decisions, inside one `REPEATABLE READ` transaction; null from `BudgetsInAsync` or `GoalsAsync`, or a missing balance rate → `Error.Unavailable("rates.unavailable")`.
  - `GET /v1/overview`, `MapOverviewEndpoints` registered in `Program.cs`, tag `Overview`.
- [ ] **Step 1: Failing tests**
  - Validator rows: `day`, `2026-10`, `Week`, `""` → `period` invalidValue; null, `week`, `month`, `year` valid.
  - Integration (dates relative to today UTC, `current = For(today, Day(1))`, `previous = Shift(current.Key, −1)`):
    - `Should_SumMonth_AndCompareWithPrevious`: expenses 30 000 (Groceries, `current.From`) and 50 000 (Housing, today), income 700 000 (`current.From`), expense 40 000 and income 500 000 on `previous.From` → `range` month, `from`/`to`/`previousFrom`/`previousTo` of `current` and `previous`, `currency` PLN, `income { 700 000, 1, 40 }`, `expense { 80 000, 2, 100 }`, `averageExpense` 40 000, `transactionCount` 3, `hasTransactions` true; the property sets of the document and of every nested object equal the contract names, and `averageExpense`, `change`, `categoryId`, `name`, `deadline` are present as `null` when null.
    - `Should_SplitSpending_IntoTopFiveAndBucket`: today Housing 210 000, Groceries 124 000, Leisure 42 000, Cafes 41 200, Transport 31 000, Health 20 000, Shopping 15 370 and the expense `Other` 10 000 → `spending` = Housing 42.5, Groceries 25.1, Leisure 8.5, Cafes 8.4, Transport 6.3, then `{ categoryId null, name null, amount 45 370, percent 9.2 }`; percents add up to 1000 tenths; `expense { 493 570, 8 }`.
    - `Should_CoverWeekAndYear`: expenses 1 000 today, 2 000 today − 6, 4 000 today − 7, 8 000 today − 13, 16 000 today − 14 → `week`: `from` today − 6, `to` today, `previousFrom` today − 13, `previousTo` today − 7, `expense { 3 000, 2, −75 }`. Separately (fresh user) 1 000 on `Shift(current.Key, −11).From` and 2 000 the day before → `year`: `from` that day, `to` `current.To`, `previousFrom` `Shift(current.Key, −23).From`, `previousTo` the day before, `expense { 1 000, 1, −50 }`.
    - `Should_KeepMonthOnCurrentPeriod_WithTopBudgets`: budgets in `current` Groceries 100 000, Cafes 10 000, Housing 20 000, Sport 50 000, Transport 30 000, Leisure 40 000; expenses today Groceries 50 000, Cafes 9 000, Housing 25 000; `?period=week` → `month = { current.Key, From, To, planned 250 000, spent 84 000, left 166 000, budgets 6 }`; `topBudgets` names/status/remaining = Housing over_budget −5 000, Cafes almost_spent 1 000, Groceries on_track 50 000, Sport on_track 50 000, Transport on_track 30 000; the first has `categoryId` of Housing, `limit` 20 000, `spent` 25 000.
    - `Should_TotalBalances_OfAccountsHoldingMoney` (isolated data, EUR 4 on today): accounts PLN card 100 000, EUR savings 1 000, PLN cash 5 000 → `balance { 109 000, 3 }`; an expense of 12 345 from the card → `{ 96 655, 3 }`; the cash account archived (through `TestData` or `PUT /v1/accounts/{id}`) → still `{ 96 655, 3 }`; an archived PLN account with opening balance 0 → unchanged count 3.
    - `Should_ListNewestTransactions_OfTheRange`: an expense 500 titled `old` on `previous.From`, then eight expenses today titled `t1` … `t8` created in order → `recent` titles `t8` … `t3`; `recent[0]` = `{ id, kind expense, date today, title t8, categoryName, accountName, amount 8 000 }`; a transfer between two accounts never appears.
    - `Should_PreviewThreeGoals_NotReachedFirst`: goals A 10 000 (saved 10 000), B 20 000, C 30 000, D 40 000 created in order → `goals` names B, C, D; `goals[0] = { id, name B, target 20 000, saved 0, percent 0, status active, deadline null }`; `goalCount` 4.
    - `Should_RepriceOverview_When_BaseCurrencyChanges` (isolated data, EUR 4 on today and on `current.From`): a PLN card with opening balance 40 000, an expense 10 000 today; switch to EUR → `currency` EUR, `expense.amount` 2 500, `spending[0].amount` 2 500, `recent[0].amount` 2 500, `balance.amount` 7 500.
    - `Should_Return503_When_BalanceNeedsMissingRate` (isolated data, no rates): an EUR account with opening balance 1 000 → 503 with `code` `rates.unavailable`.
    - `Should_FollowMonthStart_ForMonthAndYear`: `monthStartDay` 10 → `month`: `from`/`to` of `For(today, Day(10))`, `month.period` its key; `year`: `from` = `Shift(key, −11, Day(10)).From`.
    - `Should_ReturnEmptyOverview_And_IsolateUsers`: a fresh user → `balance { 0, 0 }`, `income`/`expense` `{ 0, 0, null }`, `averageExpense` null, `transactionCount` 0, empty `spending`, `topBudgets`, `recent`, `goals`, `month` zeros with `budgets` 0, `goalCount` 0, `hasTransactions` false; another user's transactions, budgets and goals do not show up.
    - `Should_RejectUnknownPeriod` (`?period=day`, `?period=` → 400 `period: ["validation.invalidValue"]`), `Should_Return401_When_NotAuthenticated`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: overview with totals, spending, budgets, recent transactions and goals`

### Task 4: API contract, OpenAPI and the Docker smoke

**Files:** `docs/api-contract.md` (title; two endpoint rows; `Overview`, `OverviewTotal`, `SpendingSlice`, `OverviewMonth`, `OverviewBudget`, `OverviewTransaction`, `OverviewGoal`, `Report`, `ReportPeriod`, `ReportStructure`, `ReportStructureItem` types; a section "Overview and reports" with the ranges, counted transactions, rounding of changes, shares and savings rate, balance, month block, previews, 503 on the overview, no caching, no paging), `tests/Mita.Api.IntegrationTests/OpenApiTests.cs`; outside the repo `dock.mita-work/scripts/or-docker-smoke.sh` (built from `bg-docker-smoke.sh`).
- [ ] **Step 1: Failing test** — `Should_DescribeOverviewAndReports`: the document contains `/v1/overview` and `/v1/reports` with tags Overview and Reports and the query parameters `period` and `range`.
- [ ] **Step 2: Implement, run, see it pass;** Docker smoke `or-docker-smoke.sh`:
  1. `docker compose up -d --build`; register;
  2. a PLN card with 100 000; an income of 50 000 in the first income category and an expense of 1 250 in Groceries today;
  3. `GET /v1/overview` → 200, `income.amount` 50 000, `expense.amount` 1 250, `balance.amount` 148 750, `recent` length 2, `hasTransactions` true; `?period=week` and `?period=year` → 200; `?period=day` → 400;
  4. `GET /v1/reports` → 200, 6 periods, `totals { 50 000, 1 250 }`, `structure.items[0].amount` 1 250; `?range=12m` → 12 periods; `?range=1m` → 400;
  5. `docker compose down -v` (or leave a pre-existing stack up, as the other smokes).
- [ ] **Step 3: Commit** — `docs: api contract for overview and reports`

### Task 5: Frontend hybrid mode against real overview and reports

Frontend repo, branch `feature/hybrid-overview-and-reports` from `origin/develop`.

**Files:** `.env.example` (`NUXT_PUBLIC_API_REAL_PATHS=…,/v1/overview,/v1/reports`), the project guide in the repository root (section "Running against the backend": twelve prefixes are real, only the demo endpoints `/v1/demo/*` stay on mocks; step 2 command; step 3 describes the new spec), new `tests/e2e-hybrid/overview-reports.hybrid.spec.ts`; outside the repo `dock.mita-work/scripts/al-hybrid-smoke.sh` (the same twelve prefixes).
- [ ] **Step 1: Write `overview-reports.hybrid.spec.ts`** (English UI; labels checked against the real components):
  1. register a unique user; add the account "Wallet" (Cash, PLN, 100) on `/accounts`;
  2. on `/transactions` add an income of 40.00 "Pay" in the first income category and an expense of 12.50 "Bread" in "Groceries", both from "Wallet";
  3. on `/` the overview shows income 40.00 and expenses 12.50, the recent transactions list "Bread" and "Pay", and the spending legend lists "Groceries"; switching the range to week keeps both figures;
  4. on `/reports` the stats show income 40.00 and expenses 12.50 and the spending structure lists "Groceries";
  5. delete the user in Settings.
- [ ] **Step 2: Run** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/scripts/al-hybrid-smoke.sh` (backend branch from Tasks 1-4 checked out); the five specs pass.
- [ ] **Step 3: Run** `bun run lint && bun run typecheck && bun test tests/unit` in the frontend; all green.
- [ ] **Step 4: Commit** — `test: hybrid e2e against real overview and reports`

### Task 6: Pull requests

- [ ] Backend:
  1. Rebase on `origin/develop` and fold fix commits into their task commits.
  2. Run `dotnet build -warnaserror` and `dotnet test --logger junit` (Docker running), the Docker smoke and the hybrid smoke; all must be green.
  3. Push, open a PR to `develop` (plain description), wait for `check-source-branch`, then `gh pr merge --rebase`.
- [ ] Frontend: same flow for `feature/hybrid-overview-and-reports` after the backend PR is merged, with lint, typecheck, unit and the full E2E suite green.
