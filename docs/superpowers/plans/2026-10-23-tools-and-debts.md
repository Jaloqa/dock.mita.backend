# Tools Section and Private Debts Implementation Plan

**Goal:** A "Tools" main section (en Tools, pl Narzędzia, ru Инструменты, uk Інструменти) whose first tool is a Debts page: private debts ("I lent Max 500 zł", "I borrowed 2000 zł from mum") with an optional account, partial repayments in any currency, an optional due date with overdue highlight, an "Owed to me / I owe" summary on the Debts page and the Overview, and an Overview balance that can include debts; debts move account balances like transfers and never appear in budgets, reports or income and expense totals.

**Architecture:**
- **Domain:** `Debt` (direction, counterparty, amount and currency, date, optional due date, optional account with its converted amount and rate, note, and a denormalised `Repaid`) and `DebtRepayment` (amount and currency, rate to the debt currency, `DebtAmount`, optional account with its converted amount). Status and overdue are computed, never stored.
- **Persistence:** one migration adds `debts` and `debt_repayments` and replaces the `account_movements` view with two more legs (a debt leg and a repayment leg), so `balance`, `inUse`, `currencyLocked` and `accountInUse` see debts without any account query changing.
- **API:** a `Debts` feature (`/v1/debts`, `/v1/debts/{id}`, `/v1/debts/{id}/repayments`, `/v1/debts/{id}/repayments/{repaymentId}`) with shared pieces in `Common/Debts`; amounts in base currency exist only at read time (summary at today's rate), so a base currency change rewrites nothing. `GET /v1/overview` gains `debts`.
- **Frontend:** a `tools` feature (hub page `/tools`) and a `debts` feature (page `/tools/debts`, debt and repayment dialogs, repayment history); the Overview gets a debts card whose switch makes the balance include debts. The mock backend implements the same rules.

**Tech Stack:** Backend: .NET 10, EF Core migrations (Npgsql), MediatR 12.5.0, FluentValidation, xUnit v3, Shouldly, Moq, Bogus, Testcontainers. Frontend: Nuxt 4, PrimeVue 4 (`Dialog`, `SelectButton`, `Select`, `DatePicker`, `InputNumber`, `ToggleSwitch`), TanStack Query, Vuelidate, bun:test, Playwright 1.64.1. No new packages in either repo.

**Spec:** `dock.mita.frontend/docs/superpowers/specs/2026-10-22-tools-design.md`, sections "Navigation", "Private debts", calls "Debts without an account" and "Debts in reports", Anti-goals "Inflated spending" and "Leaked debts", constraints "Languages", "Contract", "Money", Delivery item 2. Also binding: both repos' project guides and `docs/STACK.md`, backend `docs/api-contract.md`, the previous plan `docs/superpowers/plans/2026-10-22-currency-list.md` (rate rules, 30-day staleness on automatic conversions of writes, never on reads). Code to reuse: backend `Common/{Rates/RateTable,Rates/ManualRate,Transactions/TransactionWriter,Transactions/TransactionPrice,Transactions/TransactionReferenceRules,Goals/*,Wire/JsonValues,Paging}`, `Features/{Transfers,Goals,Overview}`, `Persistence/Migrations/20261019173905_CategoriesTransactions.cs` (the current view); frontend `app/shared/{utils/navigation.ts,components/AppSidebar.vue,components/AppBottomNav.vue,api/invalidation.ts,rates/useRates.ts,money/money.ts}`, `app/features/{goals,overview,transactions/components/TransactionDialog.vue}`, `mocks/{db,ledger,goals,transactions,transfers,rates,overview}.ts`, `mocks/seed/*`.

## Decisions

| Topic | Decision | Cost if wrong |
|---|---|---|
| Tools entries for parts not built yet | Show only what exists: the hub lists Debts alone; parts 3 and 5 add Groups, Savings calculator and Currency converter to `tools` in `app/features/tools/utils/tools.ts` | Users do not see what is coming |
| Navigation | Sidebar item "Tools" after Reports, linking the hub `/tools`; the Debts page is `/tools/debts`; the sidebar item is active on `/tools` and every `/tools/...` page. The mobile bottom bar stays as is (five slots, full); phones reach the hub through a third icon link in the mobile Overview header and the Overview debts card | Tools are less visible on phones |
| Storage | Separate tables `debts` and `debt_repayments`, not special transactions; their account legs join the `account_movements` view like transfers | The account history on the Accounts page (transfers list) does not show debt movements; they are listed on the Debts page |
| Direction | `lent` (money left me, "Owed to me") and `borrowed` (money came to me, "I owe"). Lent leg: `-AccountAmount`; borrowed leg: `+AccountAmount`; a repayment of a lent debt `+AccountAmount`, of a borrowed debt `-AccountAmount` | None |
| Counterparty | Free text, 1 to 40 characters after trimming, no control characters; not a contact entity | "Max" and "max" are two people; no per-person totals |
| Debt currency and account | Any of the 148 currencies; account optional (spec call). With an account, `accountAmount` is the amount converted like a transaction: rate 1 for the same currency, else the manual `rate` (debt currency → account currency) or the stored rate of `date`, under the 30-day staleness rule (503 `rates.unavailable`). Without an account `accountAmount` and `rate` are `null` and a sent `rate` is validated, then ignored | None |
| Repayment conversion | `rate` = repayment currency → debt currency: 1 for the same currency, else the manual `rate` or the stored rate of the repayment `date` (staleness applies). `debtAmount` = amount converted with it. The optional account receives (lent) or pays (borrowed) `accountAmount`: the amount when the account is in the repayment currency, `debtAmount` when it is in the debt currency, otherwise the amount converted at the stored rate of the date (no manual override, staleness applies) | A repayment into an account in a third currency cannot take a manual rate |
| Over-repayment | Refused: `debtAmount` above the remaining amount answers 400 `amount: ["validation.exceedsRemaining"]` (also on a settled debt). Repaying exactly the remaining amount settles the debt | A friend who paid a little more cannot be recorded exactly |
| Converted amounts | `debtAmount` or `accountAmount` that rounds to 0 → `rate: ["validation.positiveAmount"]`; above 100 000 000 000 minor units → `rate: ["validation.invalidValue"]` (as transactions) | None |
| Editing | `PUT /v1/debts/{id}` replaces every field and re-prices the account amount like `PUT /v1/transactions` (the dialog resends the stored rate as the manual rate). The currency cannot change once a repayment exists (`currency: ["validation.debtCurrencyLocked"]`); `amount` cannot go below `repaid` (`amount: ["validation.belowRepaid"]`). `PUT` may keep an account that was archived since. Repayments are never edited: delete and add again | None |
| Deleting | `DELETE /v1/debts/{id}` deletes the debt and its repayments (cascade) and so restores every balance; `DELETE /v1/debts/{id}/repayments/{repaymentId}` restores the debt and the balance | None |
| Due date | Optional, a real date not before `date` (`dueDate: ["validation.dueBeforeDate"]`, checked only when `date` is valid). Overdue = open and `dueDate` before today (UTC); due today is not overdue. Repayment dates are not compared with the debt date | None |
| Status | `open` while `remaining > 0`, `settled` at 0. Not stored | None |
| Summary | Over open debts only: `owedToMe` (lent) and `owedByMe` (borrowed) are the remaining amounts summed per currency, each sum converted into the base currency at today's latest stored rate whatever its age (a read) and rounded half away from zero, then added; `open` counts open debts, `overdue` overdue ones. The same object is `DebtPage.summary` and `Overview.debts`. When a needed currency has no stored rate at all the answer is 503 `rates.unavailable`, as the overview balance does | None |
| Base currency change | Nothing to rewrite (no base amounts are stored); `PUT /v1/settings` does not check debt currencies, as it does not check account currencies | A base change to a currency with no stored rate can make `GET /v1/debts` answer 503 until the sync runs |
| Lists | `GET /v1/debts?status&page&pageSize`: `status` `open` (default) or `settled`, case-sensitive; `pageSize` default and maximum 100. Open: `dueDate` ascending with no due date last, then `date` descending, creation time descending, `id` descending. Settled: `date`, creation time and `id` descending. `GET /v1/debts/{id}/repayments`: newest first (`date`, creation time, `id`), `pageSize` default and maximum 100. The page loads the first 100 of each | More than 100 open or settled debts: only the first 100 are shown |
| Repayment answer | `POST /v1/debts/{id}/repayments` answers 200 with the updated `Debt`, no `Location` (as goal contributions) | None |
| Privacy | Every query filters by the current user; a foreign debt or repayment id answers exactly like an unknown one (404); a foreign account id is `validation.unknownAccount` | None (anti-goal "Leaked debts") |
| Reports and totals | Debts are not transactions, so reports, budgets, overview income/expense, spending, recent transactions and `hasTransactions` never see them (pinned by tests) | None (anti-goal "Inflated spending") |
| Balance with debts | The client computes `balance.amount + debts.owedToMe - debts.owedByMe`. A switch "Include debts in the balance" in the Overview debts card (shown when `open > 0`) swaps the balance figures of the desktop stat card and the mobile balance card to it, labelled "Balance with debts". Remembered per browser in `localStorage['mita.balanceWithDebts']` (`'1'`), default off, read on mount, every access in try/catch | The choice does not follow the user across devices |
| Overview layout | Desktop: the debts card is the fourth card of the bottom row, whose minimum column width drops from 300px to 250px. Mobile: the debts card follows the balance card | One card wraps alone on screens narrower than about 1350px |
| Groups on the Debts page (part 4) | The page has the summary cards, then a section "Personal debts" (`aria-labelledby`); part 4 adds a "Groups" section after it and decides whether the summary includes group balances | None |
| Mock data | Anna gets four debts (one overdue, one settled, one in EUR), Oleh one; the seed lowers the opening balances by the seeded debt legs so every designed balance stays (Konto główne 8,820.40, Gotówka 940.00, Oszczędności 14,000.00 zł); `MockState.version` becomes 3 | Developer mock data resets once |
| Real smoke | `dock.mita-work/scripts/db-real-smoke.sh`: a copy of `cl-real-smoke.sh` with its own compose projects (`dock-mita-backend-debts-smoke`, `dock-mita-frontend-debts-smoke`, image `dock-mita-frontend-debts-smoke`), API 18080, web 3100 | None |

## Global Constraints

- Each repo's project guide and `docs/STACK.md` are binding. No new packages. MediatR stays 12.5.0.
- Backend: warnings are errors; `Api → Infrastructure → Domain`; a slice never references another slice's internals (share through `Common/Debts`); records, `sealed`, file-scoped namespaces, no comments; `CancellationToken` everywhere; first failing rule per field; validation (400) before lookup (404); ids in paths are GUIDs (anything else 404); never edit an applied migration; tests `Should_X_When_Y`; tests never call the real NBP API; tests that seed rates use `factory.WithIsolatedData()` and dates relative to today (the app clock is the real UTC clock).
- Frontend: Biome clean, `bun run typecheck` 0 `error TS`, `bun test tests/unit` green, no `any`, every string through i18n in `en`, `pl`, `ru`, `uk` (`tests/unit/i18n/locales.test.ts` keeps the key sets equal), inputs labelled, keyboard reachable, touch targets ≥ 44px, money through `useMoney().format` with the debt or account currency, amounts typed with `currencyDigits` fraction digits.
- Contract, byte for byte between backend and mocks: the types and codes of the "Debts" section written in Task 5; `AMOUNT_MINOR_MAX = 100_000_000_000`; `MANUAL_RATE_MAX = 1_000_000_000`; counterparty ≤ 40, note ≤ 200; rate rounding and the 30-day staleness as in the currency-list plan.
- New validation codes (all four locales get a message): `validation.dueBeforeDate`, `validation.exceedsRemaining`, `validation.belowRepaid`, `validation.debtCurrencyLocked`.
- Commits: Conventional Commits, English; no tool attribution, no `Co-Authored-By`, no "Generated with" lines in commits, pull requests, code or committed files.

## Review Focus

- A lent debt and its repayments on one account must net to the right balance, and a borrowed one the opposite way: lend 3 000 from an account of 10 000 (7 000), get 1 000 back (8 000), borrow 5 000 into it (13 000), repay 2 000 of that (11 000). Pinned in Task 1 (`Should_IncludeDebtLegsInBalance`) and Task 6 (mock ledger row).
- A 50 € repayment of a złoty debt at 4.2563 must reduce it by 212.82 zł (21282, half away from zero), put 50 € into a EUR account and 212.82 zł into a złoty account; an account in a third currency gets the date's cross rate. Pinned in Task 3 (`RepaymentPriceTests`, `Should_ConvertRepayment_When_CurrencyDiffers`) and Task 6.
- Repaying one minor unit more than the remaining amount must be refused and write nothing; exactly the remaining amount settles the debt and clears overdue. Pinned in Task 3 (`Should_Return400_When_RepaymentExceedsRemaining`, `Should_SettleDebt_When_RepaidInFull`) and Task 9 (E2E).
- Lending, borrowing and repaying must never show up as income or expense: overview income/expense/count/`hasTransactions`, reports totals and structure stay 0 after debts with accounts and repayments. Pinned in Task 5 (`Should_NotCountDebts_InTotals`, `Should_IgnoreDebts_InReports`).
- Another user's debt, repayment or account must be unreachable: 404 for every debt route with a foreign id, `unknownAccount` for a foreign account, and lists and summaries never include foreign rows. Pinned in Task 2 (`Should_IsolateUsers`), Task 3 (`Should_Return404_When_DebtBelongsToAnotherUser`) and Task 4.

---

## File map

```
Backend (dock.mita.backend, branch feature/debts)
src/Mita.Domain/Debts/{Debt,DebtRepayment,DebtDirection,DebtStatus,DebtDetails,RepaymentDetails}.cs
src/Mita.Infrastructure/Persistence/{AppDbContext.cs, Configurations/{DebtConfiguration,DebtRepaymentConfiguration}.cs, Migrations/<timestamp>_Debts.cs}
src/Mita.Api/Common/Debts/{DebtDto,DebtSummaryDto,DebtWire,IDebtInput,DebtInputParser,DebtInputValidator,DebtReferencesValidator,DebtPrice,DebtWriter,DebtQueries,DebtSummaries}.cs
src/Mita.Api/Features/Debts/{DebtsEndpoints.cs, ListDebts/*, CreateDebt/*, UpdateDebt/*, DeleteDebt/*, AddRepayment/*, ListRepayments/*, DeleteRepayment/*}
src/Mita.Api/Features/Overview/GetOverview/{GetOverviewHandler,OverviewResponse}.cs, src/Mita.Api/Program.cs, src/Mita.Api/Extensions/MediatorExtensions.cs
docs/api-contract.md
tests/Mita.Api.UnitTests/{Debts/DebtTests.cs, Features/Debts/{CreateDebtValidatorTests,ListDebtsValidatorTests,AddRepaymentValidatorTests,DebtPriceTests,RepaymentPriceTests}.cs}
tests/Mita.Api.IntegrationTests/{Infrastructure/TestData.cs, Persistence/{MigrationTests,LedgerTests}.cs, Features/Debts/{DebtsTests,RepaymentsTests,UpdateDeleteDebtTests}.cs, Features/Accounts/UpdateDeleteAccountTests.cs, Features/Users/MeTests.cs, Features/Overview/OverviewTests.cs, Features/Reports/ReportsTests.cs, OpenApiTests.cs}

Frontend (dock.mita.frontend, branch feature/debts)
app/shared/api/{contract,invalidation}.ts, app/shared/utils/navigation.ts, app/shared/types/icon.ts, app/shared/components/{AppIcon,AppSidebar}.vue
app/features/tools/{utils/tools.ts, components/ToolCard.vue}
app/features/debts/{api/{debtsApi,keys}.ts, composables/{useDebts,useDebtMutations,useRepayments}.ts, components/{DebtSummaryCards,DebtCard,DebtDialog,RepaymentDialog,RepaymentList}.vue, schemas/{debtSchema,repaymentSchema}.ts, utils/debtView.ts}
app/features/overview/{components/{OverviewDebts,OverviewStats,OverviewBalanceCompact}.vue, composables/useBalanceWithDebts.ts, utils/overviewView.ts}
app/pages/{index.vue, tools/index.vue, tools/debts.vue}
mocks/{db,ledger,debts,overview}.ts, mocks/handlers/{debts,index}.ts, mocks/seed/{debts,index}.ts
i18n/locales/{en,pl,ru,uk}.json
tests/unit/{mocks/{debts,seedDebts,overview,reports,db}.test.ts, features/debts/{debtSchema,repaymentSchema,debtView}.test.ts, features/overview/overviewView.test.ts, shared/invalidation.test.ts}
tests/e2e/{tools,debts,overview}.spec.ts, tests/e2e-real/{debts.real,navigation.real}.spec.ts
project guide in the repository root ("Real-backend suite")
```

---

### Task 1: Debt records and their account legs

Backend repo, branch `feature/debts`.

**Files:** new `Mita.Domain/Debts/{Debt,DebtRepayment,DebtDirection,DebtStatus,DebtDetails,RepaymentDetails}.cs`; new `Persistence/Configurations/{DebtConfiguration,DebtRepaymentConfiguration}.cs`; `Persistence/AppDbContext.cs`; migration `Debts`; tests new `tests/Mita.Api.UnitTests/Debts/DebtTests.cs`, `tests/Mita.Api.IntegrationTests/Infrastructure/TestData.cs`, `Persistence/{MigrationTests,LedgerTests}.cs`, `Features/Accounts/UpdateDeleteAccountTests.cs`, `Features/Users/MeTests.cs`.

**Interfaces:**
- Produces:
  - `enum DebtDirection { Lent, Borrowed }`, `enum DebtStatus { Open, Settled }`.
  - `sealed record DebtDetails(DebtDirection Direction, string Counterparty, long Amount, Currency Currency, DateOnly Date, DateOnly? DueDate, Guid? AccountId, long? AccountAmount, decimal? Rate, string? Note)`.
  - `sealed class Debt : IAuditable` with private setters for `Id, UserId, Direction, Counterparty, Amount, Currency, Date, DueDate, AccountId, AccountAmount, Rate, Note, Repaid` and `CreatedAt/UpdatedAt`; `const int CounterpartyMaxLength = 40`, `NoteMaxLength = 200`; `long Remaining => Amount - Repaid`; `DebtStatus Status => Remaining == 0 ? Settled : Open`; `bool IsOverdue(DateOnly today) => Remaining > 0 && DueDate < today`; `static Debt Create(Guid userId, DebtDetails details)` (id `Guid.CreateVersion7()`); `void Update(DebtDetails details)`; `void Repay(long debtAmount)`; `void Unrepay(long debtAmount)`. `Create`/`Update` throw `ArgumentException` when `Amount <= 0`, `DueDate < Date`, `AccountId`, `AccountAmount` and `Rate` are not all null or all set, `Amount < Repaid`, or the currency changes while `Repaid > 0`. `Repay` throws `InvalidOperationException` for `debtAmount <= 0` or `> Remaining`; `Unrepay` for `<= 0` or `> Repaid`.
  - `sealed record RepaymentDetails(DateOnly Date, long Amount, Currency Currency, decimal Rate, long DebtAmount, Guid? AccountId, long? AccountAmount, string? Note)`; `sealed class DebtRepayment : IAuditable` (`Id, UserId, DebtId` + the details' fields, `CreatedAt/UpdatedAt`), `static DebtRepayment Create(Guid userId, Guid debtId, RepaymentDetails details)`.
  - `AppDbContext.Debts`, `AppDbContext.DebtRepayments`.
  - Configurations: tables `debts` and `debt_repayments`; `Direction` and every `Currency` as strings (max 10 and 3); `Counterparty` max 40 required, `Note` max 200; `Rate` `HasPrecision(24, 12)`; check constraints `CK_debts_amount_positive` (`"Amount" > 0`), `CK_debts_repaid_range` (`"Repaid" >= 0 AND "Repaid" <= "Amount"`), `CK_debts_account_fields` (`("AccountId" IS NULL) = ("AccountAmount" IS NULL) AND ("AccountId" IS NULL) = ("Rate" IS NULL)`), `CK_debts_account_amount_positive` (`"AccountAmount" > 0`), `CK_debts_due_after_date` (`"DueDate" >= "Date"`), `CK_debt_repayments_amount_positive`, `CK_debt_repayments_debt_amount_positive`, `CK_debt_repayments_account_fields`, `CK_debt_repayments_account_amount_positive`; indexes `debts (UserId, Date)`, `debts (AccountId)`, `debt_repayments (DebtId)`, `debt_repayments (AccountId)`; FKs: `UserId` → users cascade (both), `DebtId` → debts cascade, `AccountId` → accounts `NoAction` (both).
  - Migration `Debts` (`dotnet ef migrations add Debts ...`), plus by hand at the end of `Up`:
    ```sql
    CREATE OR REPLACE VIEW account_movements AS
    SELECT "UserId", "FromAccountId" AS "AccountId", -"FromAmount" AS "Amount" FROM transfers
    UNION ALL
    SELECT "UserId", "ToAccountId", "ToAmount" FROM transfers
    UNION ALL
    SELECT "UserId", "AccountId", CASE WHEN "Kind" = 'Income' THEN "AccountAmount" ELSE -"AccountAmount" END
    FROM transactions
    UNION ALL
    SELECT "UserId", "AccountId", CASE WHEN "Direction" = 'Borrowed' THEN "AccountAmount" ELSE -"AccountAmount" END
    FROM debts WHERE "AccountId" IS NOT NULL
    UNION ALL
    SELECT r."UserId", r."AccountId", CASE WHEN d."Direction" = 'Lent' THEN r."AccountAmount" ELSE -r."AccountAmount" END
    FROM debt_repayments r JOIN debts d ON d."Id" = r."DebtId" WHERE r."AccountId" IS NOT NULL;
    ```
    and at the start of `Down` the three-branch view of `CategoriesTransactions` before the tables are dropped.
  - `TestData.AddDebtAsync(WebApplicationFactory<Program> factory, Guid userId, DebtDirection direction, long amount, Currency currency = Currency.PLN, Account? account = null, string date = "2026-10-01", string? dueDate = null) : Task<Debt>` (account amount = amount at rate 1 for the same currency, rate 2 otherwise, as `AddTransferAsync`); `TestData.AddRepaymentAsync(WebApplicationFactory<Program> factory, Debt debt, long amount, Account? account = null, string date = "2026-10-05") : Task<DebtRepayment>` (in the debt currency, rate 1; account amount likewise; updates the stored debt's `Repaid` in the same save).
- [ ] **Step 1: Failing tests**
  - `DebtTests`: a new debt of 500 has `Remaining` 500 and `Status` Open; `Repay(300)` → 200; `Repay(201)` then throws; `Repay(0)` throws; `Repay(200)` → Settled; `Unrepay(300)` → 300 again; `Unrepay(301)` throws; `Update` with `Amount` 299 after `Repay(300)` throws; `Update` with another currency after a repayment throws and without one succeeds; `Create` with `DueDate` the day before `Date` throws; with `AccountId` but no `AccountAmount` throws. `IsOverdue` rows (today 2026-10-23): due 2026-10-22 open → true; due 2026-10-23 → false; due 2026-10-22 settled → false; no due date → false.
  - `LedgerTests.Should_IncludeDebtLegsInBalance`: PLN account opening 10 000; lent 3 000 on it → `balance` 7 000 and `inUse` true; repayment 1 000 into it → 8 000; borrowed 5 000 into it → 13 000; repayment 2 000 of the borrowed debt from it → 11 000; a lent debt of 999 without an account and its repayment of 500 without an account → still 11 000.
  - `LedgerTests.Should_DropDebtLegs_When_DebtDeleted`: deleting the lent debt row from the context removes its repayments (cascade) and the balance loses both legs.
  - `MigrationTests.Should_ApplyMigrations_When_DatabaseIsEmpty` (extended): tables `debts` and `debt_repayments` exist.
  - `UpdateDeleteAccountTests.Should_RefuseDelete_When_AccountHasDebtLegs`: an account used only by a debt → `DELETE /v1/accounts/{id}` 400 `id: ["validation.accountInUse"]`; another account used only by a repayment of a debt without an account → the same; `PUT` with another currency on either → `currency: ["validation.currencyLocked"]`.
  - `MeTests.Should_DeleteUser_When_DebtsUseAccounts`: a user with a lent debt on an account and a repayment into another account → `DELETE /v1/me` 204 and no `debts` or `debt_repayments` row of that user is left.
- [ ] **Step 2: Run, see them fail.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/be-run.sh 'dotnet test --filter "FullyQualifiedName~DebtTests|FullyQualifiedName~LedgerTests|FullyQualifiedName~MigrationTests|FullyQualifiedName~UpdateDeleteAccountTests|FullyQualifiedName~MeTests"'`
- [ ] **Step 3: Implement, add the migration, run, see them pass,** then the full backend suite.
- [ ] **Step 4: Commit** — `feat: debt records with account legs in the ledger`

### Task 2: Create and list debts

**Files:** new `Common/Debts/{DebtDto,DebtSummaryDto,DebtWire,IDebtInput,DebtInputParser,DebtInputValidator,DebtReferencesValidator,DebtPrice,DebtWriter,DebtQueries,DebtSummaries}.cs`, new `Features/Debts/{DebtsEndpoints.cs, CreateDebt/{CreateDebtCommand,CreateDebtEndpoint,CreateDebtHandler,CreateDebtValidator,CreateDebtReferencesValidator}.cs, ListDebts/{ListDebtsQuery,ListDebtsEndpoint,ListDebtsHandler,ListDebtsValidator,DebtPageResponse}.cs}`, `Program.cs`, `Extensions/MediatorExtensions.cs`; tests new `tests/Mita.Api.UnitTests/Features/Debts/{CreateDebtValidatorTests,ListDebtsValidatorTests,DebtPriceTests}.cs`, new `tests/Mita.Api.IntegrationTests/Features/Debts/DebtsTests.cs`, `OpenApiTests.cs`.

**Interfaces:**
- Consumes: Task 1 domain; `RateTable.FreshPlnValuesAsync`, `RateTable.PlnValuesAsync`, `RateTable.Rate`, `ExchangeRate.Round`, `MinorUnits.Convert/MaxAmount`, `ManualRate.TryParse`, `TransactionReferenceRules.AccountError`, `JsonValues`, `PageRequest`/`ToPageAsync`, `SettingsAsync`, `UtcCalendar`, `AccountCacheTags.Accounts`.
- Produces:
  - `sealed record DebtDto(Guid Id, string Direction, string Counterparty, long Amount, string Currency, long Repaid, long Remaining, string Status, bool Overdue, string Date, string? DueDate, Guid? AccountId, long? AccountAmount, double? Rate, string? Note, int Repayments)`.
  - `sealed record DebtSummaryDto(long OwedToMe, long OwedByMe, int Open, int Overdue)`.
  - `DebtWire`: `ToWire(DebtDirection)` → `lent`/`borrowed`, `ToWire(DebtStatus)` → `open`/`settled`, `TryParseDirection(JsonElement?, out DebtDirection)`, `TryParseStatus(string?, out DebtStatus)` (exact lower-case).
  - `IDebtInput { JsonElement? Direction, Counterparty, Amount, Currency, Date, DueDate, AccountId, Rate, Note; Guid? ExistingId }`; `abstract class DebtInputValidator<T> : AbstractValidator<T> where T : IDebtInput` (cascade stop per field): `direction` required → invalidValue; `counterparty` required → invalidValue (not a string or a control character) → nameTooLong (> 40 after trimming); `amount` required → invalidValue (not a safe integer or > `MinorUnits.MaxAmount`) → positiveAmount; `currency` required → invalidValue; `date` required → invalidValue → dateTooFar (> today + 1 year); `dueDate` when present: invalidValue → dueBeforeDate (only when `date` parsed); `accountId` when present: not a GUID → unknownAccount; `rate` when present: `ManualRate.TryParse` → invalidValue; `note`: not a string or control character → invalidValue, > 200 after trimming → noteTooLong.
  - `sealed record ParsedDebt(DebtDirection Direction, string Counterparty, long Amount, Currency Currency, DateOnly Date, DateOnly? DueDate, Guid? AccountId, decimal? ManualRate, string? Note)`; `DebtInputParser.Parse(IDebtInput) : ParsedDebt` (throws `InvalidOperationException` for unvalidated input; blank note → null).
  - `abstract class DebtReferencesValidator<T>(AppDbContext db, ICurrentUser currentUser)`: an `accountId` that is a GUID must be an account of the user (`unknownAccount`) and not archived (`archivedAccount`) unless `ExistingId`'s debt already has that account.
  - `sealed record DebtPrice(decimal? Rate, long? AccountAmount)`; `static DebtPrice? Compute(long amount, Currency currency, Currency? accountCurrency, decimal? manualRate, IReadOnlyDictionary<Currency, decimal> plnValues)`: no account → `(null, null)`; same currency → `(1, amount)`; else manual rate rounded by `ExchangeRate.Round`, or `RateTable.Rate`; `null` when the rate is missing.
  - `sealed class DebtWriter(AppDbContext db, TimeProvider time)` registered scoped like `TransactionWriter`: `Task<Result<DebtDetails>> PrepareAsync(Guid userId, ParsedDebt input, Guid? existingAccountId, CancellationToken ct)` — locks the account `FOR SHARE` (`TransactionWriter` style), account errors under `accountId`, `FreshPlnValuesAsync(input.Date, today)`, `DebtPrice.Compute`, missing rate → `Error.Unavailable("rates.unavailable")`, account amount ≤ 0 → `rate: positiveAmount`, > max → `rate: invalidValue`.
  - `DebtQueries.Project(this IQueryable<Debt> debts, AppDbContext db, DateOnly today) : IQueryable<DebtDto>` (`Repayments` = count of the debt's repayments, `Overdue` = `Repaid < Amount && DueDate < today`).
  - `DebtSummaries.SummarizeAsync(this AppDbContext db, Guid userId, Currency baseCurrency, DateOnly today, CancellationToken ct) : Task<DebtSummaryDto?>` — one grouped query over open debts by `(Direction, Currency)` (sum of `Amount - Repaid`, count, overdue count), `PlnValuesAsync(today)` only when a currency differs from the base, each sum converted with `MinorUnits.Convert`, `null` when a rate is missing.
  - `ListDebtsQuery(string? Status, int Page, int PageSize) : IRequest<Result<DebtPageResponse>>`; validator: paging rules, `status` when not null must parse (`validation.invalidValue`); `DebtPageResponse(IReadOnlyList<DebtDto> Items, int Page, int PageSize, int Total, string Currency, DebtSummaryDto Summary)`; endpoint `GET /v1/debts` with `string? status = null, int page = 1, int pageSize = 100`; ordering as in Decisions; not cached.
  - `CreateDebtRequest`/`CreateDebtCommand(JsonElement? Direction, Counterparty, Amount, Currency, Date, DueDate, AccountId, Rate, Note) : IRequest<Result<DebtDto>>, IDebtInput, IInvalidatesCache` (`ExistingId` null, invalidates `AccountCacheTags.Accounts`); `POST /v1/debts` → 201 `Location: /v1/debts/{id}`.
  - `DebtsEndpoints.MapDebtsEndpoints(this IEndpointRouteBuilder app)`: `MapV1().WithTags("Debts")`, every route `RequireAuthorization(AuthExtensions.UserPolicy)`; `Program.cs` maps it after goals.
- [ ] **Step 1: Failing tests**
  - `DebtPriceTests`: `(5000, EUR, PLN, null, {EUR 4.2563})` → `Rate 4.2563, AccountAmount 21282`; `(5000, EUR, PLN, 4.5, …)` → `22500`; `(1500, JPY, PLN, null, {JPY 0.024764})` → `3715`; `(700, PLN, PLN, 9, …)` → `(1, 700)`; `(700, PLN, null, …)` → `(null, null)`; missing EUR → `null`.
  - `CreateDebtValidatorTests` (one row per rule above, in order): `direction` missing → required, `"gift"` → invalidValue; `counterparty` `"   "` → required, `"Ma\u0007x"` → invalidValue, 41 letters → nameTooLong, 40 → valid; `amount` 0 → positiveAmount, 1.5 → invalidValue, 100000000001 → invalidValue; `currency` `"XDR"` → invalidValue; `date` `"2026-02-30"` → invalidValue, today + 1 year + 1 day → dateTooFar; `dueDate` `"x"` → invalidValue, the day before `date` → dueBeforeDate, equal to `date` → valid; `accountId` `"1"` → unknownAccount; `rate` 0 → invalidValue; `note` of 201 characters → noteTooLong.
  - `ListDebtsValidatorTests`: `status` `"open"`, `"settled"`, null valid; `"Open"`, `"all"` → invalidValue; pageSize 101 → invalidValue.
  - `DebtsTests` (`WithIsolatedData` where rates are seeded):
    - `Should_CreateLentDebt_When_AccountGiven`: PLN account opening 100 000; `POST` lent, `"  Max "`, 50 000 PLN, today, due today + 10 → 201, `Location` `/v1/debts/{id}`, body `direction "lent"`, `counterparty "Max"`, `repaid 0`, `remaining 50000`, `status "open"`, `overdue false`, `accountAmount 50000`, `rate 1`, `repayments 0`, `note null`; the account `balance` 50 000.
    - `Should_CreateBorrowedDebt_When_AccountGiven`: borrowed 20 000 into it → balance 120 000.
    - `Should_KeepBalances_When_DebtHasNoAccount`: `accountId`, `accountAmount`, `rate` null; balances unchanged; a sent `rate` 3 is ignored.
    - `Should_PriceDebt_When_AccountCurrencyDiffers`: EUR 4.2563 on the date; lent 5 000 EUR from a PLN account → `rate 4.2563`, `accountAmount 21282`, balance −21 282; with `rate` 4.5 → 22 500.
    - `Should_Return503_When_DebtRateIsStale`: EUR stored 40 days before the date only → 503 `rates.unavailable`, nothing written; with `rate` 4.5 → 201.
    - `Should_RefuseAccount_When_UnknownForeignOrArchived`: another user's account and a random GUID → `accountId: ["validation.unknownAccount"]`; an archived own account → `archivedAccount`.
    - `Should_ListOpenDebts_WithSummary`: base PLN, EUR 4.30 today; lent 40 000 PLN due today + 5; lent 5 000 EUR without due date; borrowed 150 000 PLN due yesterday; a lent 12 000 PLN debt repaid in full → `GET /v1/debts` items borrowed, lent PLN, lent EUR (that order); borrowed `overdue true`; `total 3`; `currency "PLN"`; `summary { owedToMe 61500, owedByMe 150000, open 3, overdue 1 }`; `?status=settled` → the settled debt only, `status "settled"`, `remaining 0`, `overdue false`, same summary.
    - `Should_Return503_When_SummaryNeedsMissingRate`: an open KWD debt and no KWD rate → `GET /v1/debts` 503; once settled (repaid in full) → 200.
    - `Should_IsolateUsers`: user B's list is empty with a zero summary while user A has debts.
  - `OpenApiTests.Should_DescribeDebts`: paths `/v1/debts` (get, post) tagged `Debts`, `get` parameters contain `status`.
- [ ] **Step 2: Run, see them fail.** `be-run.sh 'dotnet test --filter "FullyQualifiedName~Debt|FullyQualifiedName~OpenApiTests"'`
- [ ] **Step 3: Implement, run, see them pass,** then the full backend suite.
- [ ] **Step 4: Commit** — `feat: create and list private debts`

### Task 3: Repayments

**Files:** new `Features/Debts/AddRepayment/{AddRepaymentCommand,AddRepaymentEndpoint,AddRepaymentHandler,AddRepaymentValidator,AddRepaymentReferencesValidator,RepaymentPrice}.cs`, `ListRepayments/{ListRepaymentsQuery,ListRepaymentsEndpoint,ListRepaymentsHandler,ListRepaymentsValidator,RepaymentResponse}.cs`, `DeleteRepayment/{DeleteRepaymentCommand,DeleteRepaymentEndpoint,DeleteRepaymentHandler}.cs`, `DebtsEndpoints.cs`; tests new `tests/Mita.Api.UnitTests/Features/Debts/{AddRepaymentValidatorTests,RepaymentPriceTests}.cs`, new `tests/Mita.Api.IntegrationTests/Features/Debts/RepaymentsTests.cs`, `OpenApiTests.cs`.

**Interfaces:**
- Consumes: Tasks 1-2 (`Debt.Repay/Unrepay`, `DebtQueries.Project`, `DebtWire`, `DebtReferencesValidator` rules for the account, `RateTable`, `ManualRate`).
- Produces:
  - `sealed record RepaymentPrice(decimal Rate, long DebtAmount, long? AccountAmount)`; `static RepaymentPrice? Compute(long amount, Currency currency, Currency debtCurrency, Currency? accountCurrency, decimal? manualRate, IReadOnlyDictionary<Currency, decimal> plnValues)` — the "Repayment conversion" decision; `null` when a needed rate is missing.
  - `AddRepaymentCommand(Guid DebtId, JsonElement? Date, Amount, Currency, Rate, AccountId, Note) : IRequest<Result<DebtDto>>, IInvalidatesCache` (accounts tag). Validator: `amount` required → invalidValue → positiveAmount; `currency` required → invalidValue; `date` required → invalidValue → dateTooFar; `rate` invalidValue; `accountId` not a GUID → unknownAccount; `note` invalidValue → noteTooLong. References validator: account of the user (`unknownAccount`), not archived (`archivedAccount`). Handler: one transaction; debt `FOR UPDATE` of the user (404 when missing); account `FOR SHARE` with the same errors; `FreshPlnValuesAsync(date, today)`; `null` price → 503; `DebtAmount` or `AccountAmount` ≤ 0 → `rate: positiveAmount`; either > `MinorUnits.MaxAmount` → `rate: invalidValue`; `DebtAmount > Remaining` → `amount: exceedsRemaining`; `Repay`, insert, save, answer the projected `DebtDto`. Endpoint `POST /v1/debts/{id:guid}/repayments` → 200.
  - `RepaymentResponse(Guid Id, string Date, long Amount, string Currency, double Rate, long DebtAmount, Guid? AccountId, long? AccountAmount, string? Note)`; `ListRepaymentsQuery(Guid DebtId, int Page, int PageSize) : IRequest<Result<PageResponse<RepaymentResponse>>>`; `GET /v1/debts/{id:guid}/repayments?page=1&pageSize=100` → 404 for an unknown or foreign debt, newest first.
  - `DeleteRepaymentCommand(Guid DebtId, Guid RepaymentId) : IRequest<Result>, IInvalidatesCache`; handler locks the debt `FOR UPDATE`, finds the repayment of that debt and user (404 otherwise), `Unrepay`, removes it; `DELETE /v1/debts/{id:guid}/repayments/{repaymentId:guid}` → 204.
- [ ] **Step 1: Failing tests**
  - `RepaymentPriceTests` (PLN values EUR 4.2563, USD 3.9012): `(5000, EUR, PLN, EUR, null)` → `Rate 4.2563, DebtAmount 21282, AccountAmount 5000`; `(5000, EUR, PLN, PLN, null)` → account 21282; `(5000, EUR, PLN, USD, null)` → account `MinorUnits.Convert(5000, ExchangeRate.Cross(4.2563, 3.9012), EUR, USD)` (5455); `(5000, EUR, PLN, USD, 4.0)` → debt 20000, account still 5455; `(5000, EUR, PLN, null, null)` → account null; `(1000, PLN, PLN, PLN, 7)` → `(1, 1000, 1000)`; missing EUR → null; missing USD with a USD account → null.
  - `AddRepaymentValidatorTests`: one row per rule, in order.
  - `RepaymentsTests` (`WithIsolatedData` where rates are seeded):
    - `Should_ReduceRemaining_And_MoveMoney_When_RepaymentRecorded`: lent 50 000 from a PLN account (balance 50 000 after it); repay 10 000 PLN into it → 200 `repaid 10000`, `remaining 40000`, `repayments 1`, `status "open"`; balance 60 000.
    - `Should_Return400_When_RepaymentExceedsRemaining`: then 40 001 → 400 `amount: ["validation.exceedsRemaining"]`, no repayment row, balance 60 000.
    - `Should_SettleDebt_When_RepaidInFull`: a lent debt due yesterday (overdue) repaid by exactly its remaining → `status "settled"`, `overdue false`, it leaves the open list and the summary; one more minor unit → 400 `exceedsRemaining`.
    - `Should_ConvertRepayment_When_CurrencyDiffers`: PLN debt 50 000; EUR 4.2563 on the date; 5 000 EUR into a EUR account → `debtAmount 21282`, `rate 4.2563`, `accountAmount 5000`, EUR balance +5 000; into a PLN account → `accountAmount 21282`; with `rate` 4.0 → `debtAmount 20000`.
    - `Should_UseDateRate_When_AccountInThirdCurrency`: USD 3.9012 too; 5 000 EUR into a USD account → `accountAmount 5455` with or without a manual rate.
    - `Should_MoveMoneyOut_When_BorrowedDebtRepaid`: borrowed 100 000 into an account (balance +100 000); repay 30 000 from it → −30 000.
    - `Should_Return503_When_RepaymentRateIsStale`: EUR stored 40 days before the date only → 503, nothing written; with `rate` → 200.
    - `Should_ListRepayments_NewestFirst`: three repayments on three dates → newest first with every field; `pageSize=2` → `total 3`.
    - `Should_RestoreDebt_And_Balance_When_RepaymentDeleted`: delete the 10 000 repayment → 204, `remaining` back, balance back, `repayments` 0; deleting it again → 404; a repayment id of another debt in the path → 404.
    - `Should_Return404_When_DebtBelongsToAnotherUser`: user B's `POST`, `GET` and `DELETE` on user A's debt and repayment → 404, A's data unchanged.
    - `Should_Return400_BeforeLookup_When_BodyInvalid`: an invalid body to a random debt id → 400; a valid body → 404.
    - `Should_RefuseArchivedAccount_ForRepayment` → `accountId: ["validation.archivedAccount"]`.
  - `OpenApiTests.Should_DescribeDebts` (extended): `/v1/debts/{id}/repayments` (get, post) and `/v1/debts/{id}/repayments/{repaymentId}` (delete) tagged `Debts`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite. `be-run.sh 'dotnet test --filter "FullyQualifiedName~Repayment|FullyQualifiedName~OpenApiTests"'`
- [ ] **Step 3: Commit** — `feat: partial repayments of private debts`

### Task 4: Edit and delete debts

**Files:** new `Features/Debts/UpdateDebt/{UpdateDebtCommand,UpdateDebtEndpoint,UpdateDebtHandler,UpdateDebtValidator,UpdateDebtReferencesValidator}.cs`, `DeleteDebt/{DeleteDebtCommand,DeleteDebtEndpoint,DeleteDebtHandler}.cs`, `DebtsEndpoints.cs`; tests new `tests/Mita.Api.IntegrationTests/Features/Debts/UpdateDeleteDebtTests.cs`, `OpenApiTests.cs`.

**Interfaces:**
- Consumes: Tasks 1-3 (`DebtInputValidator<T>`, `DebtReferencesValidator<T>`, `DebtInputParser`, `DebtWriter.PrepareAsync(…, existingAccountId, …)`, `Debt.Update`).
- Produces:
  - `UpdateDebtCommand(Guid Id, …same fields…) : IRequest<Result<DebtDto>>, IDebtInput, IInvalidatesCache` (`ExistingId => Id`; both commands of this task invalidate `AccountCacheTags.Accounts`); handler: stored account id of the user's debt (404 when missing), `PrepareAsync`, debt `FOR UPDATE` (404), `currency: ["validation.debtCurrencyLocked"]` when the currency changes and `Repaid > 0`, `amount: ["validation.belowRepaid"]` when `Amount < Repaid` (both reported together), `Update`, save, projected `DebtDto`. `PUT /v1/debts/{id:guid}` → 200.
  - `DeleteDebtCommand(Guid Id) : IRequest<Result>, IInvalidatesCache` → `ExecuteDeleteAsync` scoped to the user; `DELETE /v1/debts/{id:guid}` → 204 or 404.
- [ ] **Step 1: Failing tests** (`UpdateDeleteDebtTests`)
  - `Should_UpdateDebt_And_MoveLeg_When_AccountChanges`: lent 30 000 from account A (balance 70 000 of 100 000); `PUT` with account B → A 100 000, B −30 000; `PUT` without an account → both back to their openings; every field round-trips (counterparty trimmed, due date cleared with `null`, note).
  - `Should_KeepArchivedAccount_When_Updating`: the debt's account is archived after creation; `PUT` with the same account → 200; with another archived account → `archivedAccount`.
  - `Should_RefuseCurrencyChange_When_RepaymentsExist`: with a repayment → `currency: ["validation.debtCurrencyLocked"]`; after deleting the repayment → 200.
  - `Should_RefuseAmountBelowRepaid`: repaid 10 000; `amount` 9 999 → `amount: ["validation.belowRepaid"]`; 10 000 → 200 and `status "settled"`.
  - `Should_DeleteDebt_WithRepayments_And_RestoreBalances`: lent 30 000 from A with a 10 000 repayment into B → `DELETE` 204; A and B back to their openings; `GET /v1/debts/{id}/repayments` 404; both accounts deletable again.
  - `Should_Return404_When_DebtUnknownOrForeign`: `PUT`/`DELETE` on a random id and on another user's debt → 404 (the foreign debt unchanged); an invalid body to a random id → 400.
  - `OpenApiTests.Should_DescribeDebts` (extended): `/v1/debts/{id}` (put, delete) tagged `Debts`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite.
- [ ] **Step 3: Commit** — `feat: edit and delete private debts`

### Task 5: Overview debts summary and the API contract

**Files:** `Features/Overview/GetOverview/{GetOverviewHandler,OverviewResponse}.cs`, `docs/api-contract.md`; tests `Features/Overview/OverviewTests.cs`, `Features/Reports/ReportsTests.cs`.

**Interfaces:**
- Consumes: `DebtSummaries.SummarizeAsync`, `DebtSummaryDto` (Task 2).
- Produces: `OverviewResponse` gains `DebtSummaryDto Debts` right after `Balance` (wire `debts: { owedToMe, owedByMe, open, overdue }`), computed inside the existing repeatable-read snapshot; `null` → 503 `rates.unavailable`.
- [ ] **Step 1: Failing tests**
  - `OverviewTests.Should_SummarizeDebts`: the data of `Should_ListOpenDebts_WithSummary` → `debts` equals `{ 61500, 150000, 3, 1 }`; a new user → `{ 0, 0, 0, 0 }`; another user's debts never count.
  - `OverviewTests.Should_NotCountDebts_InTotals`: a user with only accounts, a lent and a borrowed debt on accounts and a repayment of each → `income.amount` 0, `expense.amount` 0, `transactionCount` 0, `spending` empty, `recent` empty, `hasTransactions` false, and `balance.amount` equals the sum of the account balances (the debt legs included).
  - `OverviewTests.Should_RepriceDebts_When_BaseCurrencyChanges`: lent 43 000 PLN, EUR 4.30 today; base EUR → `owedToMe` 10000.
  - `OverviewTests.Should_Return503_When_DebtNeedsMissingRate`: an open KWD debt, no KWD rate → 503.
  - `ReportsTests.Should_IgnoreDebts_InReports`: the same debts and repayments → every period 0, `totals` 0, `structure.items` empty, `hasTransactions` false.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite.
- [ ] **Step 3: Commit** — `feat: debts summary on the overview`
- [ ] **Step 4: Contract.** Title gains "debts". Endpoint rows for the seven debt routes (errors: list 400, 401, 503; create and update 400, 401, 403, 404 (update), 503; delete 401, 403, 404; repayments list 400, 401, 404; add 400, 401, 403, 404, 503; delete repayment 401, 403, 404). Types `DebtDirection`, `DebtStatus`, `Debt`, `DebtInput`, `DebtSummary`, `DebtPage`, `DebtRepayment`, `DebtRepaymentInput` with the limits in comments, and `Overview.debts`. A new section "Debts" after "Budgets and goals": privacy, direction and account legs (balance formula of "Accounts" now names debts and repayments), every Decisions rule for conversion, staleness, over-repayment, editing, deleting, due date, status, overdue, summary, ordering, paging, the 200 answer of a repayment, and "debts never appear in reports, budgets or the overview totals". Validation table: the four new codes; `required`, `invalidValue`, `nameTooLong`, `noteTooLong`, `positiveAmount`, `dateTooFar`, `unknownAccount`, `archivedAccount` mention debts and repayments; 503 row lists the debt endpoints and `GET /v1/overview` for a debt; `accountInUse`/`currencyLocked` mention debt legs. Overview section: `debts`.
- [ ] **Step 5: Run** `be-run.sh 'dotnet build -warnaserror && dotnet test'`; all green.
- [ ] **Step 6: Commit** — `docs: api contract for private debts`

### Task 6: Frontend contract and mock backend

Frontend repo, branch `feature/debts` from `origin/develop`.

**Files:** `app/shared/api/contract.ts`, `mocks/{db,ledger,overview}.ts`, new `mocks/debts.ts`, new `mocks/handlers/debts.ts`, `mocks/handlers/index.ts`, new `mocks/seed/debts.ts`, `mocks/seed/index.ts`; tests new `tests/unit/mocks/{debts,seedDebts}.test.ts`, `tests/unit/mocks/{overview,reports,db}.test.ts`.

**Interfaces:**
- Consumes: the Task 5 contract; `freshRateOn`, `rateOn`, `convertMinor`, `roundRate`, `paginate`, `collectErrors`, `isMissing`, `isManualRate`, `checkAccount`, `baseCurrencyOf`, `amountIn`, `oneYearAhead`.
- Produces:
  - `contract.ts`: `DebtDirection = 'lent' | 'borrowed'` and `debtDirections`; `DebtStatus = 'open' | 'settled'` and `debtStatuses`; `DebtDto`, `DebtInput` (`dueDate?: string | null`, `accountId?: string | null`, `rate?: number`, `note?: string | null`), `DebtSummary = { owedToMe: number; owedByMe: number; open: number; overdue: number }`, `DebtPage = Page<DebtDto> & { currency: Currency; summary: DebtSummary }`, `DebtRepaymentDto`, `DebtRepaymentInput`; `OverviewDto.debts: DebtSummary`; `COUNTERPARTY_MAX = 40`, `DEBT_NOTE_MAX = 200`.
  - `db.ts`: `DebtRow = { id; userId; direction; counterparty; amount; currency; date; dueDate; accountId; accountAmount; rate; note; repaid; createdAt }`, `DebtRepaymentRow = DebtRepaymentDto & { userId: string; debtId: string; createdAt: string }`; `MockState` gains `debts` and `debtRepayments`; `version: 3` (`isState` checks 3).
  - `ledger.ts`: `debtMovements: Ledger` (both legs, signs from Decisions) appended to `ledgers`.
  - `debts.ts`: `priceDebt(rates, { date, today, amount, currency, accountCurrency: Currency | null, rate?: number }): { rate: number | null; accountAmount: number | null }`; `priceRepayment(rates, { date, today, amount, currency, debtCurrency, accountCurrency: Currency | null, rate?: number }): { rate: number; debtAmount: number; accountAmount: number | null }` (missing or stale rate → `ratesUnavailable()`); `debtView(state, row, today): DebtDto`; `debtsOf(state, userId, status, today): DebtDto[]` (Decisions ordering, insertion order standing in for creation time); `summarizeDebts(state, userId, base, today): DebtSummary` (per-currency sums, `amountIn` at today).
  - `handlers/debts.ts` `registerDebtHandlers`: the seven routes with the backend's rules, field order and codes, `paginate` with 100/100, 404 through `notFound()` for unknown and foreign ids, `db.save()` after writes.
  - `seed/debts.ts`: `demoDebts(today): { debts: DebtRow[]; repayments: DebtRepaymentRow[] }` — Anna: lent "Max" 50 000 PLN on `today − 20`, due `today + 10`, account `acc_anna_cash`, repaid 10 000 on `today − 5` into `acc_anna_cash`; borrowed "Mama" 200 000 PLN on `today − 60`, no due date, account `acc_anna_card`, repaid 50 000 on `today − 30` from `acc_anna_card`; lent "Kasia" 5 000 EUR on `today − 40`, due `today − 5`, no account; lent "Tomek" 12 000 PLN on `today − 90`, no account, repaid 12 000 on `today − 70`. Oleh: borrowed "Андрій" 300 000 UAH on `today − 15`, no account. `withDebtHistory(accounts, debts, repayments): AccountRow[]` lowers opening balances by the debt legs; `seedState` applies it after `withTransactionHistory`.
  - `overview.ts`: `debts: summarizeDebts(state, userId, base, today)`.
- [ ] **Step 1: Failing unit tests**
  - `debts.test.ts` (rates seeded in the test state; numbers identical to the backend tests): create lent 50 000 from a PLN account → 201 shape and balance −50 000; borrowed +; no account → nulls; EUR 5 000 on a PLN account at 4.2563 → 21282, manual 4.5 → 22500, 40-day-old EUR → 503; list order and summary `{ 61500, 150000, 3, 1 }`; repayment rows of `RepaymentPriceTests` (21282 / 5000 / 21282 / 5455 / 20000); 40 001 of 40 000 → `amount: validation.exceedsRemaining`; exact → settled; `PUT` currency with a repayment → `validation.debtCurrencyLocked`, amount below repaid → `validation.belowRepaid`; due date before date → `validation.dueBeforeDate`; validation rows of `CreateDebtValidatorTests`; delete debt restores balances; delete repayment restores; another user's ids → 404; an account in use only by a debt cannot be deleted (`validation.accountInUse`).
  - `seedDebts.test.ts` on the seed days of `seedGoals.test.ts`: Anna's balances `acc_anna_card` 882 040, `acc_anna_cash` 94 000, `acc_anna_savings` 1 400 000; her summary `owedByMe` 150 000, `open` 3, `overdue` 1, settled list `["Tomek"]`; Oleh's open list `["Андрій"]`.
  - `overview.test.ts` and `reports.test.ts`: their state resets add `debts: []` and `debtRepayments: []`; a new row: overview `debts` equals `summarizeDebts` and income/expense ignore debts.
  - `db.test.ts`: a stored state with `version: 2` is reseeded.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/wsl-run.sh 'bun test tests/unit && bun run lint && bun run typecheck'`; the mock E2E suite still passes (balances unchanged).
- [ ] **Step 3: Commit** — `feat: debts in the mock backend`

### Task 7: Tools section

**Files:** `app/shared/utils/navigation.ts`, `app/shared/types/icon.ts`, `app/shared/components/{AppIcon,AppSidebar}.vue`, `app/pages/index.vue` (mobile header), new `app/pages/tools/index.vue`, new `app/features/tools/{utils/tools.ts,components/ToolCard.vue}`, `i18n/locales/*.json`; tests new `tests/e2e/tools.spec.ts`, `tests/e2e-real/navigation.real.spec.ts`.

**Interfaces:**
- Produces:
  - `NavigationItem.key` gains `'tools'`; `primaryNavigation` gains `{ key: 'tools', to: '/tools', icon: 'tools' }` after reports (the mobile bar's filters are unchanged).
  - `IconName` gains `'tools'` (wrench) and `'debts'` (two opposite arrows over a coin or similar), outline, 24px grid, `currentColor`.
  - `AppSidebar` `isActive(to)`: exact match for `/`, otherwise exact or prefix `localePath(to) + '/'`.
  - `tools.ts`: `type ToolKey = 'debts'`, `type ToolEntry = { key: ToolKey; to: string; icon: IconName }`, `tools: readonly ToolEntry[] = [{ key: 'debts', to: '/tools/debts', icon: 'debts' }]`.
  - `ToolCard.vue` (`entry: ToolEntry`): one `NuxtLinkLocale` card (min 44px, focus ring) with the icon, `t('tools.entries.<key>.title')` and `.description`.
  - `/tools` page: `PageHeader` eyebrow `t('tools.eyebrow')`, title `t('navigation.tools')`; a `ul` labelled `t('tools.list.label')` of `ToolCard`s; `useHead` title.
  - Mobile Overview header: a third icon link to `/tools` labelled `t('navigation.tools')`, same style as the calendar and settings links.
  - i18n: `navigation.tools` = Tools / Narzędzia / Инструменты / Інструменти; `tools.eyebrow` (en "Extras"); `tools.list.label` ("Tools"); `tools.entries.debts.title` = Debts / Długi / Долги / Борги; `tools.entries.debts.description` (en "Money you lent or borrowed, and what is left to repay").
- [ ] **Step 1: Failing E2E** (`tools.spec.ts`)
  - desktop: the sidebar link "Tools" opens `/tools` with heading "Tools" and `aria-current="page"`; the list has exactly one link, "Debts", with `href` ending `/tools/debts`.
  - mobile: the Overview header link "Tools" opens `/tools`.
  - `/pl/tools`, `/ru/tools`, `/uk/tools` show the headings Narzędzia, Инструменты, Інструменти.
  - `navigation.real.spec.ts`: a `Tools` row (`/tools`, heading `Tools`) after Reports.
- [ ] **Step 2: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite (desktop and mobile); all pass.
- [ ] **Step 3: Commit** — `feat: tools section in the navigation`

### Task 8: Debts page with lending and borrowing

**Files:** new `app/pages/tools/debts.vue`, new `app/features/debts/{api/debtsApi.ts, api/keys.ts, composables/useDebts.ts, composables/useDebtMutations.ts, components/DebtSummaryCards.vue, components/DebtCard.vue, components/DebtDialog.vue, schemas/debtSchema.ts, utils/debtView.ts}`, `app/shared/api/invalidation.ts`, `i18n/locales/*.json`; tests new `tests/unit/features/debts/{debtSchema,debtView}.test.ts`, `tests/unit/shared/invalidation.test.ts`, new `tests/e2e/debts.spec.ts`, `tests/e2e/tools.spec.ts`.

**Interfaces:**
- Consumes: Task 6 contract; `useAccounts`, `useRates`, `useUserSettings`, `CurrencySelect`, `FormRow`, `StatCard`, `ProgressBar`, `EmptyState`, `ErrorState`, `PageHeader`, `useServerErrors`, `useNotify`, `useConfirm`, `toMinor`, `fromMinor`, `fitMajor`, `convertMinor`, `withinAmountLimit`, `withinRateLimit`, `currencyDigits`.
- Produces:
  - `debtsApi(api)`: `list(status: DebtStatus) → DebtPage` (`{ status, pageSize: 100 }`), `create(body: DebtInput) → DebtDto`, `update(id, body) → DebtDto`, `remove(id)`.
  - `debtKeys = { all: ['debts'], list: (status: DebtStatus) => ['debts', 'list', status] as const, repayments: (id: string) => ['debts', 'repayments', id] as const }`.
  - `useDebts(status: MaybeRefOrGetter<DebtStatus>) → { page, items, isPending, isError, refetch }`; `useDebtMutations() → { create, update, remove }`, each invalidating `'debts'`.
  - `invalidation.ts`: `DataChange` gains `'debts'` → `[['debts'], ['accounts'], ['overview']]`; `settings` also invalidates `['debts']`.
  - `debtView.ts`: `repaidShare(debt: Pick<DebtDto, 'amount' | 'repaid'>): number` (0 to 1); `convertedPreview(amount: number | null, rate: number | null, from: Currency, to: Currency): number | undefined` (minor units of `to`, undefined when an input is missing or not positive).
  - `debtSchema.ts`: `DebtForm = { direction: DebtDirection; counterparty: string; amount: number | null; currency: Currency; date: Date | null; dueDate: Date | null; accountId: string | null; rate: number | null; note: string }`; `debtRules(t, { crossCurrency: () => boolean; currency: () => Currency; maxDate: () => Date })`: counterparty required and ≤ 40 after trimming (`validation.nameTooLong`), amount required, positive, within the limit, date required and ≤ `maxDate` (`validation.dateTooFar`), due date not before the date (`validation.dueBeforeDate`), rate required when `crossCurrency`, positive, ≤ `MANUAL_RATE_MAX`, note ≤ 200 (`validation.noteTooLong`).
  - Page `/tools/debts`: `PageHeader` with the eyebrow as a link to `/tools` (`t('navigation.tools')`), title `t('debts.title')`, action "New debt" (button on desktop, icon button on mobile as on Goals); `DebtSummaryCards` (`summary`, `currency`): StatCards "Owed to me", "I owe", "Overdue" (count); section "Personal debts" (`aria-labelledby`) with a `SelectButton` Open / Settled and the `DebtCard` list; empty states: open "No open debts" + "Add a debt", settled "No settled debts yet"; skeletons and `ErrorState` as on Goals.
  - `DebtCard` (`debt: DebtDto`, `accountName?: string`; emits `edit`, `repay`): an `article` labelled by the counterparty; direction label ("Owed to me" / "I owe"); remaining in the debt currency, "of {amount}" when `repaid > 0`; `ProgressBar` of `repaidShare` labelled "{name}: share repaid"; "Since {date}"; "Due {date}", or the overdue badge "Overdue · due {date}" in `text-accent` with `data-overdue` when `overdue`; the account name; the note; buttons "Repayment" (hidden when settled; `aria-label` "Record a repayment for {name}") and "Edit" (`aria-label` "Edit {name}"). Dates through `d()`.
  - `DebtDialog` (`debt?: DebtDto`, `v-model:visible`): direction `SelectButton` "I lent" / "I borrowed"; "Who" (`InputText`); amount (`InputNumber` currency mode, `currencyDigits`) and `CurrencySelect compact` pinned to the base currency and the account currencies; date; due date (`DatePicker` with clear, optional); account (`Select`, active accounts plus the debt's own archived one, `show-clear`, placeholder "No account") with hint "The amount leaves this account." (lent) / "The amount enters this account." (borrowed); rate (label "Exchange rate ({from} → {to})", suggested from `useRates(account currency).rateFrom(currency)`, edited flag as `TransactionDialog`) and the converted amount line when the account currency differs; note. Currency disabled with hint "The currency cannot change once a repayment is recorded." when `debt.repayments > 0`; the amount re-fits with `fitMajor` when the currency changes. Edit: "Delete" with confirm "Delete the debt?" / "“{name}” and its repayments will be deleted. Account balances are restored." Server field errors through `useServerErrors().apply` for every field; toasts "Debt saved", "Debt updated", "Debt deleted".
  - i18n `debts.*` for every string above, `validation.dueBeforeDate` ("The due date cannot be before the date."), `validation.belowRepaid` ("The amount cannot be less than what was already repaid."), `validation.debtCurrencyLocked` ("The currency cannot change once a repayment is recorded."), `validation.exceedsRemaining` ("The amount is more than what is left to repay."); `settings.delete.description` mentions debts. The edit dialog title follows the `locales.test.ts` rule for ru/uk (starts with "Изменение"/"Зміна") and is added to that test's list.
- [ ] **Step 1: Failing unit tests**
  - `debtSchema.test.ts`: counterparty `'   '` required, 41 characters `validation.nameTooLong`; amount 0 positive; 1 000 000 000.01 PLN invalid; due date the day before the date `validation.dueBeforeDate`; rate required only when `crossCurrency`; note 201 characters `validation.noteTooLong`.
  - `debtView.test.ts`: `repaidShare` of 10 000 / 50 000 → 0.2, of a settled debt → 1; `convertedPreview(50, 4.2563, 'EUR', 'PLN')` → 21282, missing rate → undefined.
  - `invalidation.test.ts`: `'debts'` row and the `settings` row.
- [ ] **Step 2: Failing E2E** (`debts.spec.ts`, both projects; `tools.spec.ts` gains the click-through)
  - "shows the seeded debts with the overdue one highlighted": summary "I owe" `zł 1,500.00` and "Overdue" `1`; cards in order Kasia (overdue badge), Max (`zł 400.00` and `of zł 500.00`), Mama; "Settled" shows Tomek only.
  - "lends money from an account": "I lent", Who `Ola`, 100, account `Gotówka · PLN`, save → card Ola `zł 100.00`; on Accounts, Gotówka shows `zł 840.00`.
  - "borrows without an account": "I borrowed", `Bank`, 300 → "I owe" `zł 1,800.00`; Accounts total unchanged.
  - "edits and deletes a debt": edit Ola to 120 → Gotówka `zł 820.00`; delete with confirmation → the card disappears and Gotówka `zł 940.00`.
  - "refuses a due date before the date": the field shows "The due date cannot be before the date."
  - `/pl/tools/debts` shows the heading "Długi".
  - `tools.spec.ts`: the "Debts" entry opens `/tools/debts` with heading "Debts"; the page eyebrow link "Tools" returns to `/tools`.
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite; all pass.
- [ ] **Step 4: Commit** — `feat: debts page with lending and borrowing`

### Task 9: Repayments on the Debts page

**Files:** new `app/features/debts/{components/RepaymentDialog.vue, components/RepaymentList.vue, composables/useRepayments.ts, schemas/repaymentSchema.ts}`, `app/features/debts/{api/debtsApi.ts, composables/useDebtMutations.ts, components/DebtCard.vue}`, `app/pages/tools/debts.vue`, `i18n/locales/*.json`; tests new `tests/unit/features/debts/repaymentSchema.test.ts`, `tests/e2e/debts.spec.ts`.

**Interfaces:**
- Consumes: Task 8 (`debtsApi`, `debtKeys.repayments`, `useDebtMutations`, `DebtCard`, `convertedPreview`).
- Produces:
  - `debtsApi`: `repayments(id) → Page<DebtRepaymentDto>` (`pageSize: 100`), `repay(id, body: DebtRepaymentInput) → DebtDto`, `removeRepayment(id, repaymentId)`.
  - `useRepayments(id: MaybeRefOrGetter<string>, enabled: MaybeRefOrGetter<boolean>) → { items, isPending, isError, refetch }`; `useDebtMutations` gains `repay` and `removeRepayment` (invalidate `'debts'`).
  - `repaymentSchema.ts`: `RepaymentForm = { amount: number | null; currency: Currency; date: Date | null; rate: number | null; accountId: string | null; note: string }`; `repaymentRules(t, { crossCurrency, currency, maxDate, remaining: () => number | undefined })` — amount required, positive, within the limit, and when `remaining()` is defined (same currency as the debt) `toMinor(amount) <= remaining` (`validation.exceedsRemaining`); rate required when `crossCurrency`; date required and ≤ `maxDate`; note ≤ 200.
  - `RepaymentDialog` (`debt: DebtDto`, `v-model:visible`): header "Repayment from {name}" (lent) / "Repayment to {name}" (borrowed); amount and `CurrencySelect compact` (default the debt currency); "Repay the rest" button (sets the debt currency and the remaining amount); date; rate when the currency differs from the debt's ("Exchange rate ({from} → {to})", suggested from `useRates(debt.currency)`); line "Reduces the debt by {amount}" from `convertedPreview`; account (`Select`, active accounts, default the debt's account when active, `show-clear`, hint "The money enters this account." for lent, "The money leaves this account." for borrowed); note; server errors mapped; toast "Repayment saved".
  - `RepaymentList` (`debt: DebtDto`): rows newest first with date, the amount in its currency, "= {debtAmount}" when the currency differs, the account name; a delete button (`aria-label` "Delete the repayment of {date}") with confirm "Delete this repayment? The debt and the account balance are restored."; toast "Repayment deleted".
  - `DebtCard`: "Repayment" emits `repay`; a "History" toggle (`aria-expanded`, shown when `repayments > 0`) renders `RepaymentList` below.
- [ ] **Step 1: Failing unit tests** (`repaymentSchema.test.ts`): 401 PLN with remaining 40 000 → `validation.exceedsRemaining`; 400 → valid; EUR 500 with remaining undefined → valid; rate required only when `crossCurrency`.
- [ ] **Step 2: Failing E2E** (`debts.spec.ts`)
  - "records a partial repayment into an account": Max → Repayment 100 into `Gotówka · PLN` → `zł 300.00` `of zł 500.00`; Gotówka `zł 1,040.00`.
  - "settles a debt with repay the rest": Kasia → "Repay the rest" → save → she leaves the open list, "Overdue" shows `0`, "Settled" lists her.
  - "repays in another currency": Mama → 100 EUR with rate `4.30` → "Reduces the debt by zł 430.00" then the card shows `zł 1,070.00`.
  - "refuses more than what is left": Max → 401 → "The amount is more than what is left to repay."
  - "deletes a repayment from the history": Max → History lists the seeded 100 zł repayment → delete → `zł 500.00`; Gotówka `zł 840.00`.
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite; all pass.
- [ ] **Step 4: Commit** — `feat: partial repayments on the debts page`

### Task 10: Overview debts card and balance with debts

**Files:** new `app/features/overview/{components/OverviewDebts.vue, composables/useBalanceWithDebts.ts}`, `app/features/overview/utils/overviewView.ts`, `app/features/overview/components/{OverviewStats,OverviewBalanceCompact}.vue`, `app/pages/index.vue`, `i18n/locales/*.json`; tests `tests/unit/features/overview/overviewView.test.ts`, `tests/e2e/overview.spec.ts`.

**Interfaces:**
- Consumes: `OverviewDto.debts` (Task 6).
- Produces:
  - `overviewView.ts`: `balanceWithDebts(amount: number, debts: DebtSummary): number` = `amount + debts.owedToMe - debts.owedByMe`.
  - `useBalanceWithDebts() → { included: Readonly<Ref<boolean>>; setIncluded(value: boolean): void }` on `localStorage['mita.balanceWithDebts']` (`'1'` or removed), read in `onMounted`, every access in try/catch, one shared state for the page.
  - `OverviewDebts` (`overview?: OverviewDto`, `failed?: boolean`; emits `retry`): `section` labelled by its heading "Debts"; rows "Owed to me" and "I owe" (`MoneyText`, data-testids `overview-owed-to-me`, `overview-owed-by-me`); "{count} overdue" in `text-accent` when `overdue > 0` (plural forms); `ToggleSwitch` "Include debts in the balance" (labelled, shown when `open > 0`); link "All debts" → `/tools/debts`; empty: "No open debts." and "Add a debt" → `/tools/debts`; skeleton and compact `ErrorState` as `OverviewGoals`.
  - `OverviewStats` balance card and `OverviewBalanceCompact`: when `included && debts.open > 0`, the label is "Balance with debts" and the figure `balanceWithDebts(...)`.
  - `index.vue`: desktop bottom row `grid-cols-[repeat(auto-fit,minmax(250px,1fr))]` with `OverviewDebts` fourth; mobile `OverviewDebts` right after `OverviewBalanceCompact`.
- [ ] **Step 1: Failing unit test** — `balanceWithDebts(1_000_000, { owedToMe: 61_500, owedByMe: 150_000, open: 3, overdue: 1 })` → 911 500.
- [ ] **Step 2: Failing E2E** (`overview.spec.ts`, both projects)
  - "shows what is owed both ways": "I owe" `zł 1,500.00`, "Owed to me" matches `/^zł\s6\d{2}\.\d{2}$/` (400 zł plus 50 € at today's mock EUR rate), "1 overdue"; "All debts" opens `/tools/debts`.
  - "includes debts in the balance": the balance figure before the switch, then "Include debts in the balance" on → label "Balance with debts" and a different figure; after a reload the switch is still on; off → "Total balance" (desktop) / "Balance" (mobile) again.
  - "hides the switch without open debts": after deleting Max, Mama and Kasia on the Debts page, the Overview shows "No open debts." and no switch.
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite; all pass.
- [ ] **Step 4: Commit** — `feat: debts summary and balance with debts on the overview`

### Task 11: Real-backend spec, smoke and docs

Frontend repo, same branch; the backend checkout on `feature/debts` (Tasks 1-5).

**Files:** new `tests/e2e-real/debts.real.spec.ts`, the project guide in the repository root ("Real-backend suite": a `debts` line, the `navigation` line mentions Tools, the auth-call count per run rises by two); outside the repo `dock.mita-work/scripts/db-real-smoke.sh` (Decisions "Real smoke").
- [ ] **Step 1: Write `debts.real.spec.ts`** (helpers in the style of `tags.real.spec.ts`, same-currency amounts only): register a unique user; add a "Wallet" account (Cash, PLN, 1000); open Tools → Debts; lend `Max` 200 from Wallet with a due date yesterday → the card is overdue; Accounts: Wallet `zł 800.00`; repay 50 into Wallet → `zł 150.00` left, Wallet `zł 850.00`; borrow `Bank` 100 without an account; Overview: "Owed to me" `zł 150.00`, "I owe" `zł 100.00`, income and expense `zł 0.00`; switch on → "Balance with debts" `zł 900.00`; repay the rest of Max → he is under "Settled"; delete Bank; delete the user.
- [ ] **Step 2: Run** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/scripts/db-real-smoke.sh`; every real spec passes, the X-Forwarded-For check passes, both smoke projects are torn down, `pgdb`, `dockmitabackend` and `dock-mita-frontend-web-1` untouched.
- [ ] **Step 3: Run** `bun run lint && bun run typecheck && bun test tests/unit`; all green.
- [ ] **Step 4: Commit** — `test: real-backend spec for private debts`

### Task 12: Pull requests

- [ ] Backend: rebase `feature/debts` on `origin/develop`, fold fix-ups into their task commits (tree unchanged), `dotnet build -warnaserror` and `dotnet test --logger junit`, push with `--force-with-lease`, open a PR to `develop` with a plain description, wait for `check-source-branch`, `gh pr merge --rebase`.
- [ ] Frontend: after the backend PR is merged, the same flow with lint, typecheck, unit, the mock E2E suite and the real smoke green.
- [ ] Both checkouts back on `develop`, pulled; local `feature/debts` branches deleted.

## Deferred (not in this plan)

Group balances on the Debts page (part 4), the Groups, Savings calculator and Currency converter tools (parts 3 and 5), debt movements in the account history on the Accounts page, per-person totals, editing a repayment in place, reminders for due dates, a "Show more" beyond 100 debts, and the delisted-currency follow-up of the currency-list plan (debts inherit the 30-day rule on writes).
