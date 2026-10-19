# Budgets and Goals Implementation Plan

**Goal:** Real budgets and goals on the backend. Budgets per expense category and financial period with spent, status, per-day hint, a summary and copying from the previous period; goals with a target, an optional deadline and an optional linked savings account, contributions that become transfers into that account, and a summary; category `inUse` and reassign-on-delete that see budgets, and `linkedGoals` on accounts, exactly as the frontend contract and mocks define them.

**Architecture:**
- **Periods:** financial periods move to the server. `PeriodKey` (`YYYY-MM`, 1970-2099) and `FinancialPeriod` (from/to for a key under a `MonthStart`, the period of a date, shifting, days left) are pure domain code in `Mita.Domain/Periods`, ported from `app/shared/period/period.ts`.
- **Stored currencies:** a budget stores `Limit` with the `Currency` it was written in, a goal `Target` and `Saved` with its `Currency`, a contribution `BaseAmount` with its `BaseCurrency`, as the mock rows do. Reads convert into the current base currency, as the mock does: budget limits at the rate of the period start, goals at today's rate, contributions at the rate of their date. A goal is rebased (target and saved rewritten into the base currency at today's rate) when it is updated or receives a contribution; a budget takes the base currency when it is written.
- **Spent:** Σ `BaseAmount` of expense transactions of the category inside the period. `BaseAmount` is always in the current base currency (categories plan), so no conversion is needed.
- **Slices:** `Features/Budgets/{ListBudgets,CreateBudget,UpdateBudget,DeleteBudget,CopyBudgets}` and `Features/Goals/{ListGoals,CreateGoal,UpdateGoal,DeleteGoal,ContributeToGoal}`, shared parts in `Common/Budgets` and `Common/Goals`.

**Tech Stack:** .NET 10, EF Core migrations (Npgsql), MediatR 12.5.0, FluentValidation, xUnit v3, Shouldly, Moq, Bogus, Testcontainers. Frontend task: Nuxt, Playwright. No new packages.

**Spec:** This plan argues from these documents:
- The backend project guide in the repository root, `docs/STACK.md`, `docs/api-contract.md` and the merged plans in `docs/superpowers/plans/` (especially `2026-10-19-categories-and-transactions.md`, whose "What later plans need" section this plan answers).
- The frontend contract in the frontend repo:
  - `docs/superpowers/specs/2026-10-15-frontend-mocks-design.md` (budget `period` is the `YYYY-MM` of the financial period start and `limit` is in the base currency; goal `saved` in the base currency; "a goal contribution is a transfer to the goal's savings account when the goal has one; otherwise it only increases `saved`"; the period follows `monthStartDay`; status below 85 % on track, 85-100 % almost spent, above 100 % over budget; per-day and per-month hints);
  - `app/shared/api/contract.ts` (`BudgetDto`, `BudgetInput`, `BudgetSummary`, `BudgetPage`, `BudgetCopyInput`, `BudgetCopyResult`, `GoalDto`, `GoalInput`, `GoalSummary`, `GoalPage`, `GoalContributionInput`, `AccountDto.linkedGoals`);
  - `app/shared/period/period.ts` (periods, `isPeriodKey`, `daysLeft`);
  - `mocks/handlers/budgets.ts`, `mocks/budgets.ts`, `mocks/handlers/goals.ts`, `mocks/goals.ts`, `mocks/categories.ts` (`budgetReference`), `mocks/handlers/accounts.ts` (`linkedGoals`, `unlinkAccount`), `mocks/transfers.ts`, `mocks/rates.ts`, `mocks/db.ts`, `mocks/paginate.ts`, `mocks/validation.ts` (rules and codes);
  - `tests/unit/shared/period.test.ts`, `tests/unit/mocks/budgets.test.ts`, `tests/unit/mocks/goals.test.ts` (the expected values below come from these);
  - `app/features/budgets/api/budgetsApi.ts`, `app/features/goals/api/goalsApi.ts`, `app/shared/api/invalidation.ts`.
- The deferred follow-ups in the categories ledger (`dock.mita-work/categories-transactions-ledger.md`).

## Decisions

| Topic | Decision |
|---|---|
| Scope | Budgets, goals, contributions, server-side financial periods, category `inUse` and reassignment with budgets, `linkedGoals`, the base-currency change check for budgets and goals. Overview and reports stay out (see "What later plans need") |
| Today | `UtcCalendar.Today(TimeProvider)` everywhere (current period, days left, goal status, deadline checks, saving window). The mock uses the browser's local date; the server has no user time zone |
| Period keys | `PeriodKey` accepts exactly `^(19[7-9]\d\|20\d\d)-(0[1-9]\|1[0-2])$`, stored as `varchar(7)` in `YYYY-MM` form so string order equals period order |
| Periods | `FinancialPeriod.ByKey(key, start)`: from = start of the key's month (day `start`, or the last working day, Monday to Friday, of the previous month), to = the day before the next key's start. `For(date, start)` tries the date's month, then the previous, then the next. `Shift(key, months, start)`. `DaysLeft(today)` = 0 after `To`, else the inclusive day count from `max(today, From)` to `To` |
| Budget rows | `Limit`, `Currency` (base currency at write), `Period`, `CategoryId`, `Position` (index in a copy, 0 otherwise). One budget per user, category and period: unique index `IX_budgets_user_category_period`. A lost race (`23505`) answers `categoryId: ["validation.budgetExists"]`; a category deleted meanwhile (`23503`) answers `categoryId: ["validation.unknownCategory"]` |
| Budget categories | Expense categories of the user that are not `transfer`. Missing, foreign, non-GUID or `transfer` → `unknownCategory`; income → `expenseCategoryOnly`; `budgetExists` only when `period` is valid. A category with a budget is `inUse`, so its kind is locked and an expense-only budget can never end on an income category |
| Budget view | `limit` = `Limit` converted from `Currency` into the base currency at the rate of the period `From` (`round(Limit × rate)`, the rate rounded to 6 decimals, both half away from zero); `spent` as above; `remaining = limit − spent` (negative when over); `status`: `over_budget` when `spent > limit`, `almost_spent` when `spent × 100 ≥ limit × 85`, else `on_track`; `perDay = floor(remaining / daysLeft)` when both are > 0, else null. `summary = { planned: Σ limit, spent: Σ spent, left: planned − spent, daysLeft }` over every budget of the period, not only the page |
| Budget order | `CreatedAt`, `Position`, `Id` (mock insertion order; a copy keeps the source order) |
| Budget list | `GET /v1/budgets?period&page&pageSize`: `period` defaults to the period of today; default pageSize 100, max 100. Answer `BudgetPage = Page<BudgetDto> & { period, from, to, currency, summary }` |
| Budget copy | `POST /v1/budgets/copy { period }` copies every budget of `Shift(period, −1)` whose category has no budget in `period`, with the limit converted into the base currency at the source period's `From`, in one `INSERT … SELECT … ON CONFLICT DO NOTHING` (so a concurrent create never fails the copy). Answers 200 `{ copied }`; 0 rows → 400 `period: ["validation.nothingToCopy"]` |
| Category reassignment | Deleting a category moves its budgets to the user's expense `other`; when `other` already has a budget in the same period, that budget's limit becomes the sum of both limits converted into the base currency at the period `From`, its currency the base currency, and the moved row is deleted (mock `budgetReference.reassign`). The merged limit is not capped |
| Category `inUse` | `CategoryQueries.UsedCategoryIds(db)`: category ids of transactions `UNION` budgets; used by the list projection, `UpdateCategoryKindValidator` and `UpdateCategoryHandler` |
| Goal rows | `Name`, `Target`, `Saved`, `Currency`, `Deadline` (`PeriodKey?`), `AccountId` (FK to accounts `ON DELETE SET NULL`, the mock's `unlinkAccount`) |
| Goal validation | `name` required / invalidValue (not a string, or a control character: backend rule for names, the mock has none) / nameTooLong (> 40 after trim); `target` required / invalidValue (not a safe integer or > 100 000 000 000) / positiveAmount; `deadline` optional, invalidValue when not a period key or later than the current month + 600, deadlinePast when before the current month, both skipped when equal to the stored deadline; `accountId` optional, unknownAccount (missing, foreign, non-GUID), then, unless it equals the stored link, savingsAccountOnly and archivedAccount |
| Goal view | `target` and `saved` converted into the base currency at today's rate; `remaining = max(target − saved, 0)`; `percent = floor(saved × 100 / target)`; `months = (deadline year × 12 + month) − (today year × 12 + month)`; `status`: `reached` when `saved ≥ target`, `overdue` when a deadline has `months < 0`, else `active`; `monthly`: null without a deadline, when `remaining ≤ 0` or `months < 0`, else `ceil(remaining / max(months, 1))`; `currency` the base currency |
| Goal list | `GET /v1/goals?page&pageSize`, default 100, max 100, order `CreatedAt`, `Id`. Answer `GoalPage = Page<GoalDto> & { currency, summary }`, `summary = { saved: Σ saved, monthly, active: count(status ≠ reached) }` over every goal. `monthly = round(Σ contribution value / 3)` over the user's contributions with `today − 3 months < date ≤ today` (the month step clamps to the end of a shorter month), each valued from `BaseCurrency` into the base currency at the rate of its date (one SQL statement with `pln_value`) |
| Goal update | Rebase first (when `Currency` differs from the base: `Target` and `Saved` converted at today's rate, `Currency` = base), then replace `name`, `target` (taken as base currency), `deadline`, `accountId` |
| Goal delete | Deletes the goal and its contributions (FK cascade); transfers stay, as in the mock |
| Contribution rules | `date` required / invalidValue / dateTooFar; `amount` required / invalidValue / positiveAmount; `rate` invalidValue (the `ManualRate` rule, cap 1 000 000, which the mock lacks); `accountId: archivedAccount` when the goal's linked account is archived; `fromAccountId`, checked when the goal is linked or the field is present: required / unknownAccount (incl. non-GUID) / archivedAccount, then sameAccount when it equals the linked account |
| Contribution effect | Unlinked goal: no money moves, `amount` is in the base currency, `baseAmount = amount`, `fromAccountId` is stored when sent. Linked goal: a transfer from `fromAccountId` to the linked account (rate 1 for the same currency, else the manual rate rounded to 6 decimals, else the stored rate of the date; `note` = goal name), with the transfer rules `rate: positiveAmount` / `rate: invalidValue` / 503 `rates.unavailable`; `baseAmount` = `toAmount` converted from the linked account currency into the base at the date. Then rebase the goal at today's rate and add `baseAmount` to `Saved`. Answers 200 with the `GoalDto` (no `Location`: the body is the goal, not the contribution) |
| Status codes | `POST /v1/budgets` 201 `Location: /v1/budgets/{id}`; `POST /v1/goals` 201 `Location: /v1/goals/{id}` (no GET by id, as categories); `POST /v1/budgets/copy` 200; `POST /v1/goals/{id}/contributions` 200; `PUT` 200; `DELETE` 204; routes use `{id:guid}`; validation before lookup, so an invalid body on an unknown id answers 400 |
| Base currency change | `PUT /v1/settings` with a new base currency also answers 503 `rates.unavailable` (nothing changes) when a budget currency, goal currency or contribution base currency other than the new base, or the new base itself when any such row exists, has no stored rate at all. Reads therefore always find a rate; a missing one still answers 503 instead of 500 |
| Races and lock order | Global order: settings → accounts → categories → transactions → budgets / goals. Budget writes lock settings `FOR SHARE` and the category `FOR SHARE` and re-check it. Category delete locks the category, then the user's expense `other`, both `FOR UPDATE`, before moving budgets. Goal create/update lock settings `FOR SHARE`, the new account `FOR SHARE` (re-check), the goal `FOR UPDATE` (update). A contribution reads the goal's link, locks settings `FOR SHARE`, the source and linked accounts `FOR UPDATE ORDER BY "Id"` (as transfers), then the goal `FOR UPDATE`; if the link changed in between it answers 409 `goal.concurrentUpdate` (as `user.concurrentUpdate`) |
| Caching | Budgets and goals are not cached (they depend on today, rates and transactions). Budget writes invalidate `categories` (`inUse`); goal create/update/delete invalidate `accounts` (`linkedGoals`); contributions invalidate `accounts` and `transfers` |
| Errors in reads | A budget or goal read that needs a rate that is not stored answers 503 `rates.unavailable` (unreachable after the settings check, kept so it never becomes a 500) |
| Hybrid frontend | `/v1/budgets` and `/v1/goals` become real in the hybrid default. Overview and reports stay mocked on the cloned demo data; the frontend project guide says so |

## Global Constraints

- All foundation, auth, accounts and categories constraints apply: warnings are errors; central packages; `Api → Infrastructure → Domain`; records, `sealed`, no comments, English; `CancellationToken` everywhere; every endpoint in the v1 group with `RequireAuthorization("User")`; ProblemDetails for every error; XSRF on unsafe cookie requests (already global); request fields bound as `JsonElement?`; a whitespace-only string counts as missing; first failing rule per field only (a database validator skips a field that already failed a synchronous rule).
- Contract with the frontend, byte for byte (camelCase JSON):
  - `BudgetDto { id, categoryId, period, limit, spent, remaining, status: "on_track"|"almost_spent"|"over_budget", perDay: number|null }`;
  - `BudgetInput { categoryId, period, limit }`; `BudgetCopyInput { period }`; `BudgetCopyResult { copied }`;
  - `BudgetPage = Page<BudgetDto> & { period, from: "YYYY-MM-DD", to, currency, summary: { planned, spent, left, daysLeft } }`;
  - `GoalDto { id, name, target, saved, remaining, percent, monthly: number|null, status: "active"|"reached"|"overdue", deadline: "YYYY-MM"|null, accountId: string|null, currency }`;
  - `GoalInput { name, target, deadline: string|null, accountId: string|null }`;
  - `GoalPage = Page<GoalDto> & { currency, summary: { saved, monthly, active } }`;
  - `GoalContributionInput { amount, date, fromAccountId?, rate? }`;
  - `AccountDto.linkedGoals` = number of the user's goals linked to the account.
- Endpoints:
  - `GET /v1/budgets?period&page&pageSize`, `POST /v1/budgets`, `PUT /v1/budgets/{id}`, `DELETE /v1/budgets/{id}`, `POST /v1/budgets/copy`;
  - `GET /v1/goals?page&pageSize`, `POST /v1/goals`, `PUT /v1/goals/{id}`, `DELETE /v1/goals/{id}`, `POST /v1/goals/{id}/contributions`.
- Validation codes, exactly as the mocks:
  - budgets: `period` required / invalidValue; `categoryId` required / unknownCategory / expenseCategoryOnly / budgetExists; `limit` required / invalidValue (not a safe integer or > 100 000 000 000) / positiveAmount (≤ 0); copy `period` required / invalidValue / nothingToCopy; query `period`, `page`, `pageSize` invalidValue;
  - goals and contributions: as in Decisions; query `page`, `pageSize` invalidValue.
- Every budget, goal and contribution is scoped to the current user. A foreign id answers exactly like an unknown id (404).
- Commit messages: Conventional Commits; no tool attribution and no `Co-Authored-By` trailers.

## Review Focus

- Changing the base currency (or the month start) with budgets and goals in place must show every limit, target, saved amount and the saving average in the new currency, converted from the stored currency (no drift after two changes), and must answer 503 without changing anything when a needed rate is missing. Pinned in Task 9 (`Should_ConvertBudgetsAndGoals_When_BaseCurrencyChanges`, `Should_Return503_When_BudgetCurrencyHasNoRate`) and Task 5 (`Should_RerangePeriod_When_MonthStartChanges`).
- Deleting a category whose budgets collide with budgets of `other` must merge the limits, and deleting it while another request creates a budget in it must never answer 500. Pinned in Task 4 (`Should_MergeLimits_When_ReassignedBudgetCollides`) and Task 5 (`Should_NotFail_When_BudgetCreatedWhileCategoryDeleted`).
- A contribution to a goal linked to an account in another currency must move money with the rate of its date (or the manual one), value `saved` in the base currency, and answer 503 without writing anything when no rate is stored. Pinned in Task 8 (`Should_ConvertCrossCurrencyContribution`, `Should_Return503AndWriteNothing_When_RateMissing`).
- Deleting an account linked to goals must answer 204 and unlink them, and deleting a goal must keep the transfers and balances its contributions made. Pinned in Task 7 (`Should_UnlinkGoals_When_AccountDeleted`) and Task 8 (`Should_KeepTransfers_When_GoalDeleted`).
- Copying budgets twice, or while a budget is created in the target period, must never duplicate a category in a period nor answer 500. Pinned in Task 6 (`Should_CopyOnlyMissingCategories`, `Should_NotDuplicate_When_CopyRacesCreate`).

## What later plans need

- Overview: `FinancialPeriod` for `?period=`; the budget view (`BudgetQueries.BudgetsInAsync`) for `month` and `topBudgets`; `GoalQueries` for `goals`/`goalCount`; income and expense totals from `BaseAmount` excluding the `transfer` category.
- Reports: `FinancialPeriod.Shift` for the 3/6/12 period ranges; `pln_value` for any further conversion.
- Notifications (`budgetLimit`, `goals`) can read `BudgetView.Status` and `GoalView.Status`.

---

## File map

```
src/Mita.Domain/Periods/PeriodKey.cs, FinancialPeriod.cs
src/Mita.Domain/Budgets/Budget.cs, BudgetStatus.cs
src/Mita.Domain/Goals/Goal.cs, GoalContribution.cs, GoalStatus.cs
src/Mita.Infrastructure/Persistence/Configurations/BudgetConfiguration.cs, GoalConfiguration.cs, GoalContributionConfiguration.cs
src/Mita.Infrastructure/Persistence/Migrations/<timestamp>_BudgetsGoals.cs
src/Mita.Api/Common/Wire/JsonValues.cs (TryGetPeriodKey)
src/Mita.Api/Common/Categories/CategoryQueries.cs (UsedCategoryIds)
src/Mita.Api/Common/Accounts/AccountQueries.cs (linkedGoals)
src/Mita.Api/Common/Budgets/BudgetDto.cs, BudgetView.cs, BudgetQueries.cs, IBudgetInput.cs, BudgetInputValidator.cs, BudgetReferencesValidator.cs, BudgetInputParser.cs, BudgetWire.cs
src/Mita.Api/Common/Goals/GoalDto.cs, GoalView.cs, GoalQueries.cs, IGoalInput.cs, GoalInputValidator.cs, GoalReferencesValidator.cs, GoalInputParser.cs, GoalWire.cs
src/Mita.Api/Features/Budgets/{ListBudgets,CreateBudget,UpdateBudget,DeleteBudget,CopyBudgets}/*, BudgetsEndpoints.cs
src/Mita.Api/Features/Goals/{ListGoals,CreateGoal,UpdateGoal,DeleteGoal,ContributeToGoal}/*, GoalsEndpoints.cs
src/Mita.Api/Features/Categories/{UpdateCategory,DeleteCategory}/*, Features/Settings/UpdateSettings/*
tests/Mita.Api.UnitTests/{Periods,Features/Budgets,Features/Goals}/...
tests/Mita.Api.IntegrationTests/Features/{Budgets,Goals,Categories,Settings,Accounts}/..., Persistence/MigrationTests.cs, Infrastructure/TestData.cs
```

---

### Task 1: Financial periods

**Files:** `src/Mita.Domain/Periods/PeriodKey.cs`, `src/Mita.Domain/Periods/FinancialPeriod.cs`; test `tests/Mita.Api.UnitTests/Periods/FinancialPeriodTests.cs`.

**Interfaces:**
- Consumes: `MonthStart` (`Mita.Domain.Users`).
- Produces:
  - `readonly record struct PeriodKey(int Year, int Month) : IComparable<PeriodKey>` with `static bool TryParse(string? text, out PeriodKey key)` (the regex in Decisions), `override string ToString()` (`YYYY-MM`), `PeriodKey AddMonths(int months)`, `static PeriodKey Of(DateOnly date)`, `int MonthIndex` (`Year × 12 + Month`).
  - `readonly record struct FinancialPeriod(PeriodKey Key, DateOnly From, DateOnly To)` with `static FinancialPeriod ByKey(PeriodKey key, MonthStart start)`, `static FinancialPeriod For(DateOnly date, MonthStart start)`, `static FinancialPeriod Shift(PeriodKey key, int months, MonthStart start)`, `int DaysLeft(DateOnly today)`.
- [ ] **Step 1: Failing tests** (rows from `period.test.ts`)
  - `For(2026-10-14, 1)` = `2026-10`, 2026-10-01..2026-10-31; `For(2026-10-05, 10)` = `2026-09`, 2026-09-10..2026-10-09; `For(2026-10-10, 10)` = `2026-10`, 2026-10-10..2026-11-09; `For(2027-01-05, 10)` = `2026-12`, 2026-12-10..2027-01-09.
  - `Shift(2026-12, 1, 1)` = `2027-01`, 2027-01-01..2027-01-31; `Shift(2027-01, −1, 1).Key` = `2026-12`.
  - `ByKey(2026-10, LastWorkingDay)` = 2026-09-30..2026-10-29; `For(2026-10-30, LastWorkingDay).Key` = `2026-11`, `For(2026-10-29, LastWorkingDay).Key` = `2026-10`.
  - Every day of 2026 lies inside `For(day, start)` for starts 1, 10, 28 and LastWorkingDay.
  - `DaysLeft` of October (start 1): 2026-10-14 → 18, 2026-10-31 → 1, 2026-11-02 → 0, 2026-09-20 → 31.
  - `TryParse`: `2026-10`, `1970-01`, `2099-12` parse; `2026-13`, `2026-00`, `2026-1`, `0026-10`, `0000-01`, `2100-01`, `2026-10-01`, `""`, null do not. `new PeriodKey(2026, 10).AddMonths(3)` = `2027-01`, `AddMonths(−10)` = `2025-12`.
- [ ] **Step 2: Run, see them fail.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/be-run.sh 'dotnet test tests/Mita.Api.UnitTests --filter "FullyQualifiedName~FinancialPeriod"'`
- [ ] **Step 3: Implement, run, see them pass.**
- [ ] **Step 4: Commit** — `feat: financial periods from the month start day`

### Task 2: Budgets and goals schema

**Files:** `Mita.Domain/Budgets/*`, `Mita.Domain/Goals/*`, the three configurations, `AppDbContext` (DbSets `Budgets`, `Goals`, `GoalContributions`), migration `BudgetsGoals`; tests `tests/Mita.Api.IntegrationTests/Persistence/MigrationTests.cs` (extended), `tests/Mita.Api.IntegrationTests/Features/Users/MeTests.cs` (extended), new `tests/Mita.Api.IntegrationTests/Persistence/BudgetsGoalsSchemaTests.cs`, `Infrastructure/TestData.cs`.

**Interfaces:**
- Consumes: `PeriodKey` (Task 1), `Currency`, `IAuditable`, `MinorUnits`.
- Produces:
  - `enum BudgetStatus { OnTrack, AlmostSpent, OverBudget }`, `enum GoalStatus { Active, Reached, Overdue }` (wire values in the API).
  - `sealed class Budget : IAuditable` with `Guid Id, Guid UserId, Guid CategoryId, PeriodKey Period, long Limit, Currency Currency, int Position`, `static Budget Create(Guid userId, Guid categoryId, PeriodKey period, long limit, Currency currency)` (position 0), `void Update(Guid categoryId, PeriodKey period, long limit, Currency currency)`, `void MoveTo(Guid categoryId)`, `void Absorb(long limit, Currency currency)` (sets both).
  - `sealed class Goal : IAuditable` with `Guid Id, Guid UserId, string Name, long Target, long Saved, Currency Currency, PeriodKey? Deadline, Guid? AccountId`, `const int NameMaxLength = 40`, `static Goal Create(Guid userId, string name, long target, PeriodKey? deadline, Guid? accountId, Currency currency)` (saved 0), `void Update(string name, long target, PeriodKey? deadline, Guid? accountId)`, `void Rebase(Currency currency, decimal rate)` (target and saved through `MinorUnits.Convert`), `void AddSaved(long amount)`.
  - `sealed class GoalContribution : IAuditable` with `Guid Id, Guid UserId, Guid GoalId, DateOnly Date, long Amount, Currency Currency, Guid? FromAccountId, Guid? TransferId, long BaseAmount, Currency BaseCurrency` and `static GoalContribution Create(…)` with those fields.
  - Tables:
    - `budgets`: FK user cascade; FK categories `NO ACTION`; `Period` varchar(7) through a `PeriodKey` value converter; `Currency` varchar(3); unique index `IX_budgets_user_category_period` on `(UserId, CategoryId, Period)`; index `(UserId, Period)`; check `"Limit" > 0`.
    - `goals`: FK user cascade; FK accounts `SET NULL`; `Name` varchar(40); `Deadline` varchar(7) null; index `UserId`, `AccountId`; check `"Target" > 0`.
    - `goal_contributions`: FK user cascade, FK goals cascade, FKs accounts (`FromAccountId`) and transfers (`TransferId`) `SET NULL`; indexes `(UserId, Date)`, `GoalId`.
  - `TestData`: `Task<Budget> AddBudgetAsync(factory, Guid userId, Guid categoryId, string period, long limit, Currency currency = PLN)`, `Task<Goal> AddGoalAsync(factory, Guid userId, string name, long target, long saved = 0, Currency currency = PLN, string? deadline = null, Guid? accountId = null)`, `Task AddContributionAsync(factory, Goal goal, string date, long baseAmount, Currency baseCurrency = PLN)`; `AddAccountAsync` gains `AccountType type = Card`.
- [ ] **Step 1: Failing tests**
  - `Should_ApplyMigrations_When_DatabaseIsEmpty` (extended): tables `budgets`, `goals`, `goal_contributions`.
  - `Should_DeleteUserWithBudgetsAndGoals`: a user with a budget, a goal linked to a savings account and a contribution; `DELETE /v1/me` gives 204 and no `budgets`, `goals` or `goal_contributions` rows remain for that user.
  - `Should_RejectDuplicateBudget`: two `AddBudgetAsync` calls for the same user, category and period throw a `DbUpdateException` whose inner `PostgresException.ConstraintName` is `IX_budgets_user_category_period`.
- [ ] **Step 2: Run, see them fail.**
- [ ] **Step 3: Implement, then add the migration** with `dotnet ef migrations add BudgetsGoals --project src/Mita.Infrastructure --startup-project src/Mita.Api`.
- [ ] **Step 4: Run, see them pass,** then the full suite.
- [ ] **Step 5: Commit** — `feat: budgets and goals schema`

### Task 3: Budget view and list

**Files:** `Common/Wire/JsonValues.cs`, `Common/Budgets/{BudgetDto,BudgetWire,BudgetView,BudgetQueries}.cs`, `Features/Budgets/ListBudgets/*`, `Features/Budgets/BudgetsEndpoints.cs`, `Program.cs`; tests `tests/Mita.Api.UnitTests/Features/Budgets/BudgetViewTests.cs`, `ListBudgetsValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Budgets/ListBudgetsTests.cs`.

**Interfaces:**
- Consumes: Task 1, Task 2 (`Budget`, `TestData.AddBudgetAsync`); `RateTable.PlnValuesAsync`, `RateTable.Rate`, `MinorUnits.Convert`, `UtcCalendar`, `PagingRules`, `CurrencyWire`, `IsoDate`, `TestData.AddTransactionAsync`.
- Produces:
  - `JsonValues.TryGetPeriodKey(JsonElement?, out PeriodKey)`.
  - `sealed record BudgetDto(Guid Id, Guid CategoryId, string Period, long Limit, long Spent, long Remaining, string Status, long? PerDay)`; `BudgetWire.ToWire(BudgetStatus)` (`on_track`, `almost_spent`, `over_budget`).
  - `static class BudgetView` with `BudgetStatus Status(long spent, long limit)`, `long? PerDay(long remaining, int daysLeft)`, `BudgetDto From(Budget budget, long limitInBase, long spent, int daysLeft)`, `BudgetSummary Summarize(IReadOnlyList<BudgetDto> budgets, int daysLeft)`; `sealed record BudgetSummary(long Planned, long Spent, long Left, int DaysLeft)`.
  - `BudgetQueries.BudgetsInAsync(this AppDbContext db, Guid userId, FinancialPeriod period, Currency baseCurrency, DateOnly today, CancellationToken ct) : Task<IReadOnlyList<BudgetDto>?>`: the user's budgets of `period.Key` in the Decisions order; one grouped query for spent (`Kind == Expense`, `Date` between `From` and `To`, `CategoryId` in the budgets' categories); limits through `PlnValuesAsync(period.From)` only when a budget currency differs from the base; null when a needed rate is missing.
  - `BudgetQueries.SettingsAsync(this AppDbContext db, Guid userId, CancellationToken ct) : Task<(Currency BaseCurrency, MonthStart MonthStart)>` (plain read).
  - `ListBudgetsQuery(string? Period, int Page, int PageSize)`, not cached; endpoint defaults 1 and 100. `ListBudgetsValidator`: paging rules, `period` invalidValue when present and not a period key.
  - `sealed record BudgetPageResponse(IReadOnlyList<BudgetDto> Items, int Page, int PageSize, int Total, string Period, string From, string To, string Currency, BudgetSummary Summary)`; null from `BudgetsInAsync` → 503 `rates.unavailable`.
- [ ] **Step 1: Failing tests**
  - `BudgetView` rows: status (0, 10 000) on_track, (8 499, 10 000) on_track, (8 500, 10 000) almost_spent, (10 000, 10 000) almost_spent, (10 001, 10 000) over_budget; perDay (206 430, 18) 11 468, (100, 3) 33, (0, 10) null, (−500, 10) null, (1 000, 0) null.
  - Validator rows: `period` `2026-13`, `26-10`, `abc`, `2026-10-01`, `0000-01` → invalidValue; `pageSize=101`, `page=0` → invalidValue; all null is valid.
  - Integration (dates relative to `today = DateOnly.FromDateTime(DateTime.UtcNow)`, `current = FinancialPeriod.For(today, Day(1))`):
    - `Should_CountExpensesInsidePeriodOnly`: budget Groceries 100 000 in `current`; expenses 30 000 on `current.From` and 20 000 today in Groceries, 99 000 the day before `From`, 5 000 in Cafes, an income of 70 000: page `period`, `from`, `to`, `currency` `PLN`, `total` 1, `summary` `{ planned 100 000, spent 50 000, left 50 000, daysLeft }`; item `spent` 50 000, `remaining` 50 000, `on_track`, `perDay = floor(50 000 / daysLeft)`; the property sets equal the contract names.
    - `Should_ReportAlmostSpentAndOverBudget`: limits 10 000 / 10 000, spent 9 000 / 12 500 → `almost_spent` 1 000, `over_budget` −2 500 with `perDay` null; summary planned 20 000, spent 21 500, left −1 500; order is creation order.
    - `Should_HaveNoDaysLeft_InPastPeriod`: previous period `daysLeft` 0, `perDay` null; next period `daysLeft` = its length.
    - `Should_DefaultToCurrentPeriod_And_RejectBadKeys`: no `period` → `current`; `period=2026-13` → 400 `period: ["validation.invalidValue"]`.
    - `Should_RerangePeriod_When_MonthStartChanges`: budget in `current` (start 1) with an expense of 10 000 on `current.From` (the 1st); `PUT /v1/settings` with `monthStartDay` 10: `from`/`to` equal `ByKey(current.Key, Day(10))`, `spent` 0 (the 1st now lies in the previous period); an expense of 7 000 on the new `from` gives `spent` 7 000; the default period is `For(today, Day(10)).Key`.
    - `Should_IsolateUsers`, `Should_Return401_When_NotAuthenticated`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: list budgets with spent, status and summary per financial period`

### Task 4: Budgets in category usage and reassignment

**Files:** `Common/Categories/CategoryQueries.cs`, `Features/Categories/UpdateCategory/{UpdateCategoryKindValidator,UpdateCategoryHandler}.cs`, `Features/Categories/DeleteCategory/DeleteCategoryHandler.cs`, new `Features/Categories/DeleteCategory/BudgetReassignment.cs`; tests `tests/Mita.Api.IntegrationTests/Features/Categories/CategoryBudgetsTests.cs`.

**Interfaces:**
- Consumes: Task 2 (`Budget.MoveTo`, `Budget.Absorb`, `TestData.AddBudgetAsync`), Task 1 (`FinancialPeriod.ByKey`), `RateTable`.
- Produces:
  - `CategoryQueries.UsedCategoryIds(this AppDbContext db) : IQueryable<Guid>` (transaction category ids `Union` budget category ids); the projection, the kind validator and the update handler use `db.UsedCategoryIds().Contains(category.Id)`.
  - `BudgetReassignment.ReassignAsync(AppDbContext db, Guid userId, Guid fromCategoryId, Guid otherId, Currency baseCurrency, MonthStart monthStart, CancellationToken ct) : Task<bool>`: the Decisions rule; false when a needed rate is missing (the handler then answers 503 and rolls back).
  - `DeleteCategoryHandler`: loads the settings row `FOR SHARE` (base currency and month start), locks the category, then `other` (`FOR UPDATE`), moves transactions, calls `ReassignAsync`, deletes.
- [ ] **Step 1: Failing tests**
  - `Should_MarkCategoryInUse_When_ItHasBudget`: a category with only a budget is `inUse`; changing its kind gives `kind: ["validation.categoryKindLocked"]`.
  - `Should_MoveBudgets_When_CategoryDeleted`: budget of "Books" in the previous period, 10 000 → after delete the previous period has one budget on expense `Other` with 10 000.
  - `Should_MergeLimits_When_ReassignedBudgetCollides`: Books 4 000 and Other 6 000 in the current period, an expense of 1 500 in Books; after delete the current period has exactly one budget, on Other, `limit` 10 000 and `spent` 1 500.
  - `Should_MergeInBaseCurrency_When_BudgetCurrenciesDiffer` (isolated data, EUR 4.2717 on the period `From`): Books 4 000 stored in EUR (`TestData`), Other 6 000 PLN, base PLN → merged `limit` 6 000 + 17 087 = 23 087 in PLN.
  - `Should_LeaveOtherBudgetsUntouched_When_NoCollision`: deleting a category whose budgets do not collide leaves the limits of `Other`'s budgets in other periods unchanged.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then every `Categories` test.
- [ ] **Step 3: Commit** — `feat: budgets count as category usage and follow a deleted category`

### Task 5: Create, update and delete budgets

**Files:** `Common/Budgets/{IBudgetInput,BudgetInputValidator,BudgetReferencesValidator,BudgetInputParser}.cs`, `Features/Budgets/{CreateBudget,UpdateBudget,DeleteBudget}/*`; tests `tests/Mita.Api.UnitTests/Features/Budgets/CreateBudgetValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Budgets/BudgetWritesTests.cs`.

**Interfaces:**
- Consumes: Task 3 (`BudgetQueries`, `BudgetDto`), `CategoryCacheTags`, `JsonValues`.
- Produces:
  - `interface IBudgetInput` with `JsonElement? CategoryId, Period, Limit` and `Guid? ExistingId`.
  - `abstract class BudgetInputValidator<T>`: `period` required / invalidValue; `categoryId` required / unknownCategory (non-GUID); `limit` required / invalidValue / positiveAmount.
  - `BudgetReferencesValidator<T>(AppDbContext, ICurrentUser)`: one query for the category (`unknownCategory` when missing, foreign or `Transfer`; `expenseCategoryOnly` for income) and, when `period` is valid, `budgetExists` when another budget of the user (not `ExistingId`) has that category and period.
  - `BudgetInputParser.Parse(IBudgetInput) → ParsedBudget(Guid CategoryId, PeriodKey Period, long Limit)`.
  - `CreateBudgetCommand : IBudgetInput, IInvalidatesCache` (tag `categories`): handler in a transaction locks settings `FOR SHARE`, the category `FOR SHARE` and re-checks it, inserts with the base currency, maps `23505` (`IX_budgets_user_category_period`) to `categoryId: budgetExists` and `23503` to `categoryId: unknownCategory`; answers 201 `Location: /v1/budgets/{id}` with the `BudgetDto` from `BudgetsInAsync` of the budget's period.
  - `UpdateBudgetCommand(Guid Id, …)`: same locks, then the budget `FOR UPDATE` (404 when absent or foreign), `Update(…, baseCurrency)`; 200 `BudgetDto`.
  - `DeleteBudgetCommand(Guid Id)` (tag `categories`): 204 or 404.
- [ ] **Step 1: Failing tests**
  - Validator rows: `{}` → categoryId, period and limit required; `period` `2026-13` → invalidValue; `limit` 0, −100 → positiveAmount; 10.5, `"100"`, 100 000 000 001 → invalidValue; `categoryId` `"cat_x"` → unknownCategory.
  - `Should_CreateBudget_When_InputValid`: 201, `Location` set, body `{ categoryId, period, limit 100 000, spent 0, remaining 100 000, status on_track }`; `Should_ReturnFieldErrors_When_CategoryInvalid`: income category → expenseCategoryOnly, `Transfers` and a foreign category → unknownCategory.
  - `Should_AllowOneBudgetPerCategoryAndPeriod`: a second create in the same period → budgetExists, in the previous period → 201; updating another budget onto the taken category → budgetExists; updating a budget with its own category and period → 200 with the new limit.
  - `Should_StoreBaseCurrency_When_Written` (isolated data, EUR rate on the period `From`): after switching the base to EUR, `PUT` with limit 30 000 answers `limit` 30 000; after switching back to PLN the list shows `round(30 000 × rate(EUR→PLN, From))`.
  - `Should_Return404_When_BudgetBelongsToAnotherUser` for PUT and DELETE; invalid body on an unknown id → 400; `Should_DeleteBudget`: 204, then 404.
  - `Should_NotFail_When_BudgetCreatedWhileCategoryDeleted`: 20 rounds of a fresh category, `DELETE /v1/categories/{id}` and `POST /v1/budgets` for it in parallel; deletes 204, creates 201 or 400 `categoryId: ["validation.unknownCategory"]`, none 500, no budget points at a missing category.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: create, update and delete budgets`

### Task 6: Copy budgets from the previous period

**Files:** `Features/Budgets/CopyBudgets/*`; tests `tests/Mita.Api.IntegrationTests/Features/Budgets/CopyBudgetsTests.cs`.

**Interfaces:**
- Consumes: Task 1 (`FinancialPeriod.Shift`), Task 3 (`BudgetQueries.SettingsAsync`).
- Produces: `CopyBudgetsCommand(JsonElement? Period) : IInvalidatesCache` (tag `categories`); `CopyBudgetsValidator`: required / invalidValue; handler: settings `FOR SHARE`, source period = `Shift(target, −1, monthStart)`, one `ExecuteSqlAsync` `INSERT INTO budgets (…) SELECT gen_random_uuid(), "UserId", "CategoryId", {target}, CASE WHEN "Currency" = {base} THEN "Limit" ELSE round("Limit" * round(pln_value("Currency", {sourceFrom}) / pln_value({base}, {sourceFrom}), 6)) END, {base}, row_number() OVER (ORDER BY "CreatedAt", "Position", "Id") - 1, now(), now() FROM budgets WHERE "UserId" = {userId} AND "Period" = {source} ON CONFLICT ("UserId", "CategoryId", "Period") DO NOTHING`; affected 0 → 400 `period: ["validation.nothingToCopy"]`; else 200 `{ copied }`. `POST /v1/budgets/copy`.
- [ ] **Step 1: Failing tests**
  - `Should_CopyOnlyMissingCategories`: previous Groceries 10 000 and Cafes 5 000, current Cafes 7 000 → `{ copied: 1 }`; current list is Cafes 7 000 then Groceries 10 000; a second copy → 400 `period: ["validation.nothingToCopy"]`; `{ period: "x" }` → invalidValue; `{}` → required.
  - `Should_KeepSourceOrder_When_Copying`: three source budgets created in order A, B, C → target list A, B, C.
  - `Should_ConvertLimit_When_SourceInOtherCurrency` (isolated data): a source budget stored in EUR (`TestData`) with base PLN → the copy's limit is `round(limit × rate(EUR→PLN, source From))`.
  - `Should_NotDuplicate_When_CopyRacesCreate`: 10 rounds of a copy and a `POST /v1/budgets` for a source category in the target period in parallel; never 500, and the target period never has two budgets for one category.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: copy budgets from the previous period`

### Task 7: Goals and linked accounts

**Files:** `Common/Goals/{GoalDto,GoalWire,GoalView,GoalQueries,IGoalInput,GoalInputValidator,GoalReferencesValidator,GoalInputParser}.cs`, `Features/Goals/{ListGoals,CreateGoal,UpdateGoal,DeleteGoal}/*`, `Features/Goals/GoalsEndpoints.cs`, `Program.cs`, `Common/Accounts/AccountQueries.cs`; tests `tests/Mita.Api.UnitTests/Features/Goals/GoalViewTests.cs`, `CreateGoalValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Goals/GoalsTests.cs`, `Features/Accounts/LinkedGoalsTests.cs`.

**Interfaces:**
- Consumes: Task 1 (`PeriodKey`), Task 2 (`Goal`, `TestData.AddGoalAsync`, `AddContributionAsync`), `RateTable`, `UtcCalendar`, `AccountCacheTags`.
- Produces:
  - `sealed record GoalDto(Guid Id, string Name, long Target, long Saved, long Remaining, int Percent, long? Monthly, string Status, string? Deadline, Guid? AccountId, string Currency)`; `GoalWire.ToWire(GoalStatus)` (`active`, `reached`, `overdue`).
  - `static class GoalView` with `int MonthsUntil(PeriodKey deadline, DateOnly today)`, `GoalStatus Status(long saved, long target, PeriodKey? deadline, DateOnly today)`, `long? Monthly(long remaining, PeriodKey? deadline, DateOnly today)`, `GoalDto From(Goal goal, decimal rateToBase, Currency baseCurrency, DateOnly today)`, `DateOnly SavingWindowStart(DateOnly today)` (today minus 3 months, clamped).
  - `GoalQueries.GoalsAsync(this AppDbContext db, Guid userId, Currency baseCurrency, DateOnly today, CancellationToken ct) : Task<IReadOnlyList<GoalDto>?>` (null when a rate is missing) and `GoalQueries.MonthlySavingAsync(db, userId, baseCurrency, today, ct) : Task<long?>` (the SQL from Decisions).
  - `sealed record GoalSummary(long Saved, long Monthly, int Active)`; `sealed record GoalPageResponse(IReadOnlyList<GoalDto> Items, int Page, int PageSize, int Total, string Currency, GoalSummary Summary)`.
  - `interface IGoalInput` (`JsonElement? Name, Target, Deadline, AccountId`, `Guid? ExistingId`); `abstract class GoalInputValidator<T>` (name, target, deadline format); `GoalReferencesValidator<T>(AppDbContext, ICurrentUser, TimeProvider)` (deadline range against today and the stored deadline; account rules against the stored link); `GoalInputParser.Parse → ParsedGoal(string Name, long Target, PeriodKey? Deadline, Guid? AccountId)`.
  - `CreateGoalCommand`, `UpdateGoalCommand(Guid Id, …)`, `DeleteGoalCommand(Guid Id)`, all `IInvalidatesCache` with tag `accounts`; locks per Decisions; a `23503` on save answers `accountId: unknownAccount`.
  - `AccountQueries` projection: `linkedGoals = db.Goals.Count(goal => goal.AccountId == account.Id)`.
- [ ] **Step 1: Failing tests**
  - `GoalView` rows (today 2026-10-16 unless stated): `MonthsUntil` (2027-06, 2026-10-16) 8, (2026-10, 2026-10-31) 0, (2027-01, 2026-12-31) 1, (2026-09, 2026-10-01) −1; `Monthly` (380 000, 2027-06) 47 500, (110 000, 2027-01) 36 667, (50 000, 2026-10) 50 000, (50 000, null) null, (0, 2027-06) null, (50 000, 2026-09) null; `Status` (800 000, 800 000, null) reached, (900 000, 800 000, 2026-01) reached, (100, 800 000, 2026-09) overdue, (100, 800 000, 2026-10) active, (100, 800 000, null) active; `SavingWindowStart` 2026-10-16 → 2026-07-16, 2026-05-31 → 2026-02-28, 2026-02-15 → 2025-11-15; `From` with saved 15 000 of 10 000 → remaining 0, percent 150, reached, monthly null.
  - Validator rows: `{}` → name and target required; name `"   "` → required, `42` → invalidValue, 41 × `x` → nameTooLong, `"a\u0007"` → invalidValue; target 0, −5 → positiveAmount, 10.5, `"100"`, 100 000 000 001 → invalidValue; deadline `2026-13`, `2026-10-01` → invalidValue.
  - Integration (dates relative to today UTC):
    - `Should_CreateGoal_And_ListWithSummary`: `"  Wakacje  "`, 800 000, deadline current month + 4 → 201, `Location`, body `{ name "Wakacje", target 800 000, saved 0, remaining 800 000, percent 0, monthly 200 000, status active, deadline, accountId null, currency PLN }` (property set equals the contract names); a second goal linked to a savings account; list `page` 1, `pageSize` 100, `total` 2, `summary { saved 0, monthly 0, active 2 }`, creation order.
    - `Should_ValidateDeadlineAndAccount`: deadline previous month → deadlinePast, current + 601 → invalidValue, current month and current + 600 → 201; a card account → savingsAccountOnly; a foreign account and `"acc_x"` → unknownAccount; an archived savings account → archivedAccount.
    - `Should_KeepExistingLinkAndDeadline_When_Updating`: link a savings account, archive it, `PUT` with the same `accountId` → 200; a past stored deadline (`TestData`) kept on `PUT` → 200 with status overdue and monthly null; changing it to the previous month → deadlinePast; `deadline: null` → active.
    - `Should_RebaseOnUpdate` (isolated data, EUR rate today): goal stored in PLN (target 800 000, saved 300 000), base switched to EUR, `PUT` target 200 000 → `target` 200 000, `saved` `round(300 000 × rate(PLN→EUR, today))`, `currency` EUR.
    - `Should_Return404_When_GoalBelongsToAnotherUser` for PUT and DELETE; `Should_DeleteGoal`: 204 then 404.
    - `Should_CountLinkedGoals`: two goals on one savings account → `linkedGoals` 2, other accounts 0.
    - `Should_UnlinkGoals_When_AccountDeleted`: an unused savings account with two linked goals → `DELETE /v1/accounts/{id}` 204, both goals have `accountId` null.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: goals with deadlines, linked savings accounts and a summary`

### Task 8: Goal contributions

**Files:** `Features/Goals/ContributeToGoal/*` (command, endpoint, validator, `ContributionAccountsValidator`, handler, `ContributionAccountRules.cs` shared by the validator and the handler); tests `tests/Mita.Api.UnitTests/Features/Goals/ContributeToGoalValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Goals/ContributionsTests.cs`.

**Interfaces:**
- Consumes: Task 7 (`GoalView`, `GoalQueries`, `GoalDto`), `Transfer.Create`, `RateTable.ResolveAsync`, `RateTable.PlnValuesAsync`, `ManualRate`, `JsonValues`, `UtcCalendar`, `MinorUnits`.
- Produces:
  - `ContributeToGoalCommand(Guid Id, JsonElement? Amount, JsonElement? Date, JsonElement? FromAccountId, JsonElement? Rate) : IInvalidatesCache` (tags `accounts`, `transfers`). `POST /v1/goals/{id:guid}/contributions` answers 200 `GoalDto`, 404 for an unknown or foreign goal (after validation), 409 `goal.concurrentUpdate`, 503 `rates.unavailable`.
  - `ContributionAccountRules.Errors(Guid? linkedId, bool linkedArchived, Guid? fromId, bool fromPresent, IReadOnlyDictionary<Guid, bool> archivedById) : Dictionary<string, string>` (the Decisions rules).
  - Handler, per Decisions: a missing rate for the transfer or for valuing `toAmount` into the base → 503 and nothing written; `GoalContribution` stored with `TransferId`.
- [ ] **Step 1: Failing tests**
  - Validator rows: `{}` → amount and date required; amount 0 → positiveAmount, 1.5 and 100 000 000 001 → invalidValue; date `2026-02-30` → invalidValue, today + 1 year + 1 day → dateTooFar; rate −1, 0, `"4"`, 1 000 001 → invalidValue; fromAccountId `"acc_x"` → unknownAccount.
  - `Should_AddUnlinkedContribution_WithoutMovingMoney`: goal 800 000, deadline current + 4; contribution 120 000 today from a card → `saved` 120 000, `remaining` 680 000, `percent` 15, `monthly` 170 000; balances and the transfer count unchanged; without `fromAccountId` → 200.
  - `Should_ValidateContributionAccounts`: foreign `fromAccountId` → unknownAccount; linked goal without `fromAccountId` → required, with the linked account → sameAccount; linked account archived → `accountId: archivedAccount`; archived source → archivedAccount; unknown goal with a valid body → 404, with `{}` → 400.
  - `Should_TransferIntoLinkedAccount`: linked PLN savings, 50 000 from a PLN card → `saved` 50 000, `percent` 2; the card balance −50 000, the savings +50 000; the newest transfer `{ fromAmount 50 000, toAmount 50 000, rate 1, note: goal name }`.
  - `Should_ConvertCrossCurrencyContribution` (isolated data, the only EUR rate is 4.2717 three days ago, every contribution dated three days ago): 10 000 from an EUR account into a PLN savings → `saved` 42 717; with `rate: 4` → `saved` 42 717 + 40 000 and the transfer `{ toAmount 40 000, rate 4 }`; a second goal on an EUR savings, 43 000 from a PLN card with `rate: 0.25` → savings balance 10 750 and `saved` 45 921 (`round(10 750 × 4.2717)`).
  - `Should_Return503AndWriteNothing_When_RateMissing` (isolated data, no rates): EUR source into a PLN savings without `rate` → 503 `rates.unavailable`; no transfer, no contribution, `saved` unchanged.
  - `Should_PassTarget_And_ReportReached`: target 10 000, 15 000 → `remaining` 0, `percent` 150, reached, `monthly` null; list `summary.active` 0.
  - `Should_AverageLastThreeMonths`: contributions 30 000 today, 60 000 forty days ago (linked), 90 000 exactly three months ago, 12 000 in five days → `summary { saved 192 000, monthly 30 000, active 2 }`.
  - `Should_KeepTransfers_When_GoalDeleted`: a linked contribution, then `DELETE /v1/goals/{id}` → 204; the transfer and both balances remain.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: goal contributions as transfers into the linked account`

### Task 9: Base currency change with budgets and goals

**Files:** `Features/Settings/UpdateSettings/UpdateSettingsHandler.cs`, new `Features/Settings/UpdateSettings/StoredCurrencyRates.cs`; tests `tests/Mita.Api.IntegrationTests/Features/Settings/BudgetsGoalsCurrencyTests.cs`.

**Interfaces:**
- Consumes: `pln_value`, `TransactionBaseAmounts.RecomputeAsync`.
- Produces: `StoredCurrencyRates.CoverAsync(this AppDbContext db, Guid userId, Currency newBase, CancellationToken ct) : Task<bool>`: false when the Decisions condition holds (one SQL statement over `budgets."Currency"`, `goals."Currency"`, `goal_contributions."BaseCurrency"` with `pln_value(currency, CURRENT_DATE) IS NULL`). The handler calls it before `RecomputeAsync`; false → 503 and nothing saved.
- [ ] **Step 1: Failing tests** (isolated data, EUR and USD rates on today and on the current period `From`)
  - `Should_ConvertBudgetsAndGoals_When_BaseCurrencyChanges`: budget 100 000 PLN with an expense of 40 000; goal target 800 000 with a contribution of 300 000 twenty days ago; switch to EUR: budget `limit` `round(100 000 × rate(PLN→EUR, From))`, `spent` equals the transaction's new `baseAmount`; goal `target` and `saved` at today's rate, `summary.monthly` `round(round(300 000 × rate(PLN→EUR, date)) / 3)`; switch to USD and back to PLN: limit and target equal the stored PLN values (no drift).
  - `Should_Return503_When_BudgetCurrencyHasNoRate` (isolated data, no rates; one PLN budget): switching to EUR → 503 `rates.unavailable`; settings unchanged.
  - `Should_Return503_When_GoalCurrencyHasNoRate`: same with only a goal.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then every `Settings` test.
- [ ] **Step 3: Commit** — `feat: check budget and goal currencies when the base currency changes`

### Task 10: API contract, OpenAPI and the Docker smoke

**Files:** `docs/api-contract.md` (title; endpoint rows; `Budget`, `BudgetInput`, `BudgetPage`, `BudgetCopyInput`, `BudgetCopyResult`, `Goal`, `GoalInput`, `GoalPage`, `GoalContributionInput` types; financial periods and `monthStartDay`; the new codes `expenseCategoryOnly`, `budgetExists`, `nothingToCopy`, `savingsAccountOnly`, `deadlinePast`, and the reuse of `unknownCategory`, `unknownAccount`, `archivedAccount`, `sameAccount`, `positiveAmount`, `dateTooFar`; 409 `goal.concurrentUpdate`; 503 on `GET /v1/budgets`, `GET /v1/goals`, contributions and the extended `PUT /v1/settings` condition; category `inUse` and delete with budgets; `linkedGoals`; caching), `tests/Mita.Api.IntegrationTests/OpenApiTests.cs`.
- [ ] **Step 1: Failing test** — `Should_DescribeBudgetsAndGoals`: the document contains `/v1/budgets`, `/v1/budgets/{id}`, `/v1/budgets/copy`, `/v1/goals`, `/v1/goals/{id}`, `/v1/goals/{id}/contributions` with tags Budgets and Goals.
- [ ] **Step 2: Implement, run, see it pass;** Docker smoke `dock.mita-work/scripts/bg-docker-smoke.sh` (built from `ct-docker-smoke.sh`):
  1. `docker compose up -d --build`; register;
  2. a PLN card with 10 000 and a PLN savings account; an expense of 1 250 in Groceries today;
  3. `POST /v1/budgets` Groceries in the current period with 5 000 → 201; `GET /v1/budgets` has `spent` 1 250, `status` on_track, `summary.planned` 5 000;
  4. `POST /v1/budgets/copy` with the next period → `{ copied: 1 }`;
  5. `POST /v1/goals` linked to the savings account → 201; `GET /v1/accounts` shows `linkedGoals` 1;
  6. contribution 2 000 from the card → `saved` 2 000; the card balance is 10 000 − 1 250 − 2 000 and the savings 2 000;
  7. `DELETE` Groceries → 204; the current budget is on `Other`;
  8. `DELETE` the goal → 204; `docker compose down -v`.
- [ ] **Step 3: Commit** — `docs: api contract for budgets and goals`

### Task 11: Frontend hybrid mode against real budgets and goals

Frontend repo, branch `feature/hybrid-budgets-and-goals` from `origin/develop`.

**Files:** `.env.example` (`NUXT_PUBLIC_API_REAL_PATHS=…,/v1/budgets,/v1/goals`), the project guide in the repository root (section "Running against the backend": ten prefixes are real; overview and reports stay on mocks with cloned demo data and do not see real data yet; step 2 command; step 3 describes the new spec), new `tests/e2e-hybrid/budgets-goals.hybrid.spec.ts`; outside the repo `dock.mita-work/scripts/al-hybrid-smoke.sh` (the same ten prefixes).
- [ ] **Step 1: Write `budgets-goals.hybrid.spec.ts`** (English UI; labels checked against the real components):
  1. register a unique user; add the account "Wallet" (Cash, PLN, 100) and the account "Savings" (Savings, PLN, 0) on `/accounts`;
  2. add an expense of 12.50 "Bread" in "Groceries" from "Wallet" on `/transactions`;
  3. on `/budgets`, add a budget for "Groceries" with 50.00; the card shows the spent 12.50 and the on-track status; edit the limit to 10.00; the card shows over budget;
  4. on `/goals`, add a goal "Bike" with target 500.00 linked to "Savings"; contribute 20.00 from "Wallet"; the card shows 20.00 saved; `/accounts` shows "Savings" with 20.00 and "Wallet" with 67.50;
  5. delete the goal and the budget; `/goals` and `/budgets` show their empty states;
  6. delete the user in Settings.
- [ ] **Step 2: Run** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/scripts/al-hybrid-smoke.sh` (backend branch from Tasks 1-10 checked out); the four specs pass.
- [ ] **Step 3: Run** `bun run lint && bun run typecheck && bun test tests/unit` in the frontend; all green.
- [ ] **Step 4: Commit** — `test: hybrid e2e against real budgets and goals`

### Task 12: Pull requests

- [ ] Backend:
  1. Rebase on `origin/develop` and fold fix commits into their task commits.
  2. Run `dotnet build -warnaserror` and `dotnet test --logger junit` (Docker running), the Docker smoke and the hybrid smoke; all must be green.
  3. Push, open a PR to `develop` (plain description), wait for `check-source-branch`, then `gh pr merge --rebase`.
- [ ] Frontend: same flow for `feature/hybrid-budgets-and-goals` after the backend PR is merged, with lint, typecheck, unit and the full E2E suite green.
