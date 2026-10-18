# Accounts, Transfers and Exchange Rates Implementation Plan

**Goal:** Real accounts on the backend. Accounts CRUD with archive and delete rules, transfers between accounts, balances computed by the server in minor units, and exchange rates synced from the National Bank of Poland, exactly as the frontend contract and mocks define them.

**Architecture:**
- **Domain:** `Account`, `Transfer` and `ExchangeRate` live in `Mita.Domain` (`Accounts/`, `Money/`). `Currency` moves from `Mita.Domain.Users` to `Mita.Domain.Money`.
- **Balances:** the database view `account_movements (UserId, AccountId, Amount)` is the single ledger seam. In this plan it unions the two legs of every transfer. The transactions plan redefines it with `CREATE OR REPLACE VIEW` to add transaction legs, and no account query changes. `balance = openingBalance + SUM(movements)`, `inUse = EXISTS(movement)`.
- **Rates:** a background service fetches NBP table A mid rates (PLN per 1 unit of EUR, USD, UAH) through a Polly-backed `HttpClient` into the table `exchange_rates`. Requests never call NBP. Cross rates are derived through PLN, as the mock does. `GET /v1/rates` is cached in Redis under a global (not user-scoped) key.
- **Slices:** `Features/Accounts/{ListAccounts,CreateAccount,UpdateAccount,DeleteAccount}`, `Features/Transfers/{ListTransfers,CreateTransfer}` and `Features/Rates/GetRates` run through the existing MediatR pipeline (validation, caching, invalidation).

**Tech Stack:** .NET 10, EF Core migrations (Npgsql), MediatR 12.5.0, FluentValidation, HybridCache over Redis, `Microsoft.Extensions.Http.Resilience` (Polly v8), xUnit v3, Shouldly, Moq, Bogus, Testcontainers. Frontend task: Nuxt, Playwright.

**Spec:** This plan argues from these documents:
- The backend `CLAUDE.md`, `docs/STACK.md`, `docs/api-contract.md` and the merged plans `docs/superpowers/plans/2026-10-18-backend-foundation.md` and `2026-10-18-auth-and-users.md`, which define the interfaces this plan consumes.
- The frontend contract in the frontend repo:
  - `docs/superpowers/specs/2026-10-15-frontend-mocks-design.md` (currency rules, minor units, "balances are computed by the backend", "delete only without history, otherwise archive");
  - `app/shared/api/contract.ts` (`AccountDto`, `AccountInput`, `TransferDto`, `TransferInput`, `RateDto`, `Page<T>`);
  - `mocks/handlers/accounts.ts`, `transfers.ts`, `rates.ts`, `mocks/transfers.ts`, `mocks/rates.ts`, `mocks/ledger.ts`, `mocks/paginate.ts`, `mocks/validation.ts` (rules and codes);
  - `app/shared/accounts/accountsApi.ts`, `app/features/accounts/api/transfersApi.ts`, `app/shared/rates/*` (what the client sends and reads).

## Decisions

| Topic | Decision |
|---|---|
| Transfers in scope | Yes. They are the only movement type that exists before transactions, they make `balance` and `inUse` real, and the accounts screen lists and creates them. Goal contributions stay in the goals plan |
| Balance until transactions exist | `openingBalance` plus transfer legs. `linkedGoals` is the constant `0` until the goals plan adds the column and the count |
| Ledger seam | View `account_movements` (see Architecture). The transactions plan adds a migration that replaces the view; account queries stay untouched |
| Rate source | NBP public API, table A, mid rates (`https://api.nbp.pl/api/exchangerates/tables/A/{from}/{to}/?format=json`, at most 93 days per call). Free, no key, publishes EUR, USD and UAH against PLN, which matches the mock's model (`X → PLN` rows, cross through PLN) |
| Rate sync | `RatesSyncService` (BackgroundService) runs at start and every `Rates:SyncInterval` (default 6 h). First run backfills `Rates:HistoryDays` (default 183, as the mock seed). Inserts use `ON CONFLICT DO NOTHING`, so several pods can sync at once. Failures are logged and retried at the next tick; the API keeps serving stored rates |
| Rate lookup | For a date: per currency, the latest stored rate on or before the date, else the earliest stored rate (weekends, holidays and dates before the history start, same as `mocks/rates.ts`). Cross rate `round(fromPln / toPln, 6, away from zero)` |
| Missing rates | `GET /v1/rates` omits pairs whose currencies have no stored rate (the mock would throw; the frontend already treats a missing pair as "total unavailable"). A cross-currency transfer without a manual rate and without stored rates answers 503 `{ title: "Service Unavailable", code: "rates.unavailable" }` |
| "Today" | The UTC date from `TimeProvider`, for the default rate date and for `dateTooFar`. The mock uses the browser's local date |
| Rates shape | `GET /v1/rates` returns a plain array as the contract does (at most 12 pairs), not a page. This is the one documented exception to "every collection is paginated" |
| Rates caching | Global HybridCache entry, key `g:rates:{date or "today"}:{from or "*"}:{to or "*"}`, tag `g:rates`, 1 hour; the sync invalidates `g:rates` after inserting rows. `ICachedQuery` gains `bool IsUserScoped => true`; rates set it to `false` |
| Resilience | `Microsoft.Extensions.Http.Resilience` `AddStandardResilienceHandler` (Polly v8 underneath): 3 retries with jitter, 10 s attempt timeout, 30 s total, circuit breaker defaults |
| Amounts | `long` minor units, `|amount| ≤ 100 000 000 000` (mock `MAX_AMOUNT`). JSON numbers with a zero fraction (`5.0`) count as integers, as `Number.isSafeInteger` does. Rates are `numeric(18,6)` in the database and JSON numbers on the wire |
| Conversion | `toAmount = round(fromAmount × rate, 0, away from zero)` in `decimal` (the mock's `convertMinor`). A manual rate is rounded to 6 decimals first. A manual rate above 1 000 000 is `validation.invalidValue` (the mock has no cap; the cap keeps the product inside `long`) |
| Request bodies | Account and transfer fields bind as `JsonElement?`, like `UpdateSettingsRequest`, so a wrong JSON type gives a field error (`openingBalance: ["validation.invalidValue"]`) instead of `body` |
| Blank strings | A whitespace-only string counts as missing (`validation.required`), as the mock's `isMissing` does for accounts and transfers |
| Control characters | Account names and transfer notes containing a control character give `validation.invalidValue`, as user names already do |
| 404 vs 400 | Validation runs in the pipeline before the handler, so an invalid body on an unknown account id answers 400, where the mock answers 404. Valid bodies on unknown or foreign ids answer 404 |
| Ids | `Guid.CreateVersion7()`; routes use `{id:guid}`, so a non-GUID id is a 404. `GET /v1/transfers?accountId=` with a non-GUID is 400 `accountId: ["validation.invalidValue"]` (the mock silently returns nothing) |
| Ordering | Accounts by `CreatedAt, Id` (the mock's insertion order). Transfers by `Date desc, CreatedAt desc, Id desc` (the mock's "newest first, later insert first") |
| Status codes | `POST /v1/accounts` answers 201 with `Location`, `POST /v1/transfers` 201 without one, `DELETE` 204. The mock answers 200 and empty; the frontend accepts any 2xx |
| Races | Transfer creation locks both accounts with `SELECT … FOR UPDATE` (in id order) and re-checks archived; an account update that changes the currency locks the row and re-checks `inUse`; a delete that loses the race against a new transfer gets the FK violation mapped to `validation.accountInUse` |
| User deletion | `transfers → accounts` FKs are `NO ACTION` (checked at the end of the statement), `accounts → user` and `transfers → user` cascade, so deleting a user with history works in one statement |
| Not in this plan | Transactions, categories, budgets, goals (and goal contributions), overview and reports; editing or deleting transfers (the mock has neither); a manual rate admin API; rates for currencies beyond PLN, EUR, USD, UAH |

## Global Constraints

- All foundation and auth constraints apply: warnings are errors; central packages; `Api → Infrastructure → Domain`; records, `sealed`, no comments, English; `CancellationToken` everywhere; every endpoint in the v1 group with `RequireAuthorization("User")`; ProblemDetails for every error; XSRF on unsafe cookie requests (already global).
- Contract with the frontend, byte for byte (camelCase JSON):
  - `AccountDto { id, name, type: "card"|"cash"|"savings", currency: "PLN"|"EUR"|"USD"|"UAH", openingBalance, balance, cardLast4: string|null, archived, inUse, linkedGoals }`;
  - `AccountInput { name, type, currency, openingBalance, cardLast4?: string|null, archived?: boolean }`;
  - `TransferDto { id, date: "YYYY-MM-DD", fromAccountId, toAccountId, fromAmount, toAmount, rate, note: string|null }`;
  - `TransferInput { date, fromAccountId, toAccountId, fromAmount, rate?, note? }`;
  - `RateDto { date: "YYYY-MM-DD", from, to, value }`;
  - `Page<T> { items, page, pageSize, total }`.
- Endpoints:
  - `GET /v1/accounts?page&pageSize`: default pageSize 100, max 100;
  - `POST /v1/accounts`, `PUT /v1/accounts/{id}`, `DELETE /v1/accounts/{id}`;
  - `GET /v1/transfers?accountId&page&pageSize`: default pageSize 20, max 100; `accountId` matches either side;
  - `POST /v1/transfers`;
  - `GET /v1/rates?date&from&to`: all optional; without `from`/`to` every currency; never a pair with `from == to`.
- Validation codes, exactly as the mocks (first failing rule per field only):
  - accounts: `name` required / invalidValue (not a string) / nameTooLong (> 40 after trim); `type` required / invalidValue; `currency` required / invalidValue / currencyLocked (update of an account in use to another currency); `openingBalance` required / invalidValue; `cardLast4` cardLast4 (present, not blank, and not exactly 4 digits; checked for every type, stored only for `card`); `archived` invalidValue (present and not a boolean); delete of an account in use gives `id: ["validation.accountInUse"]`;
  - transfers: `date` required / invalidValue (not a real `YYYY-MM-DD` date) / dateTooFar (after today plus one year); `fromAccountId`, `toAccountId` required / unknownAccount (not the user's) / archivedAccount; `toAccountId` sameAccount; `fromAmount` required / invalidValue (not an integer or above the max) / positiveAmount (≤ 0); `rate` invalidValue (present and not a number > 0, or above 1 000 000); `note` invalidValue (present and not a string, or longer than 200 after trim); a converted amount of 0 gives `rate: ["validation.positiveAmount"]`;
  - rates and paging: an invalid `date`, `from`, `to`, `page`, `pageSize` or `accountId` query value gives `validation.invalidValue` under that name.
- Every account and transfer is scoped to the current user. A foreign id answers exactly like an unknown id.
- Stored values never change when rates change: a transfer stores its `rate` and `toAmount`.
- Commit messages: Conventional Commits; never mention Claude, AI or assistants; no `Co-Authored-By`.

## Review Focus

- Deleting a user who has accounts and transfers must succeed and leave no rows behind, never a 500 from the `transfers → accounts` FK. Pinned in Task 1 (`Should_DeleteUserWithAccountsAndTransfers`).
- A transfer created while the other request archives or changes the currency of the same account must not end up on an archived account or across a currency change. Pinned in Task 6 (`Should_RejectTransfer_When_AccountArchivedConcurrently`, using the row lock) and Task 5 (`Should_ReturnCurrencyLocked_When_TransferExists`).
- A huge or tiny manual rate (`1e300`, `1e-9`) or a `fromAmount` at the max with a large rate must give a 400, never a 500 or an overflowed amount. Pinned in Task 6.
- Rates for a Saturday, a public holiday, a future date and a date before the history start must answer the nearest stored rate exactly as `mocks/rates.ts` does. Pinned in Task 2.
- NBP down or unreachable at startup must not stop the app or fail requests; `GET /v1/rates` serves what is stored and the next tick retries. Pinned in Task 3 (`Should_KeepRunning_When_SourceThrows`).

---

## File map

```
src/Mita.Domain/Money/Currency.cs (moved), MinorUnits.cs, ExchangeRate.cs
src/Mita.Domain/Accounts/Account.cs, AccountType.cs, Transfer.cs
src/Mita.Infrastructure/Persistence/Configurations/AccountConfiguration.cs, TransferConfiguration.cs, ExchangeRateConfiguration.cs, AccountMovementConfiguration.cs
src/Mita.Infrastructure/Persistence/ReadModels/AccountMovement.cs
src/Mita.Infrastructure/Persistence/Migrations/<timestamp>_AccountsTransfersRates.cs
src/Mita.Infrastructure/Rates/RatesOptions.cs, IExchangeRateSource.cs, NbpExchangeRateSource.cs, RatesSynchronizer.cs, RatesSyncService.cs
src/Mita.Infrastructure/Caching/GlobalCacheTags.cs
src/Mita.Api/Common/Money/CurrencyWire.cs
src/Mita.Api/Common/Wire/JsonValues.cs, IsoDate.cs
src/Mita.Api/Common/Rates/RateTable.cs
src/Mita.Api/Common/Accounts/AccountDto.cs, AccountWire.cs, AccountQueries.cs, AccountCacheTags.cs, IAccountInput.cs, AccountInputValidator.cs, AccountInputParser.cs
src/Mita.Api/Features/Rates/GetRates/*, RatesEndpoints.cs
src/Mita.Api/Features/Accounts/{ListAccounts,CreateAccount,UpdateAccount,DeleteAccount}/*, AccountsEndpoints.cs
src/Mita.Api/Features/Transfers/{ListTransfers,CreateTransfer}/*, TransfersEndpoints.cs
tests/Mita.Api.UnitTests/{Money,Features/Accounts,Features/Transfers,Features/Rates,Rates}/...
tests/Mita.Api.IntegrationTests/Features/{Accounts,Transfers,Rates}/..., Infrastructure/{TestData.cs,FakeExchangeRateSource.cs}
```

---

### Task 1: Domain model, schema and the ledger view

**Files:** `Mita.Domain/Money/*`, `Mita.Domain/Accounts/*` (move `Currency` and fix usings in Users, Settings and their tests), the four configurations, `AccountMovement`, `AppDbContext` (DbSets `Accounts`, `Transfers`, `ExchangeRates`, `AccountMovements`), migration `AccountsTransfersRates`; tests `tests/Mita.Api.UnitTests/Money/MinorUnitsTests.cs`, `ExchangeRateTests.cs`, `tests/Mita.Api.UnitTests/Accounts/AccountTests.cs`, `tests/Mita.Api.IntegrationTests/Persistence/MigrationTests.cs` (extended), `tests/Mita.Api.IntegrationTests/Infrastructure/TestData.cs`.

**Interfaces:**
- Produces:
  - `namespace Mita.Domain.Money`: `enum Currency { PLN, EUR, USD, UAH }` (unchanged values; stored as strings, so no data change).
  - `static class MinorUnits` with `const long MaxAmount = 100_000_000_000` and `static long Convert(long minor, decimal rate)` (round away from zero).
  - `sealed class ExchangeRate` with `Currency Currency`, `DateOnly Date`, `decimal PlnValue`, `DateTimeOffset FetchedAt`, plus `static IReadOnlyList<Currency> Quoted` (`EUR, USD, UAH`), `static decimal Round(decimal rate)` (6 decimals, away from zero) and `static decimal Cross(decimal fromPln, decimal toPln)`.
  - `enum AccountType { Card, Cash, Savings }`.
  - `sealed class Account : IAuditable` with `Guid Id, Guid UserId, string Name, AccountType Type, Currency Currency, long OpeningBalance, string? CardLast4, bool Archived`, `const int NameMaxLength = 40`, `static Account Create(Guid userId, string name, AccountType type, Currency currency, long openingBalance, string? cardLast4, bool archived)` and `void Update(string name, AccountType type, Currency currency, long openingBalance, string? cardLast4, bool archived)`. Both store `CardLast4` only for `Card`.
  - `sealed class Transfer : IAuditable` with `Guid Id, Guid UserId, DateOnly Date, Guid FromAccountId, Guid ToAccountId, long FromAmount, long ToAmount, decimal Rate, string? Note`, `const int NoteMaxLength = 200`, `static Transfer Create(Guid userId, DateOnly date, Account from, Account to, long fromAmount, decimal rate, string? note)` which sets `ToAmount = MinorUnits.Convert(fromAmount, rate)`.
  - `sealed record AccountMovement(Guid UserId, Guid AccountId, long Amount)`: keyless, `ToView("account_movements")`.
  - Tables:
    - `accounts`: FK to `AspNetUsers` cascade; `Name` varchar(40); `Type` and `Currency` as strings; `CardLast4` varchar(4); index `(UserId, CreatedAt)`.
    - `transfers`: FK to the user cascade; `FromAccountId` and `ToAccountId` FKs `NO ACTION`; `Rate numeric(18,6)`; `Note` varchar(200); indexes `(UserId, Date)`, `FromAccountId`, `ToAccountId`; check constraints `FromAccountId <> ToAccountId`, `FromAmount > 0`, `ToAmount > 0`.
    - `exchange_rates`: PK `(Currency, Date)`, `PlnValue numeric(18,6)`.
  - The migration creates the view with `migrationBuilder.Sql`, column names quoted as EF generates them:

    ```sql
    CREATE VIEW account_movements AS
    SELECT "UserId", "FromAccountId" AS "AccountId", -"FromAmount" AS "Amount" FROM transfers
    UNION ALL
    SELECT "UserId", "ToAccountId", "ToAmount" FROM transfers;
    ```

    `Down` drops the view first.
  - `TestData` (test helper) with `Task<Account> AddAccountAsync(WebApplicationFactory<Program>, Guid userId, Currency currency = PLN, long openingBalance = 0, bool archived = false)`, `Task AddTransferAsync(..., Account from, Account to, long amount)` and `Task AddRatesAsync(..., params (Currency, string Date, decimal PlnValue)[] rows)`, writing through a scoped `AppDbContext`.
- [ ] **Step 1: Failing tests**
  - `Should_RoundHalfAwayFromZero_When_Converting`: `(1, 0.5m) → 1`, `(-1, 0.5m) → -1`, `(10000, 4.2717m) → 42717`, `(3, 0.333333m) → 1`.
  - `Should_CrossThroughPln_When_ComputingRate`: `Cross(4.27m, 3.93m) == 1.086514m`; `Round(0.0000004m) == 0m`.
  - `Should_DropCardLast4_When_AccountIsNotCard`: `Create(..., Cash, ..., "1234", ...)` has `CardLast4 == null`; a `Card` keeps it.
  - `Should_ApplyMigrations_When_DatabaseIsEmpty` (extended): tables `accounts`, `transfers`, `exchange_rates` and view `account_movements` exist.
  - `Should_DeleteUserWithAccountsAndTransfers`: register a user, add two accounts and a transfer through `TestData`, `DELETE /v1/me` gives 204, and no `accounts`, `transfers` or movement rows remain for that user.
- [ ] **Step 2: Run, see them fail.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/be-run.sh 'dotnet test --filter "FullyQualifiedName~MinorUnits|FullyQualifiedName~ExchangeRateTests|FullyQualifiedName~AccountTests|FullyQualifiedName~MigrationTests|FullyQualifiedName~DeleteUserWithAccounts"'`
- [ ] **Step 3: Implement, then add the migration** with `dotnet ef migrations add AccountsTransfersRates --project src/Mita.Infrastructure --startup-project src/Mita.Api`, and append the view SQL by hand.
- [ ] **Step 4: Run, see them pass,** then run the full suite (the `Currency` move touches settings).
- [ ] **Step 5: Commit** — `feat: accounts, transfers and exchange rates schema with the ledger view`

### Task 2: Rate lookup and `GET /v1/rates`

**Files:** `Common/Money/CurrencyWire.cs` (`SettingsWire` delegates its currency members to it), `Common/Wire/IsoDate.cs`, `Common/Rates/RateTable.cs`, `Common/Caching/ICachedQuery.cs`, `Common/Caching/CacheKeys.cs`, `Behaviors/CachingBehavior.cs`, `Infrastructure/Caching/GlobalCacheTags.cs`, `Infrastructure/Caching/RedisOptions.cs` (optional `InstanceName`), `Features/Rates/GetRates/{GetRatesQuery,GetRatesValidator,GetRatesHandler,GetRatesEndpoint,RateDto}.cs`, `Features/Rates/RatesEndpoints.cs`, `Program.cs`; tests `tests/Mita.Api.UnitTests/Features/Rates/GetRatesValidatorTests.cs`, `tests/Mita.Api.UnitTests/Behaviors/CachingBehaviorTests.cs` (extended), `tests/Mita.Api.IntegrationTests/Features/Rates/RatesTests.cs`, `Infrastructure/MitaApiFactory.cs`.

**Interfaces:**
- Consumes: `ExchangeRate`, `TestData.AddRatesAsync` (Task 1).
- Produces:
  - `static class CurrencyWire` with `string ToWire(Currency)`, `bool TryParse(string?, out Currency)` (exact upper case) and `bool TryParse(JsonElement?, out Currency)`.
  - `static class IsoDate` with `bool TryParse(string?, out DateOnly)` (`yyyy-MM-dd`, invariant, real calendar date) and `string ToWire(DateOnly)`.
  - `static class RateTable`:
    - `Task<IReadOnlyDictionary<Currency, decimal>> PlnValuesAsync(this AppDbContext db, DateOnly date, CancellationToken ct)`: PLN is 1; every quoted currency with any stored row gets the lookup from Decisions; currencies without rows are absent;
    - `decimal? Rate(IReadOnlyDictionary<Currency, decimal> plnValues, Currency from, Currency to)`: 1 when equal, `null` when a side is absent, else `ExchangeRate.Cross`.
  - `ICachedQuery.IsUserScoped` (default `true`). For `false`, `CachingBehavior` uses key `CacheKeys.Global(key)` = `g:{key}` and tags `g:{tag}` without the user tag.
  - `GlobalCacheTags.Rates = "g:rates"`, equal to `CacheKeys.Global(RatesCacheTag)`.
  - `RedisOptions.InstanceName` (default `"mita:"`, configuration `Redis:InstanceName`) replaces the `CacheDefaults.InstanceName` constant in `AddStackExchangeRedisCache`.
  - `MitaApiFactory.WithIsolatedData()` returns a factory on `postgres.NewDatabaseConnectionString()` with a unique `Redis:InstanceName`. Global rows such as rates are tested only through it.
  - `MitaApiFactory` sets `Rates:SyncEnabled = false` (the option arrives in Task 3; until then the key is ignored).
  - `sealed record GetRatesQuery(string? Date, string? From, string? To) : IRequest<Result<IReadOnlyList<RateDto>>>, ICachedQuery` with `IsUserScoped = false`, key `rates:{Date ?? "today"}:{From ?? "*"}:{To ?? "*"}`, tag `rates`, expiration 1 hour.
  - `sealed record RateDto(string Date, string From, string To, double Value)`.
  - `GET /v1/rates` (policy `User`): sources × targets in the order `PLN, EUR, USD, UAH`, skipping `from == to` and pairs with a `null` rate; `date` in the response is the requested date (today UTC when omitted), not the effective date of the stored rate.
- [ ] **Step 1: Failing tests**
  - Validator rows: `date` values `"2026-02-30"`, `"2026-1-01"`, `"x"` give `date: invalidValue`; `from` values `"pln"` and `"GBP"` give `from: invalidValue`; same for `to`; all null is valid.
  - `Should_UseGlobalKey_When_QueryIsNotUserScoped`: two users get one factory call; keys start with `g:`.
  - `Should_ReturnTwelvePairs_When_NoFilter`: rows for EUR, USD and UAH on 2026-03-02 give 12 items, none with `from == to`, and `EUR→PLN` equals the stored value.
  - `Should_ReturnCrossRate_When_NeitherSideIsPln`: EUR 4.27 and USD 3.93 give `EUR→USD` 1.086514.
  - `Should_UseLatestEarlierRate_When_DateIsWeekendOrFuture`: rows on Friday 2026-03-06 only; `date=2026-03-08` and `date=2027-01-01` both answer the Friday value, with `date` echoing the request.
  - `Should_UseEarliestRate_When_DateBeforeHistory`: `date=2020-01-01` answers the earliest stored value.
  - `Should_OmitPairs_When_CurrencyHasNoRates`: only EUR rows; `to=PLN` gives `[EUR→PLN]` only.
  - `Should_Return401_When_NotAuthenticated`.
  - `Should_DefaultToTodayUtc_When_DateOmitted`: every item's `date` is today's UTC date.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: exchange rates endpoint with a global cache entry`

### Task 3: NBP source and the background sync

**Files:** `Directory.Packages.props` (`Microsoft.Extensions.Http.Resilience`, latest 10.x, pinned), `Infrastructure/Rates/{RatesOptions,IExchangeRateSource,NbpExchangeRateSource,RatesSynchronizer,RatesSyncService}.cs`, `Infrastructure/DependencyInjection.cs` (`AddRates`), `appsettings.json` (`Rates` section), `.env.example` (`Rates__SyncEnabled`); tests `tests/Mita.Api.UnitTests/Rates/NbpExchangeRateSourceTests.cs` (fixture JSON inline), `tests/Mita.Api.UnitTests/Configuration/OptionsValidatorTests.cs` (extended), `tests/Mita.Api.IntegrationTests/Rates/RatesSynchronizerTests.cs`, `Infrastructure/FakeExchangeRateSource.cs`.

**Interfaces:**
- Consumes: `ExchangeRate`, `GlobalCacheTags.Rates` (Tasks 1 and 2).
- Produces:
  - `sealed class RatesOptions` (section `Rates`) with `bool SyncEnabled = true`, `TimeSpan SyncInterval = 6h` (≥ 1 minute), `int HistoryDays = 183` (1..3650) and `Uri NbpBaseUrl = https://api.nbp.pl/api/` (absolute, https). Validated on start by `RatesOptionsValidator`.
  - `sealed record PlnRate(Currency Currency, DateOnly Date, decimal Value)`.
  - `interface IExchangeRateSource { Task<IReadOnlyList<PlnRate>> FetchAsync(DateOnly from, DateOnly to, CancellationToken ct); }`
  - `sealed class NbpExchangeRateSource(HttpClient http) : IExchangeRateSource`:
    - splits the range into windows of at most 93 days;
    - calls `exchangerates/tables/A/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}/?format=json` per window;
    - reads `[{ effectiveDate, rates: [{ code, mid }] }]` and keeps `EUR`, `USD` and `UAH`;
    - treats 404 (no data in the window) as empty;
    - throws `HttpRequestException` on other failures, after the resilience handler gives up.
  - Registration: `AddHttpClient<IExchangeRateSource, NbpExchangeRateSource>` with `BaseAddress = NbpBaseUrl` and `.AddStandardResilienceHandler()` set as in Decisions.
  - `sealed class RatesSynchronizer(AppDbContext db, IExchangeRateSource source, HybridCache cache, TimeProvider time, IOptions<RatesOptions> options)` (scoped) with `Task<int> SyncAsync(CancellationToken ct)`:
    - `today` is the UTC date;
    - `start` is `today - HistoryDays` when any quoted currency has no row, else the oldest of the per-currency latest dates plus one day;
    - returns 0 without calling the source when `start > today`;
    - inserts with `INSERT … ON CONFLICT DO NOTHING` and `FetchedAt = now`;
    - when rows were inserted, removes tag `GlobalCacheTags.Rates` (a cache failure is logged, not thrown);
    - returns the inserted count.
  - `sealed class RatesSyncService : BackgroundService`: when `SyncEnabled`, calls `SyncAsync` in a new scope at start and on a `PeriodicTimer(SyncInterval, TimeProvider)`, and logs any exception as a warning and continues.
- [ ] **Step 1: Failing tests**
  - `Should_ParseMidRates_When_NbpAnswersTable`: a two-day fixture with EUR 4.2717, USD 3.9312, UAH 0.0948 and GBP gives six `PlnRate`s, without GBP.
  - `Should_ReturnEmpty_When_NbpAnswers404`.
  - `Should_SplitIntoWindows_When_RangeExceeds93Days`: 2026-01-01..2026-07-01 gives the request paths `2026-01-01/2026-04-03`, `2026-04-04/2026-07-01` (each ≤ 93 days, contiguous).
  - `Should_RetryOnce_When_NbpAnswers503ThenOk`: the client resolved from DI with a stub primary handler makes 2 calls and returns the rates.
  - Options rows: `SyncInterval` 30 s, `HistoryDays` 0, and an `http://` URL are each rejected.
  - `Should_BackfillHistory_When_TableIsEmpty` (isolated data, `FakeExchangeRateSource`, `ManualTimeProvider` at 2026-10-18): the source is asked for `2026-04-18..2026-10-18`, and the rows are stored.
  - `Should_FetchOnlyNewDays_When_HistoryExists`: the second run with time advanced 2 days asks for `2026-10-19..2026-10-20`.
  - `Should_IgnoreDuplicates_When_RowsAlreadyStored`: overlapping rows give no error and no duplicates.
  - `Should_InvalidateRatesCache_When_RowsInserted`: `GET /v1/rates?to=PLN` before and after a sync that adds a newer day shows the new value without waiting for expiry.
  - `Should_KeepRunning_When_SourceThrows`: with a throwing source, the service logs a warning, the host stays up, and `GET /v1/rates` still answers 200 from stored rows.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: sync nbp exchange rates in the background with polly resilience`

### Task 4: List and create accounts

**Files:** `Common/Wire/JsonValues.cs`, `Common/Accounts/{AccountDto,AccountWire,AccountQueries,AccountCacheTags,IAccountInput,AccountInputValidator,AccountInputParser}.cs`, `Features/Accounts/ListAccounts/*`, `Features/Accounts/CreateAccount/*`, `Features/Accounts/AccountsEndpoints.cs`, `Program.cs`; tests `tests/Mita.Api.UnitTests/Features/Accounts/CreateAccountValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Accounts/AccountsTests.cs`.

**Interfaces:**
- Consumes: `Account`, `AccountMovement`, `CurrencyWire` (Tasks 1 and 2), `PageResponse<T>`, `PagingExtensions.ToPageAsync`.
- Produces:
  - `static class JsonValues` with:
    - `bool IsMissing(JsonElement?)`: null, undefined, or a whitespace-only string;
    - `bool TryGetString(JsonElement?, out string)`;
    - `bool TryGetMinorAmount(JsonElement?, out long)`: a JSON number with no fraction and `|value| ≤ MinorUnits.MaxAmount`;
    - `bool TryGetBoolean(JsonElement?, out bool)`.
  - `sealed record AccountDto(Guid Id, string Name, string Type, string Currency, long OpeningBalance, long Balance, string? CardLast4, bool Archived, bool InUse, int LinkedGoals)`.
  - `static class AccountWire` with `ToWire(AccountType)` (`card`, `cash`, `savings`) and `TryParse(JsonElement?, out AccountType)`.
  - `AccountQueries.ProjectAccounts(this AppDbContext db, Guid userId) : IQueryable<AccountDto>`:
    - no tracking, ordered by `CreatedAt, Id`;
    - `Balance = OpeningBalance + (AccountMovements.Where(AccountId == id).Sum(m => (long?)m.Amount) ?? 0)`;
    - `InUse = AccountMovements.Any(AccountId == id)`;
    - `LinkedGoals = 0`.
  - `AccountCacheTags.Accounts = "accounts"`, `AccountCacheTags.Transfers = "transfers"`.
  - `interface IAccountInput` with `JsonElement? Name, Type, Currency, OpeningBalance, CardLast4, Archived`.
  - `abstract class AccountInputValidator<T> : AbstractValidator<T> where T : IAccountInput`: the account rules from Global Constraints, except `currencyLocked`, with `CascadeMode.Stop` per rule.
  - `AccountInputParser.Parse(IAccountInput) → ParsedAccount(string Name, AccountType Type, Currency Currency, long OpeningBalance, string? CardLast4, bool? Archived)`: the name is trimmed, and a blank `cardLast4` is null.
  - `ListAccountsQuery(int Page, int PageSize) : ICachedQuery`: key `accounts:{page}:{pageSize}`, tag `accounts`. Validator: page ≥ 1, pageSize 1..100 (as `PageRequestValidator`). The endpoint binds `int page = 1, int pageSize = 100`.
  - `CreateAccountCommand : IAccountInput, IInvalidatesCache` (tag `accounts`). `POST /v1/accounts` answers 201, `Location: /v1/accounts/{id}`, body `AccountDto`.
- [ ] **Step 1: Failing tests**
  - Validator rows:
    - `name`: missing, `"   "` → required; `5` → invalidValue; 41 characters after trim → nameTooLong; `"a\u0007"` → invalidValue;
    - `type`: missing → required; `"bank"` → invalidValue;
    - `currency`: `""` → required; `"GBP"`, `"pln"` → invalidValue;
    - `openingBalance`: missing → required; `"100"`, `1.5`, `100000000001` → invalidValue; `-100000000000` and `5.0` are valid;
    - `cardLast4`: `"12a4"`, `"12345"`, `1234` → cardLast4; `null` and `""` are valid;
    - `archived`: `"yes"` → invalidValue.
  - `Should_ReturnEmptyPage_When_UserHasNoAccounts`: `{ items: [], page: 1, pageSize: 100, total: 0 }`.
  - `Should_CreateAccount_When_InputValid`: 201, `Location` set, the property set equals the contract's ten names, `balance == openingBalance`, `inUse == false`, `linkedGoals == 0`, `archived == false`, and the name is trimmed.
  - `Should_DropCardLast4_When_TypeIsNotCard`: cash with `"1234"` stores `null`.
  - `Should_ListInCreationOrder_And_IsolateUsers`: user A's three accounts are in order; user B sees none.
  - `Should_ComputeBalanceFromMovements`: after `TestData.AddTransferAsync(a → b, 2500)`, the list shows `a.balance = opening - 2500`, `b.balance = opening + 2500`, and both `inUse`.
  - `Should_Return400_When_PagingInvalid`: `pageSize=101`, `page=0`, `page=abc` → `validation.invalidValue` under the parameter name.
  - `Should_ShowNewAccount_When_ListWasCached`: GET, POST, GET shows it (tag invalidated).
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: list and create accounts with server-computed balances`

### Task 5: Update and delete accounts

**Files:** `Features/Accounts/UpdateAccount/*` (including `UpdateAccountCurrencyValidator.cs`), `Features/Accounts/DeleteAccount/*`; tests `tests/Mita.Api.UnitTests/Features/Accounts/UpdateAccountValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Accounts/UpdateDeleteAccountTests.cs`.

**Interfaces:**
- Consumes: `AccountInputValidator<T>`, `AccountInputParser`, `ProjectAccounts`, `AccountCacheTags` (Task 4); `TestData` (Task 1).
- Produces:
  - `UpdateAccountCommand(Guid Id, …IAccountInput) : IInvalidatesCache` (tag `accounts`).
  - `PUT /v1/accounts/{id:guid}` answers 200 with `AccountDto`, or 404 for an unknown or foreign id. An omitted `archived` keeps the stored value.
  - `UpdateAccountCurrencyValidator(AppDbContext, ICurrentUser)`: `currency: validation.currencyLocked` when the currency parses, the account exists for the user, it is in use, and the currency differs. It is skipped otherwise, so there is never a second code on `currency`.
  - The handler runs in a transaction and loads the row with `FOR UPDATE`. When the currency changes, it re-checks `inUse` under the lock and returns the same validation error on a race.
  - `DeleteAccountCommand(Guid Id) : IInvalidatesCache` (tag `accounts`). `DELETE /v1/accounts/{id:guid}` answers 204, 404 for an unknown or foreign id, or 400 `id: ["validation.accountInUse"]` from `DeleteAccountValidator` when the account is in use. A `PostgresException` with SQL state `23503` on save maps to the same 400.
- [ ] **Step 1: Failing tests**
  - Validator: the shared rows from Task 4 still hold for the update command (one theory reused through `IAccountInput`).
  - `Should_UpdateAllFields_When_InputValid`, then GET shows the change (cache invalidated).
  - `Should_KeepArchived_When_ArchivedOmitted`, and `Should_ArchiveAndRestore`.
  - `Should_ChangeCurrency_When_AccountNotInUse`.
  - `Should_ReturnCurrencyLocked_When_TransferExists`, while `Should_AllowSameCurrency_When_TransferExists` succeeds.
  - `Should_Return404_When_AccountBelongsToAnotherUser`, for both PUT and DELETE.
  - `Should_DeleteAccount_When_NotInUse`: 204, and the list no longer contains it.
  - `Should_ReturnAccountInUse_When_DeletingAccountWithTransfers`: 400 `id: ["validation.accountInUse"]`, and the account still exists.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: update, archive and delete accounts with history rules`

### Task 6: Transfers

**Files:** `Common/Error.cs` and `Common/ErrorProblemResult.cs` (`ErrorType.Unavailable`), `Features/Transfers/ListTransfers/*`, `Features/Transfers/CreateTransfer/*` (including `CreateTransferAccountsValidator.cs`), `Features/Transfers/TransfersEndpoints.cs`, `Program.cs`; tests `tests/Mita.Api.UnitTests/Common/ResultHttpExtensionsTests.cs` (extended), `tests/Mita.Api.UnitTests/Features/Transfers/CreateTransferValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Transfers/TransfersTests.cs`.

**Interfaces:**
- Consumes: `Transfer.Create`, `MinorUnits`, `ExchangeRate.Round` (Task 1); `RateTable`, `IsoDate`, `MitaApiFactory.WithIsolatedData` (Task 2); `JsonValues`, `AccountCacheTags` (Task 4).
- Produces:
  - `ErrorType.Unavailable` and `Error.Unavailable(string code)` → 503 `{ title: "Service Unavailable", code }`.
  - `sealed record TransferDto(Guid Id, string Date, Guid FromAccountId, Guid ToAccountId, long FromAmount, long ToAmount, double Rate, string? Note)`.
  - `ListTransfersQuery(string? AccountId, int Page, int PageSize) : ICachedQuery`: key `transfers:{accountId ?? "*"}:{page}:{pageSize}`, tag `transfers`. The endpoint defaults are 1 and 20, max 100. A non-GUID `accountId` is `invalidValue`. Order as in Decisions.
  - `CreateTransferCommand(JsonElement? Date, FromAccountId, ToAccountId, FromAmount, Rate, Note) : IInvalidatesCache` (tags `transfers`, `accounts`).
  - `CreateTransferValidator`: the field rules from Global Constraints that need no database. "Today" comes from `TimeProvider`; a non-GUID account id is `unknownAccount`.
  - `CreateTransferAccountsValidator(AppDbContext, ICurrentUser)` adds `unknownAccount` / `archivedAccount` per side, then `toAccountId: sameAccount`. It only runs for sides that passed the sync rules.
  - Handler steps:
    1. In a transaction, lock both accounts `FOR UPDATE` in id order, and re-check ownership and archived (same codes on a race).
    2. Pick the rate: `1` for equal currencies (a given rate is ignored); else `ExchangeRate.Round(rate)` when given; else `RateTable.Rate(PlnValuesAsync(date))`, and `null` → `Error.Unavailable("rates.unavailable")`.
    3. `toAmount ≤ 0` → `rate: ["validation.positiveAmount"]`.
    4. Insert, commit. `POST /v1/transfers` answers 201 with `TransferDto` and no `Location` (there is no single-transfer route).
- [ ] **Step 1: Failing tests**
  - `Should_Return503WithCode_When_ErrorUnavailable` (`ResultHttpExtensions`).
  - Validator rows:
    - `date`: missing → required; `"2026-02-30"`, `"18.10.2026"` → invalidValue; today + 1 year + 1 day → dateTooFar, today + 1 year is valid;
    - `fromAmount`: `0`, `-5` → positiveAmount; `1.5`, `"10"`, `100000000001` → invalidValue;
    - `rate`: `0`, `-1`, `"4"`, `1000001`, `1e300` → invalidValue; `1e-9` passes validation;
    - `note`: 201 characters after trim, `5`, `"a\u0000"` → invalidValue;
    - `fromAccountId`: missing → required; `"acc_x"` → unknownAccount.
  - `Should_CreateTransfer_When_SameCurrency`: rate 1, `toAmount == fromAmount`, a trimmed note, and an empty note is `null`; account balances change on both sides and both are `inUse`.
  - `Should_UseStoredRate_When_CurrenciesDiffer` (isolated data, EUR 4.2717 on the transfer date): 10000 EUR→PLN gives rate 4.2717, toAmount 42717. `Should_UseManualRate_When_Given`: 4.5 gives 45000.
  - `Should_ReturnPositiveAmount_When_ConvertedAmountIsZero`: rate `1e-9` gives `rate: ["validation.positiveAmount"]`.
  - `Should_StoreConvertedAmount_When_MaxAmountAndMaxRate`: PLN→EUR, `fromAmount` 100000000000 with rate 1000000 gives 201 and `toAmount` 100000000000000000.
  - `Should_Return503_When_NoRatesAndNoManualRate` (isolated data, empty rates): `code == "rates.unavailable"`.
  - `Should_ReturnFieldErrors_When_AccountsInvalid`: a foreign account gives unknownAccount, an archived one gives archivedAccount, the same id twice gives `toAccountId: sameAccount`.
  - `Should_RejectTransfer_When_AccountArchivedConcurrently`: archive through PUT and create the transfer in parallel, 20 times on fresh accounts; every archive answers 200, every transfer answers 201 or 400 `fromAccountId: ["validation.archivedAccount"]`, and none answers 500.
  - `Should_ListNewestFirst_And_FilterByAccount`: three transfers on two dates; the order is date desc, then later insert first; `accountId` matches either side; another user sees none; the default `pageSize` is 20.
  - `Should_Return400_When_AccountIdNotGuid`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: transfers between accounts with rate resolution`

### Task 7: API contract, OpenAPI and the Docker smoke

**Files:** `docs/api-contract.md` (endpoint rows, `Account`, `Transfer`, `Rate` types, the new validation codes, 503 `rates.unavailable`, a note that rates come from NBP, that 404 has precedence only for valid bodies, and the rates array exception), `README.md` (`Rates__SyncEnabled=false` for offline work), `tests/Mita.Api.IntegrationTests/OpenApiTests.cs`.
- [ ] **Step 1: Failing test** — `Should_DescribeAccountsTransfersAndRates`: the document contains `/v1/accounts`, `/v1/accounts/{id}`, `/v1/transfers` and `/v1/rates` with tags Accounts, Transfers, Rates.
- [ ] **Step 2: Implement, run, see it pass;** Docker smoke:
  1. `docker compose up -d --build`;
  2. register with a curl cookie jar;
  3. `GET /v1/rates?to=PLN` gives 200 with three items once the first sync has run (the log line from `RatesSyncService` shows the inserted count);
  4. create two PLN accounts, transfer 2500 between them, and the list shows the balances;
  5. `DELETE` of the used account gives 400 `accountInUse`; archive it through PUT and get 200;
  6. `docker compose down -v`.
- [ ] **Step 3: Commit** — `docs: api contract for accounts, transfers and exchange rates`

### Task 8: Frontend hybrid mode against the real accounts, transfers and rates

Frontend repo, branch `feature/hybrid-accounts-and-rates` from `origin/develop`.

**Files:** `.env.example` (`NUXT_PUBLIC_API_REAL_PATHS=/v1/auth,/v1/me,/v1/settings,/v1/rates`), `CLAUDE.md` ("Running against the backend": `/v1/rates` is real by default; `/v1/accounts,/v1/transfers` are opt-in because mocked transactions and goals still reference mock account ids), `tests/e2e-hybrid/auth.hybrid.spec.ts` (the `/accounts` check asserts the heading "Accounts" instead of the demo account "Konto główne"), new `tests/e2e-hybrid/accounts.hybrid.spec.ts`; outside the repo `dock.mita-work/scripts/al-hybrid-smoke.sh` (`NUXT_PUBLIC_API_REAL_PATHS=/v1/auth,/v1/me,/v1/settings,/v1/rates,/v1/accounts,/v1/transfers`).
- [ ] **Step 1: Write `accounts.hybrid.spec.ts`:**
  1. register a unique user, and `/accounts` shows "No accounts yet";
  2. add "Cash" (PLN, 100.00) and "Card" (PLN, 0.00, last 4 `4821`);
  3. transfer 25.00 Cash → Card, and the cards show 75.00 and 25.00, and the transfers list shows the route;
  4. "Cash" offers Archive and no Delete, then archive it, and it moves under "Show archived (1)";
  5. delete the user in Settings.
- [ ] **Step 2: Run** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/scripts/al-hybrid-smoke.sh` (backend branch from Tasks 1-7 checked out); both specs pass.
- [ ] **Step 3: Run** `bun run lint && bun run typecheck && bun run test` in the frontend; all green.
- [ ] **Step 4: Commit** — `test: hybrid e2e against real accounts, transfers and rates`

### Task 9: Pull requests

- [ ] Backend:
  1. Rebase on `origin/develop` and fold fix commits into their task commits.
  2. Run `dotnet build -warnaserror` and `dotnet test --logger junit` (Docker running); all must be green.
  3. Push, open a PR to `develop` (plain description), wait for `check-source-branch`, then `gh pr merge --rebase`.
- [ ] Frontend: same flow for `feature/hybrid-accounts-and-rates` after the backend PR is merged.
