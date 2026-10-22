# Currency List Implementation Plan

**Goal:** Every currency NBP publishes in tables A and B (147 currencies plus PLN) can be chosen wherever a currency is chosen today (accounts, transactions, transfers through accounts, goal contributions through accounts), with ISO 4217 minor units, table B rates synced weekly, rate lookups that stay correct for tiny and huge rates, and a searchable picker; the base currency stays PLN, EUR, USD or UAH.

**Architecture:**
- **Domain:** `Currency` stays an enum whose member names are the ISO codes (the wire value is the name). `Currencies` holds the list, the base subset and parsing; `MinorUnits` knows each currency's ISO 4217 exponent and converts minor units between currencies; `ExchangeRate.Round` keeps 6 significant digits for small rates.
- **Persistence:** one migration widens every rate column to `numeric(24,12)` and adds two SQL functions, `currency_exponent(text)` and `round_rate(numeric)`, so the SQL conversions (base currency change, goal and budget re-pricing) compute exactly what C# computes.
- **Rates:** the NBP source fetches table A or table B; the synchronizer keeps a start date per table and backfills a table when one of its currencies has no rows. Rate lookups read every currency in one query. Writes and `GET /v1/rates` ignore a rate older than 30 days; reads that value existing data never do.
- **Frontend:** a shared currency catalog (`app/shared/money/currencies.ts`) mirrors the backend list and exponents; every money helper takes the currency; a shared `CurrencySelect` (search, "Your currencies" group, names from `Intl.DisplayNames`) replaces the account, transaction and base-currency pickers. The mock backend implements the same rules.

**Tech Stack:** Backend: .NET 10, EF Core migrations (Npgsql), MediatR 12.5.0, FluentValidation, xUnit v3, Shouldly, Moq, Bogus, Testcontainers. Frontend: Nuxt 4, PrimeVue 4 (`Select` with `filter` and option groups), TanStack Query, Vuelidate, `Intl.DisplayNames`/`Intl.NumberFormat`, bun:test, Playwright 1.64.1. No new packages in either repo.

**Spec:** `dock.mita.frontend/docs/superpowers/specs/2026-10-22-tools-design.md`, section "Currencies", constraint "Money", Delivery item 1. Also binding: both repos' project guides and `docs/STACK.md`, backend `docs/api-contract.md` ("Accounts, transfers and rates", "Transactions", "Errors"), `src/Mita.Domain/Money/*`, `src/Mita.Api/Common/{Money,Rates,Settings,Transactions}/*`, `src/Mita.Infrastructure/Rates/*`; frontend `app/shared/{money,rates,api}/*`, `app/features/settings/utils/options.ts`, `app/features/transactions/utils/{keypad,csv}.ts`, `mocks/{rates,validation,db}.ts`, `mocks/seed/rates.ts`, `mocks/handlers/{rates,users}.ts`.

## Decisions

| Topic | Decision | Cost if wrong |
|---|---|---|
| Source of the list | Static: the codes of NBP tables A and B as published on 2026-10-21/22, compiled into the `Currency` enum and the frontend `CURRENCIES` tuple. Not loaded from NBP at run time | A currency NBP adds later needs a release of both repos (the sync logs it until then) |
| XDR | Excluded: it is a unit of account, not a currency anyone holds. Ignored by the sync without a warning | Nobody can record SDR amounts |
| List order | `PLN, EUR, USD, UAH`, then the other 144 codes alphabetically, in both repos. Every `Currency` column stays stored as its 3-letter string | None |
| Base currency | Still only `PLN, EUR, USD, UAH` (`Currencies.Base`). `PUT /v1/settings` with any other supported code → `baseCurrency: ["validation.invalidValue"]`; `UserSettings.BaseCurrency` throws `ArgumentException` for a non-base value | None (spec) |
| Wire parsing | Exactly three upper-case ASCII letters naming an enum member; `jpy`, `" JPY"`, `"1"` and `XDR` are `validation.invalidValue` | Clients sending lower case are refused, as today |
| Minor units | ISO 4217 exponent: 0 for `BIF CLP DJF GNF ISK JPY KMF KRW PYG RWF UGX VND VUV XAF XOF XPF`, 3 for `BHD IQD JOD KWD LYD OMR TND`, 2 for the rest (all base currencies are 2) | A wrong entry misprices that currency by a factor of 10 or 100 |
| Rate meaning | A rate stays "major unit to major unit" (1 JPY = 0.024764 PLN), as NBP publishes it. Converting minor units: `round_half_away(minor × rate × 10^(exponent(to) − exponent(from)))` | None |
| Rate precision | `ExchangeRate.Round`: 6 decimals; below 0.1, one more decimal per leading zero (6 significant digits), at most 12 decimals, half away from zero. Columns `exchange_rates.PlnValue`, `transactions.Rate`, `transfers.Rate` become `numeric(24,12)`. Rates of 0.1 and above round exactly as before | Stored values gain digits; a UAH→EUR rate now has 7 decimals instead of 6 |
| Manual rate | `0 < rate <= 1 000 000 000` (was 1 000 000; KWD→IRR is about 6 000 000), rounded by `ExchangeRate.Round` | None |
| Amount limit | Stays 100 000 000 000 minor units for every currency | 1 billion IRR (~2 000 zł) or 100 million KWD is the largest single amount |
| Rate lookup | Unchanged rule: the latest stored rate on or before the date, the earliest stored rate for a date before the history; one SQL query for all currencies instead of two per currency | None |
| Maximum staleness | `ExchangeRate.MaxAgeDays = 30`. On writes that convert automatically (transactions, transfers, contributions) and in `GET /v1/rates`, a rate older than 30 days before `min(date, today)` counts as missing (503 `rates.unavailable` / pair omitted; a manual rate still works for the entered-to-account step). Valuations of stored data (overview balance, budgets, goals, base currency change, budget copy) use the latest rate whatever its age, so a delisted currency never breaks a read | An account in a currency NBP dropped more than 30 days ago cannot take new automatic conversions |
| Delisted currency | Stays in the list and in every existing record; no automatic removal | A dead currency keeps showing in the picker |
| Table B sync | Same background job, same `Rates:HistoryDays` (183) and interval; per table: start = latest stored date among the table's currencies + 1, or `today − HistoryDays` when any currency of the table has no row (this also backfills the new table A currencies on the first run after deploy); one table failing does not stop the other, and `SyncAsync` rethrows only when both failed. Rows are inserted in one statement per fetched window | A currency in the list that NBP never publishes makes its table refetch the whole window every run (logged) |
| Unknown NBP codes | Skipped; one warning per code per sync (`NBP table {Table} has rates for unsupported currency {Code}`); XDR silent | None |
| `GET /v1/rates` without filters | At least one of `from`/`to` is required: neither → `to: ["validation.required"]`. The answer has at most 147 pairs, still a plain array | A client listing every pair breaks (the frontend never does) |
| Amount search | The entered amount is written with its currency's digits: `1500` for JPY, `12.500` for KWD, `12.50` otherwise; the search text may have up to 3 fraction digits | None |
| Currency names | `Intl.DisplayNames(locale, { type: 'currency' })`, falling back to the code; no translation keys | A browser without a name shows the bare code |
| Picker | Shared `CurrencySelect`: PrimeVue `Select` with filter on code and name; when it offers more than 10 currencies, a group "Your currencies" (recently picked first, then the base currency and the currencies of the user's accounts, at most 8) above "All currencies" (the rest, by code). Option label `CODE — Name`; the selected value shows the code only in compact fields. Recent picks live in `localStorage` (`mita.recentCurrencies`, 5, wrapped in try/catch) | Recent picks do not follow the user across devices |
| Base currency picker | The same component with the four base currencies, no groups; labels change from `PLN — zł` to `PLN — Polish Zloty` | Two E2E assertions change |
| Formatting | `Intl.NumberFormat` with `currencyDisplay: 'narrowSymbol'` and `minimumFractionDigits = maximumFractionDigits = exponent` (CLDR disagrees with ISO for IQD and others); amount inputs and the keypad allow exactly `exponent` fraction digits (no decimal key for 0) | None |
| CSV | Each amount column uses the digits of its own currency (`amount`: entered, `accountAmount`: account, `baseAmount`: base) | Spreadsheets see `1500` instead of `1500.00` for JPY |
| Mock rates | EUR, USD, UAH daily as today; every other currency on Wednesdays only, from a level table with the NBP mid of 2026-10-21/22 and the existing drift; `MockState.version` becomes 2 so stored mock data is reseeded | Developer mock data resets once |

## Global Constraints

- Each repo's project guide and `docs/STACK.md` are binding. No new packages. MediatR stays 12.5.0.
- Backend: warnings are errors; `Api → Infrastructure → Domain`; records, `sealed`, file-scoped namespaces, no comments; `CancellationToken` everywhere; first failing rule per field; validation (400) before lookup (404); never edit an applied migration; tests `Should_X_When_Y`; tests never call the real NBP API (stub `HttpMessageHandler` or `FakeExchangeRateSource`).
- Frontend: Biome clean, `bun run typecheck` 0 `error TS`, `bun test tests/unit` green, no `any`, every string through i18n in `en`, `pl`, `ru`, `uk`, inputs labelled, keyboard reachable, touch targets ≥ 44px.
- Contract, byte for byte between backend and mocks: `Currency` = the 148 codes; `BaseCurrency = 'PLN' | 'EUR' | 'USD' | 'UAH'` for `Settings.baseCurrency` and `SettingsInput.baseCurrency`; amounts in ISO 4217 minor units; `MANUAL_RATE_MAX = 1_000_000_000`; `AMOUNT_MINOR_MAX = 100_000_000_000`; rate rounding and the 30-day staleness as in Decisions.
- The 148 codes, in order: `PLN EUR USD UAH AED AFN ALL AMD AOA ARS AUD AWG AZN BAM BBD BDT BHD BIF BND BOB BRL BSD BWP BYN BZD CAD CDF CHF CLP CNY COP CRC CUP CVE CZK DJF DKK DOP DZD EGP ERN ETB FJD GBP GEL GHS GIP GMD GNF GTQ GYD HKD HNL HTG HUF IDR ILS INR IQD IRR ISK JMD JOD JPY KES KGS KHR KMF KRW KWD KZT LAK LBP LKR LRD LSL LYD MAD MDL MGA MKD MMK MNT MOP MRU MUR MVR MWK MXN MYR MZN NAD NGN NIO NOK NPR NZD OMR PAB PEN PGK PHP PKR PYG QAR RON RSD RUB RWF SAR SBD SCR SDG SEK SGD SLE SOS SRD SSP STN SVC SYP SZL THB TJS TMT TND TOP TRY TTD TWD TZS UGX UYU UZS VES VND VUV WST XAF XCD XCG XOF XPF YER ZAR ZMW ZWG`.
- Table A (31): `AUD BRL CAD CHF CLP CNY CZK DKK EUR GBP HKD HUF IDR ILS INR ISK JPY KRW MXN MYR NOK NZD PHP RON SEK SGD THB TRY UAH USD ZAR`; table B: every other code except PLN.
- Commits: Conventional Commits, English; no tool attribution, no `Co-Authored-By`, no "Generated with" lines in commits, pull requests, code or committed files.

## Review Focus

- A 1 500 JPY expense on a PLN account at 0.024764 must store `accountAmount` 3715 (37.15 zł), not 37 or 371 460; a 1.250 KWD transfer to a PLN account at 12.6221 must give 1578 grosze. Pinned in Task 2 (`Should_ConvertBetweenExponents_When_CurrenciesDiffer`, `Should_PriceJpyExpense_On_PlnAccount`) and Task 6 (`convertMinor` rows).
- Tiny PLN values (IRR 0.00000212, VND 0.0001503) must be stored and crossed without collapsing: IRR→KWD must not become 0, VND→PLN must keep 6 significant digits. Pinned in Task 2 (`Round` rows, `Should_StoreTinyPlnValue_Exactly`) and Task 4 (`Should_ParseTableB_When_NbpAnswers`).
- After a base currency change, a transaction re-priced by SQL must carry the same `baseAmount` as a new transaction priced by C# on the same date (rounding and exponents in two languages). Pinned in Task 2 (`Should_MatchCSharpPricing_When_BaseCurrencyChangesWithJpyAccount`, `Should_RoundLikeCSharp_InSql`).
- The first sync after deploy, with only EUR/USD/UAH rows stored, must backfill every table A and table B currency over the whole window, and later syncs must fetch only new days even if one table B currency stays silent. Pinned in Task 4 (`Should_BackfillTable_When_OneOfItsCurrenciesHasNoRows`, `Should_NotDragStartBack_When_ACurrencyStoppedBeingPublished`).
- Typing `1.5` into a JPY amount, or `1.2345` into a KWD amount, must not silently store 1 or 1234: the input allows 0 and 3 fraction digits respectively, and the quick-add keypad has no decimal key for JPY. Pinned in Task 6 (keypad and `toMinor` rows) and Task 8 (E2E "creates a JPY account and a KWD expense").

---

## File map

```
Backend (dock.mita.backend, branch feature/currency-list)
src/Mita.Domain/Money/{Currency,Currencies,MinorUnits,ExchangeRate}.cs
src/Mita.Domain/Users/UserSettings.cs
src/Mita.Infrastructure/Persistence/Configurations/{ExchangeRateConfiguration,TransactionConfiguration,TransferConfiguration}.cs
src/Mita.Infrastructure/Persistence/Migrations/<timestamp>_CurrencyPrecision.cs
src/Mita.Infrastructure/Rates/{NbpTable,IExchangeRateSource,NbpExchangeRateSource,RatesSynchronizer}.cs
src/Mita.Api/Common/Money/CurrencyWire.cs, Common/Settings/SettingsWire.cs, Common/Rates/{RateTable,ManualRate}.cs
src/Mita.Api/Common/Transactions/{TransactionPrice,TransactionWriter,TransactionBaseAmounts}.cs
src/Mita.Api/Common/{Goals/GoalQueries,Goals/GoalView,Budgets/BudgetQueries}.cs
src/Mita.Api/Features/{Transfers/CreateTransfer,Goals/ContributeToGoal,Goals/UpdateGoal,Budgets/CopyBudgets,Categories/DeleteCategory,Overview/GetOverview,Rates/GetRates,Transactions/ListTransactions}/*
docs/api-contract.md
tests/Mita.Api.UnitTests/Money/{CurrenciesTests,MinorUnitsTests,ExchangeRateTests}.cs, Rates/NbpExchangeRateSourceTests.cs, Features/Settings/UpdateSettingsValidatorTests.cs, Features/Rates/GetRatesValidatorTests.cs
tests/Mita.Api.IntegrationTests/Infrastructure/FakeExchangeRateSource.cs, Persistence/{MigrationTests,CurrencyMappingTests}.cs, Rates/RatesSynchronizerTests.cs, Features/{Rates/RatesTests,Money/MinorUnitPricingTests,Settings/SettingsTests,Transactions/ListTransactionsTests}.cs, OpenApiTests.cs

Frontend (dock.mita.frontend, branch feature/currency-list)
app/shared/money/{currencies,money,format,recentCurrencies,currencyOptions}.ts
app/shared/components/CurrencySelect.vue
app/shared/api/contract.ts, app/shared/rates/convert.ts, app/shared/composables/useMoney.ts, app/shared/user/useUserSettings.ts
app/features/accounts/components/{AccountDialog,TransferDialog}.vue, schemas/{accountSchema,transferSchema}.ts
app/features/transactions/components/{TransactionDialog,QuickAddForm,QuickAddPanel}.vue, utils/{keypad,csv}.ts, schemas/transactionSchema.ts
app/features/goals/components/{GoalDialog,GoalContributionDialog,GoalCard}.vue, utils/goalView.ts, schemas/contributionSchema.ts
app/features/budgets/components/BudgetDialog.vue
app/features/settings/components/RegionSection.vue, utils/options.ts
mocks/{db,rates,validation,transactions,transfers,goals,budgets}.ts, mocks/handlers/{rates,users,transactions}.ts, mocks/seed/{rates,rateLevels,transfers,goals}.ts
i18n/locales/{en,pl,ru,uk}.json
tests/unit/shared/{currencies,money,rates,recentCurrencies,currencyOptions}.test.ts, tests/unit/features/{transactions/keypad,transactions/csv,settings/options}.test.ts, tests/unit/mocks/{rates,users,transactions,db}.test.ts
tests/e2e/{currencies,settings,transactionDialog,overview}.spec.ts, tests/e2e-real/{currencies.real,auth.real}.spec.ts
project guide in the repository root ("Real-backend suite")
```

---

### Task 1: Currency list, wire parsing and the base restriction

Backend repo, branch `feature/currency-list`.

**Files:** `Mita.Domain/Money/{Currency,Currencies,ExchangeRate}.cs`, `Mita.Domain/Users/UserSettings.cs`, `Common/Money/CurrencyWire.cs`, `Common/Settings/SettingsWire.cs`, every `ExchangeRate.Quoted` user; tests `tests/Mita.Api.UnitTests/Money/CurrenciesTests.cs`, `Users/UserSettingsTests.cs`, `Features/Settings/UpdateSettingsValidatorTests.cs`, `Features/Accounts/CreateAccountValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Persistence/CurrencyMappingTests.cs`, `Features/Settings/SettingsTests.cs`.

**Interfaces:**
- Produces:
  - `enum Currency` with the 148 members in the Global Constraints order.
  - `static class Currencies`: `IReadOnlyList<Currency> All` (enum order), `IReadOnlyList<Currency> Base = [PLN, EUR, USD, UAH]`, `IReadOnlyList<Currency> Quoted` (all but PLN), `bool IsBase(this Currency currency)`, `bool TryParse(string? code, out Currency currency)` (regex `^[A-Z]{3}$`, then `Enum.TryParse(code, ignoreCase: false)`).
  - `ExchangeRate.Quoted` is removed; callers use `Currencies.Quoted`.
  - `CurrencyWire.ToWire(Currency) => currency.ToString()`; both `TryParse` overloads delegate to `Currencies.TryParse`.
  - `SettingsWire.TryParseCurrency` succeeds only for `IsBase`.
  - `UserSettings.BaseCurrency` setter throws `ArgumentException` for a non-base currency.
- [ ] **Step 1: Failing tests**
  - `CurrenciesTests`: `All` has 148 items, starts with `PLN, EUR, USD, UAH`, the rest is sorted ordinally and equals the Global Constraints list; `Quoted` has 147 and no PLN; `TryParse` rows: `"JPY"`, `"ZWG"`, `"XCG"` → true; `"jpy"`, `" JPY"`, `"XDR"`, `"1"`, `"PLNX"`, `""`, `null` → false.
  - `UserSettingsTests`: setting `BaseCurrency = Currency.JPY` throws `ArgumentException`.
  - `UpdateSettingsValidatorTests`: `baseCurrency` `"JPY"` → `validation.invalidValue`; `"UAH"` → valid.
  - `CreateAccountValidatorTests`: `currency` `"KWD"` → valid, `"kwd"` and `"XDR"` → `validation.invalidValue`.
  - `CurrencyMappingTests.Should_StoreEveryCurrencyAsThreeLetterText`: every property of CLR type `Currency` in the EF model has provider type `string` and max length 3.
  - `SettingsTests.Should_RejectNonBaseCurrency_When_UpdatingSettings`: `PUT /v1/settings` with `JPY` → 400 `baseCurrency: ["validation.invalidValue"]`, settings unchanged.
  - `Should_CreateAccount_When_CurrencyIsJpy` (in the integration file that already posts accounts): `POST /v1/accounts` with `JPY` → 201 and `currency: "JPY"`.
- [ ] **Step 2: Run, see them fail.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/be-run.sh 'dotnet test --filter "FullyQualifiedName~Currencies|FullyQualifiedName~UserSettings|FullyQualifiedName~UpdateSettingsValidator|FullyQualifiedName~CreateAccountValidator|FullyQualifiedName~CurrencyMapping|FullyQualifiedName~SettingsTests"'`
- [ ] **Step 3: Implement, run, see them pass,** then the full backend suite.
- [ ] **Step 4: Commit** — `feat: support every nbp table a and b currency`

### Task 2: ISO minor units and rate precision

**Files:** `Mita.Domain/Money/{MinorUnits,ExchangeRate}.cs`, `Common/Rates/ManualRate.cs`, `Configurations/{ExchangeRateConfiguration,TransactionConfiguration,TransferConfiguration}.cs`, migration `CurrencyPrecision`, `Common/Transactions/{TransactionPrice,TransactionBaseAmounts}.cs`, `Common/Goals/{GoalQueries,GoalView}.cs`, `Common/Budgets/BudgetQueries.cs`, `Features/Budgets/CopyBudgets/CopyBudgetsHandler.cs`, `Features/Categories/DeleteCategory/BudgetReassignment.cs`, `Features/Goals/ContributeToGoal/ContributeToGoalHandler.cs`, `Features/Overview/GetOverview/GetOverviewHandler.cs`, `Mita.Domain/Accounts/Transfer.cs`, `Mita.Domain/Goals/Goal.cs`, `Features/Transactions/ListTransactions/ListTransactionsHandler.cs`; tests `tests/Mita.Api.UnitTests/Money/{MinorUnitsTests,ExchangeRateTests}.cs`, `Features/Transactions/TransactionPriceTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Money/MinorUnitPricingTests.cs`, `Persistence/MigrationTests.cs`, `Features/Transactions/ListTransactionsTests.cs`.

**Interfaces:**
- Consumes: `Currencies` (Task 1).
- Produces:
  - `MinorUnits.Exponent(Currency currency) : int` (the Decisions table); `MinorUnits.Convert(long minor, decimal rate, Currency from, Currency to) : long` replaces the two-argument overload (round half away from zero of `minor × rate × 10^(Exponent(to) − Exponent(from))`). Every call site passes its real currencies, including base-to-base ones.
  - `ExchangeRate.Round(decimal rate) : decimal`, the rule from Decisions:
    ```csharp
    var decimals = 6;
    for (var scaled = Math.Abs(rate); scaled != 0 && scaled < 0.1m && decimals < MaxDecimals; scaled *= 10) decimals++;
    return Math.Round(rate, decimals, MidpointRounding.AwayFromZero);
    ```
    `ExchangeRate.MaxDecimals = 12`; `Cross` keeps using `Round`.
  - `ManualRate.Max = 1_000_000_000m`.
  - Migration `CurrencyPrecision`: `PlnValue`, `transactions.Rate`, `transfers.Rate` → `numeric(24,12)` (`HasPrecision(24, 12)`); SQL functions `currency_exponent(p_currency text) RETURNS int LANGUAGE sql IMMUTABLE` (same lists) and `round_rate(p_rate numeric) RETURNS numeric LANGUAGE plpgsql IMMUTABLE` (same loop); `Down` drops both and restores `numeric(18,6)`.
  - SQL call sites use `round_rate(...)` instead of `round(..., 6)`; `TransactionBaseAmounts` multiplies by `power(10::numeric, currency_exponent({target}) - currency_exponent(a."Currency"))` before the outer `round`.
  - Amount search: `AmountSearch` allows up to 3 fraction digits; the written amount is `Amount` for exponent 0, `Amount / 1000 + "." + pad3(Amount % 1000)` for exponent 3, today's form otherwise (branch on `Currencies` lists of exponent 0 and 3 exposed as `MinorUnits.ZeroDecimal` and `MinorUnits.ThreeDecimal`).
- [ ] **Step 1: Failing tests**
  - `MinorUnitsTests`: `Exponent` → JPY 0, KWD 3, PLN 2, every base currency 2, exactly 16 zero and 7 three-decimal currencies; `Convert` rows: `(1500, 0.024764, JPY, PLN)` → 3715; `(1250, 12.6221, KWD, PLN)` → 1578; `(3715, 40.38, PLN, JPY)` → 1500; `(100, 0.0001503, VND, PLN)` → 2 (100 VND = 0.015 zł → 1.503 grosze → 2); `(1000, 4.2563, EUR, PLN)` → 4256 (unchanged two-decimal behaviour); `(1, 0.5, PLN, PLN)` → 1 (half away from zero).
  - `ExchangeRateTests.Round` rows: `4.256349` → `4.256349`; `1.0913589744` → `1.091359`; `0.1` → `0.1`; `0.0247644` → `0.0247644`; `0.02476449` → `0.0247645`; `0.00000212` → `0.00000212`; `1.6795e-7` (as `0.00000016795m`) → `0.00000016795`; `0.0000000000004` → `0`; `-0.0247645` stays symmetric.
  - `TransactionPriceTests`: 1500 JPY entered on a PLN account with base PLN → `Rate 0.024764`, `AccountAmount 3715`, `BaseAmount 3715`; 1250 KWD on a EUR account (PLN values KWD 12.6221, EUR 4.3685) → `Rate 2.889344`, `AccountAmount 361`.
  - `MigrationTests.Should_ApplyMigrations_When_DatabaseIsEmpty` (extended): the three columns are `numeric(24,12)`; functions `currency_exponent` and `round_rate` exist.
  - `MinorUnitPricingTests` (integration, rates seeded with `TestData.AddRatesAsync`):
    - `Should_StoreTinyPlnValue_Exactly`: storing IRR `0.00000212` reads back `0.00000212`.
    - `Should_PriceJpyExpense_On_PlnAccount`: POST a 1500 JPY expense on a PLN account → `accountAmount` 3715, `baseAmount` 3715; the account balance drops by 3715.
    - `Should_ConvertBetweenExponents_When_CurrenciesDiffer`: a transfer of 1250 from a KWD account to a PLN account → `toAmount` 1578; a transfer of 1500 from a JPY account to a KWD account (JPY 0.024764, KWD 12.6221) → `rate` 0.00196196, `toAmount` 2943.
    - `Should_MatchCSharpPricing_When_BaseCurrencyChangesWithJpyAccount`: a JPY account with three transactions in JPY, PLN and EUR, base PLN; switch the base to EUR; every `baseAmount` equals `TransactionPrice.Compute` for the same inputs with base EUR.
    - `Should_RoundLikeCSharp_InSql`: for 50 Bogus rates between 1e-9 and 1e7 plus the `Round` rows above, `SELECT round_rate(@x)` equals `ExchangeRate.Round(x)`; `currency_exponent` equals `MinorUnits.Exponent` for all 148 codes.
    - `Should_AcceptManualRateUpToOneBillion`: a transfer of 1 (0.001 KWD) from a KWD account to an IRR account with `rate` 1000000000 → 201 and `toAmount` 100000000; `rate` 1000000001 → 400 `rate: ["validation.invalidValue"]`.
  - `ListTransactionsTests.Should_MatchAmountSearch_InCurrencyDigits`: JPY 1500 matches `search=1500` and not `15.00`; KWD 12500 matches `12.5` and `12,500`; PLN 1250 still matches `12,5`.
- [ ] **Step 2: Run, see them fail.** `be-run.sh 'dotnet test --filter "FullyQualifiedName~MinorUnits|FullyQualifiedName~ExchangeRateTests|FullyQualifiedName~TransactionPrice|FullyQualifiedName~MigrationTests|FullyQualifiedName~ListTransactionsTests"'`
- [ ] **Step 3: Implement,** then `dotnet ef migrations add CurrencyPrecision --project src/Mita.Infrastructure --startup-project src/Mita.Api` and add the two functions by hand in `Up`/`Down`. Then `grep -rn "/ 100\|% 100\|, 6)" src --include=*.cs` must show no money math left outside the amount search and `MinorUnits`.
- [ ] **Step 4: Run, see them pass,** then the full backend suite. Existing tests whose expected UAH cross rates change only because rates below 0.1 now keep 7+ decimals get their expected values updated; nothing else may change.
- [ ] **Step 5: Commit** — `feat: iso 4217 minor units and precise exchange rates`

### Task 3: One-query rate lookup, staleness and the rates endpoint

**Files:** `Common/Rates/RateTable.cs`, `Common/Transactions/TransactionWriter.cs`, `Features/Transfers/CreateTransfer/CreateTransferHandler.cs`, `Features/Goals/ContributeToGoal/ContributeToGoalHandler.cs`, `Features/Rates/GetRates/{GetRatesValidator,GetRatesHandler}.cs`; tests `tests/Mita.Api.UnitTests/Features/Rates/GetRatesValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Rates/RatesTests.cs`, `Features/Transactions/CreateTransactionTests.cs`, `Features/Transfers/TransfersTests.cs`, `Features/Overview/OverviewTests.cs`.

**Interfaces:**
- Consumes: `Currencies`, `ExchangeRate.Round` (Tasks 1-2).
- Produces:
  - `ExchangeRate.MaxAgeDays = 30` (Domain).
  - `RateTable.PlnValuesAsync(this AppDbContext db, DateOnly date, CancellationToken ct)` keeps its signature and result, now one SQL statement (`DISTINCT ON ("Currency")` latest on or before `date`, union the earliest row of currencies with none) — used by reads.
  - `RateTable.FreshPlnValuesAsync(this AppDbContext db, DateOnly date, DateOnly today, CancellationToken ct) : Task<IReadOnlyDictionary<Currency, decimal>>`: same, minus currencies whose on-or-before rate is older than `MaxAgeDays` before `min(date, today)`; an earliest-row fallback is never stale. Used by `TransactionWriter`, `ResolveAsync`, the contribution's account-to-base step and `GetRatesHandler`.
  - `RateTable.ResolveAsync(..., DateOnly date, DateOnly today, CancellationToken ct)` (new `today` parameter); `TransactionWriter` and `CreateTransferHandler` take `TimeProvider`.
  - `GetRatesValidator`: `to` → `validation.required` when `from` and `to` are both null (checked after the `date` rule, before the currency rules).
- [ ] **Step 1: Failing tests**
  - Validator: no `from`/`to` → `to: required`; `to=JPY` alone, `from=JPY` alone → valid; `to=XDR` → invalidValue.
  - `RatesTests`: `Should_ReturnTwelvePairs_When_NoFilter` becomes `Should_Return400_When_NeitherFromNorTo`; `Should_ReturnEveryCurrency_When_OnlyToGiven`: rates seeded for all 147 quoted on one date, `to=PLN` → 147 items, none with `from == to`; `Should_UseWeeklyRate_When_TableBCurrencyOnOtherDay`: KWD stored on Wednesday 2026-10-21 only, `date=2026-10-26&from=KWD&to=PLN` → 12.6221; `Should_OmitPair_When_RateOlderThan30Days`: KWD stored 2026-09-01 only, date 2026-10-02 → empty, date 2026-10-01 → one pair; `Should_UseEarliestRate_When_DateBeforeHistory` still passes for a table B currency.
  - `CreateTransactionTests.Should_Return503_When_EnteredCurrencyRateIsStale`: KWD rate 40 days before the date, no manual rate → 503 `rates.unavailable`; the same with `rate` 12.5 → 201.
  - `CreateTransactionTests.Should_UseTodayForStaleness_When_DateInFuture`: KWD stored today only, a transaction dated today + 200 days → 201.
  - `TransfersTests.Should_Return503_When_RateIsStale` (same shape for a transfer).
  - `OverviewTests.Should_ValueAccount_When_RateIsStale`: a KWD account whose only rate is 90 days old still counts in `balance` (no 503).
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite.
- [ ] **Step 3: Commit** — `feat: one-query rate lookup with a 30-day staleness limit on writes`

### Task 4: Sync NBP tables A and B

**Files:** new `Mita.Infrastructure/Rates/NbpTable.cs`, `Rates/{IExchangeRateSource,NbpExchangeRateSource,RatesSynchronizer}.cs`, `DependencyInjection.cs` (only if the logger needs wiring); tests `tests/Mita.Api.UnitTests/Rates/NbpExchangeRateSourceTests.cs` (+ fixture JSON in the test as a raw string), `tests/Mita.Api.IntegrationTests/Infrastructure/FakeExchangeRateSource.cs`, `Rates/RatesSynchronizerTests.cs`.

**Interfaces:**
- Consumes: `Currencies.TryParse`, `Currencies.Quoted`, `ExchangeRate.Round` (Tasks 1-2).
- Produces:
  - `enum NbpTable { A, B }`; `static class NbpTables`: `IReadOnlyList<Currency> CurrenciesOf(NbpTable table)` (A = the 31 codes of Global Constraints, B = the other quoted ones), `NbpTable Of(Currency currency)` (throws for PLN).
  - `IExchangeRateSource.FetchAsync(NbpTable table, DateOnly from, DateOnly to, CancellationToken ct) : Task<IReadOnlyList<PlnRate>>`.
  - `NbpExchangeRateSource(HttpClient http, ILogger<NbpExchangeRateSource> logger)`: path `exchangerates/tables/{table}/{from}/{to}/?format=json`, 93-day windows as today, maps every code through `Currencies.TryParse` (whatever table it came from), skips `XDR` silently and logs any other unknown code once per call at Warning.
  - `RatesSynchronizer.SyncAsync` runs table A then B: start per table as in Decisions, fetch, keep plausible rows (`Round(value)` in `(0, 1 000 000]`), insert with one `INSERT … SELECT * FROM unnest(@currencies, @dates, @values) … ON CONFLICT DO NOTHING` per table, catch and log a table failure (`LogTableSyncFailed(table)`), rethrow when both tables failed; invalidate the cache once when anything was inserted; returns the inserted count.
  - `FakeExchangeRateSource.FetchAsync(table, from, to)`: table A currencies every day, table B currencies on Wednesdays only; `Requests` becomes `IReadOnlyList<(NbpTable Table, DateOnly From, DateOnly To)>`; `Failure` gains a per-table variant `FailingTable`.
- [ ] **Step 1: Failing tests**
  - `Should_ParseTableB_When_NbpAnswers`: stub answers a table B JSON with `KWD 12.6221`, `IRR 0.00000212`, `VND 0.0001503`, `ZZZ 1.0` → three rates (no ZZZ), request path contains `/tables/B/`; one warning logged for `ZZZ`.
  - `Should_IgnoreXdr_When_TableAContainsIt`: table A JSON with `XDR` and `JPY` → only JPY, no warning.
  - Existing window, 404, 500 and retry tests pass with a table argument.
  - `RatesSynchronizerTests`:
    - `Should_BackfillHistory_When_TableIsEmpty` (extended): both tables requested from `today − 183`; every quoted currency has rows; a table B currency only on Wednesdays.
    - `Should_BackfillTable_When_OneOfItsCurrenciesHasNoRows`: EUR/USD/UAH stored up to yesterday (the pre-deploy state) → table A requested from `today − 183`, JPY now has history, EUR rows unchanged.
    - `Should_FetchOnlyNewDays_When_HistoryExists`: per table, from the table's latest date + 1.
    - `Should_NotDragStartBack_When_ACurrencyStoppedBeingPublished`: every table B currency stored up to last Wednesday except one whose rows end 60 days ago (but exist) → table B requested from last Wednesday + 1.
    - `Should_SyncTableA_When_TableBFails`: `FailingTable = B` → table A rows inserted, no exception; both failing → `SyncAsync` throws.
    - Existing duplicate, plausibility, cache-invalidation and keep-running tests pass.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.** `be-run.sh 'dotnet test --filter "FullyQualifiedName~NbpExchangeRateSource|FullyQualifiedName~RatesSynchronizer"'`
- [ ] **Step 3: Commit** — `feat: sync nbp table b rates alongside table a`

### Task 5: API contract, OpenAPI and the backend check

**Files:** `docs/api-contract.md`, `tests/Mita.Api.IntegrationTests/OpenApiTests.cs`.
- [ ] **Step 1: Failing test** — `Should_DescribeRatesQuery`: `/v1/rates` GET lists parameters `date`, `from` and `to`.
- [ ] **Step 2: Contract.** `type Currency` lists the 148 codes (one wrapped union) with a pointer to the rules; `Settings.baseCurrency` and the settings input stay the four. "Accounts, transfers and rates": amounts are ISO 4217 minor units with the exponent lists; the rate is per major unit and the conversion formula; rounding rule; manual rate up to 1 000 000 000; the 100 000 000 000 limit is in minor units of each currency. "Rates": tables A and B, XDR excluded, table B weekly so a date reads the latest rate on or before it, the per-table sync and backfill, `from` or `to` required (`to: ["validation.required"]`), at most 147 pairs, the 30-day staleness on `GET /v1/rates` and on automatic conversions of writes, never on reads. "Transactions": the amount search digits. Errors: `validation.required` mentions the rates query; `invalidValue` mentions a non-base `baseCurrency`, the new manual-rate maximum; 503 `rates.unavailable` mentions a stale rate.
- [ ] **Step 3: Run** `be-run.sh 'dotnet build -warnaserror && dotnet test'`; all green.
- [ ] **Step 4: Commit** — `docs: api contract for the extended currency list`

### Task 6: Frontend currency catalog and money math

Frontend repo, branch `feature/currency-list` from `origin/develop`.

**Files:** new `app/shared/money/{currencies,format}.ts`, `app/shared/money/money.ts`, `app/shared/api/contract.ts`, `app/shared/rates/convert.ts`, `app/shared/composables/useMoney.ts`, `app/shared/user/useUserSettings.ts`, `app/features/transactions/utils/{keypad,csv}.ts`, `components/{QuickAddForm,QuickAddPanel,TransactionDialog}.vue`, `schemas/transactionSchema.ts`, `app/features/accounts/components/{AccountDialog,TransferDialog}.vue`, `schemas/{accountSchema,transferSchema}.ts`, `app/features/goals/{components/GoalDialog.vue,components/GoalContributionDialog.vue,components/GoalCard.vue,utils/goalView.ts,schemas/contributionSchema.ts}`, `app/features/budgets/components/BudgetDialog.vue`, `app/features/settings/utils/options.ts`, `mocks/{transactions,transfers,goals,budgets,rates,validation}.ts`, `mocks/handlers/{users,transactions}.ts`, `mocks/seed/{transfers,goals}.ts`; tests new `tests/unit/shared/currencies.test.ts`, `tests/unit/shared/{money,rates}.test.ts`, `tests/unit/features/transactions/{keypad,csv}.test.ts`, `tests/unit/features/settings/options.test.ts`, `tests/unit/mocks/{transactions,users}.test.ts`.

**Interfaces:**
- Produces:
  - `currencies.ts`: `CURRENCIES` (`as const` tuple, Global Constraints order), `type Currency = (typeof CURRENCIES)[number]`, `BASE_CURRENCIES = ['PLN', 'EUR', 'USD', 'UAH'] as const`, `type BaseCurrency`, `isCurrency(value: unknown): value is Currency`, `isBaseCurrency(value: unknown): value is BaseCurrency`, `currencyDigits(currency: Currency): 0 | 2 | 3`, `AMOUNT_MINOR_MAX = 100_000_000_000`, `MANUAL_RATE_MAX = 1_000_000_000`.
  - `money.ts`: `toMinor(major: number, currency: Currency)`, `fromMinor(minor: number, currency: Currency)`, `convertMinor(minor: number, rate: number, from: Currency, to: Currency)`, `roundRate(rate: number): number` (the backend rule, moved here from `mocks/rates.ts`); the `currencies` export is removed. `money.ts` re-exports `Currency` from `currencies.ts` so existing imports keep compiling.
  - `format.ts`: `currencyFormat(currency: Currency): Intl.NumberFormatOptions` = `{ style: 'currency', currency, currencyDisplay: 'narrowSymbol', minimumFractionDigits: d, maximumFractionDigits: d }`; `useMoney().format` and `formatEntry` spread it.
  - `contract.ts`: `Settings.baseCurrency` and `SettingsInput.baseCurrency` are `BaseCurrency`; every other `currency` stays `Currency`.
  - `keypad.ts`: `pressKey(entry: string, key: KeypadKey, digits: 0 | 2 | 3)`, `entryToMinor(entry: string, digits: 0 | 2 | 3)`; `QuickAddForm` hides the decimal key when `digits === 0` and passes the account currency's digits.
  - Every `InputNumber` with `mode="currency"` binds `:min-fraction-digits`/`:max-fraction-digits` to `currencyDigits` of its currency; rate inputs use `:max-fraction-digits="12"`.
  - Schemas: amount maxima become `toMinor(value, currency) <= AMOUNT_MINOR_MAX` (account opening balance, transfer amount, transaction amount, contribution amount); manual rate rules gain `<= MANUAL_RATE_MAX` with `validation.invalidValue`.
  - CSV: `decimal(minor: number, currency: Currency)` uses `currencyDigits`; the three amount columns pass their own currency.
  - Mocks: every `convertMinor` call passes its two currencies; `isCurrency` comes from `currencies.ts`; the users handler checks `isBaseCurrency`; the manual-rate maximum is `MANUAL_RATE_MAX`; the transactions amount search mirrors the backend digits rule.
  - `options.ts`: `currencyOptions` maps `BASE_CURRENCIES` (Task 8 removes it with the old picker); `currencySymbol` stays.
- [ ] **Step 1: Failing unit tests**
  - `currencies.test.ts`: 148 codes in the given order, `BASE_CURRENCIES` is a prefix; `currencyDigits` JPY 0, KWD 3, PLN 2, 16 zero and 7 three-decimal codes (same lists as the backend); `isCurrency('XDR')`, `isCurrency('jpy')` false.
  - `money.test.ts`: `toMinor(1500, 'JPY')` 1500, `toMinor(1.25, 'KWD')` 1250, `toMinor(12.5, 'PLN')` 1250, `fromMinor(1250, 'KWD')` 1.25; `convertMinor` rows identical to the backend `MinorUnits.Convert` rows; `roundRate` rows identical to the backend `Round` rows.
  - `rates.test.ts`: `toBaseAmount(1500, 'JPY', 'PLN', [{ from: 'JPY', to: 'PLN', value: 0.024764 }])` → 3715.
  - Format: `new Intl.NumberFormat('en', currencyFormat('JPY')).format(1500)` → `¥1,500`; `currencyFormat('KWD')` formats 1.25 containing `1.250`; `currencyFormat('IQD')` has 3 digits although CLDR says 0.
  - `keypad.test.ts`: `pressKey('12', 'decimal', 0)` → `'12'`; `pressKey('1.25', '5', 3)` → `'1.255'`; `pressKey('1.255', '5', 3)` → `'1.255'`; `entryToMinor('1.5', 3)` → 1500; `entryToMinor('1500', 0)` → 1500.
  - `csv.test.ts`: a JPY transaction on a PLN account writes `-1500` and `-37.15`.
  - Mocks: the users handler refuses `baseCurrency: 'JPY'` with `validation.invalidValue`; a JPY expense on a PLN account in the mock prices `accountAmount` 3715 at 0.024764 (seed the rate in the test state).
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/wsl-run.sh 'bun test tests/unit && bun run lint && bun run typecheck'`; the existing mock E2E suite still passes (no UI change yet).
- [ ] **Step 3: Commit** — `feat: currency catalog with iso minor units in the frontend`

### Task 7: Mock rates for every currency

**Files:** `mocks/rates.ts`, new `mocks/seed/rateLevels.ts`, `mocks/seed/rates.ts`, `mocks/handlers/rates.ts`, `mocks/db.ts`, the mock write paths that read rates (`mocks/transactions.ts`, `mocks/transfers.ts`, `mocks/goals.ts`); tests `tests/unit/mocks/{rates,db,transactions,transfers}.test.ts`.

**Interfaces:**
- Consumes: Task 6 catalog and `roundRate`.
- Produces:
  - `rateLevels.ts`: `RATE_LEVELS: Readonly<Record<Exclude<Currency, 'PLN'>, number>>` with the NBP mids of 2026-10-21/22 (for example `JPY 0.024764`, `KWD 12.6221`, `IRR 0.00000212`).
  - `seedRates(today)`: EUR, USD, UAH daily over 183 days as today (their existing levels and drift); every other currency on Wednesdays only, `roundRate(level × (1 + drift))`.
  - `rateOn(rows, date, from, to)` unchanged for reads; `freshRateOn(rows, date, today, from, to): number | undefined` applies the 30-day staleness; mock writes and `GET /v1/rates` use it (a missing rate on a write answers the same 503 problem the mock already uses for a missing rate).
  - `GET /v1/rates`: neither `from` nor `to` → `to: validation.required`.
  - `MockState.version: 2`; `isState` checks 2.
- [ ] **Step 1: Failing tests**
  - Seed: KWD rows only on Wednesdays, EUR every day, 147 quoted currencies present.
  - `GET /v1/rates?to=PLN` → 147 items; no params → `to: required`; `from=KWD&to=PLN&date=<a Monday>` → the previous Wednesday's value.
  - Staleness: a write with a 40-day-old KWD rate and no manual rate → 503; with a manual rate → 201.
  - `db.test.ts`: a stored state with `version: 1` is reseeded.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass;** then unit, lint, typecheck and the mock E2E suite as in `dock.mita-work/environment.md`.
- [ ] **Step 3: Commit** — `feat: mock rates for every nbp currency`

### Task 8: Searchable currency picker

**Files:** new `app/shared/money/{currencyOptions,recentCurrencies}.ts`, new `app/shared/components/CurrencySelect.vue`, `app/features/accounts/components/AccountDialog.vue`, `app/features/transactions/components/TransactionDialog.vue`, `app/features/settings/components/RegionSection.vue`, `i18n/locales/*.json`; tests new `tests/unit/shared/{currencyOptions,recentCurrencies}.test.ts`, new `tests/e2e/currencies.spec.ts`, `tests/e2e/{settings,transactionDialog,overview}.spec.ts`.

**Interfaces:**
- Consumes: Task 6 catalog and format; the accounts list already loaded by the dialogs; `useUserSettings().baseCurrency`.
- Produces:
  - `currencyOptions.ts`: `currencyName(currency: Currency, locale: string): string` (`Intl.DisplayNames`, fallback the code); `type CurrencyOption = { value: Currency; code: Currency; name: string; label: string }` with `label = "CODE — Name"`; `type CurrencyGroup = { label: string; items: CurrencyOption[] }`; `pinnedCurrencies(recent: readonly Currency[], pinned: readonly Currency[], offered: readonly Currency[]): Currency[]` (unique, recent first, only offered codes, at most 8); `currencyGroups(locale: string, offered: readonly Currency[], pinned: readonly Currency[], labels: { pinned: string; all: string }): CurrencyGroup[]` (the "All currencies" group excludes pinned codes; no pinned group when `pinned` is empty).
  - `recentCurrencies.ts`: `readRecentCurrencies(): Currency[]` and `rememberCurrency(currency: Currency): void` on `localStorage['mita.recentCurrencies']`, at most 5, most recent first, invalid stored values dropped, every access in try/catch.
  - `CurrencySelect.vue`: `defineModel<Currency>({ required: true })`, props `inputId: string`, `offered?: readonly Currency[]` (default `CURRENCIES`), `pinned?: readonly Currency[]`, `compact?: boolean`, `invalid?: boolean`, `labelId?: string`, `describedBy?: string`, `disabled?: boolean`. More than 10 offered → `filter`, `filter-fields="['code', 'name']"`, `filter-placeholder` `t('shared.currency.search')`, empty-filter text `t('shared.currency.empty')`, option groups; otherwise a flat list. The value slot shows the code when `compact`, the label otherwise. A selection calls `rememberCurrency`. Emits `change` after the model updates (the transaction dialog's `onCurrencyChange` keeps working).
  - Account dialog: `pinned` = base currency + currencies of the user's accounts. Transaction dialog (compact): `pinned` = the chosen account's currency + base currency + currencies of the user's accounts. Settings: `offered = BASE_CURRENCIES`; `options.ts` loses `currencyOptions` and `currencyLabel` (and their rows in `tests/unit/features/settings/options.test.ts`).
  - i18n (all four locales): `shared.currency.pinned` ("Your currencies"), `shared.currency.all` ("All currencies"), `shared.currency.search` ("Search by code or name"), `shared.currency.empty` ("No currency found").
- [ ] **Step 1: Failing unit tests**
  - `currencyName('JPY', 'en')` → `Japanese Yen`; `currencyName('JPY', 'pl')` contains `jen`; an unknown name falls back to the code.
  - `pinnedCurrencies(['KWD'], ['PLN', 'EUR', 'KWD'], CURRENCIES)` → `['KWD', 'PLN', 'EUR']`; nine distinct inputs → 8; codes outside `offered` dropped.
  - `currencyGroups('en', CURRENCIES, ['PLN', 'EUR'], …)` → first group 2 items, second 146 items starting with `AED`.
  - `recentCurrencies`: remembering `JPY`, `KWD`, `JPY` gives `['JPY', 'KWD']`; a stored `'["XDR","JPY"]'` reads as `['JPY']`; a throwing `localStorage` reads `[]` and remembering does not throw.
- [ ] **Step 2: Failing E2E**
  - `currencies.spec.ts` (both projects): "creates a JPY account and a KWD expense": "Add account", open "Currency", type `yen`, pick `JPY — Japanese Yen`; type `1500` in the opening balance: the input shows `¥1,500` with no decimal separator; save; the card shows `¥1,500`. Add a transaction on that account in KWD (typed as `kuw`), amount `1.250`: the input shows three fraction digits and the rate field is labelled `Exchange rate (KWD → JPY)`; save; the row matches `/KWD\s1\.250/`.
  - `currencies.spec.ts`: "shows the user's currencies first": the account dialog's currency list starts with the "Your currencies" group holding `PLN`, `EUR`, `UAH`, `USD` (the seeded accounts and base), then "All currencies".
  - `currencies.spec.ts`: "finds a currency by its Polish name" on `/pl/accounts`: typing `jen` offers `JPY`.
  - Updated: `transactionDialog.spec.ts` and `overview.spec.ts` pick `EUR — Euro`; `settings.spec.ts` expects `UAH — Ukrainian Hryvnia` and picks `EUR — Euro`, and the base currency list has exactly four options.
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite (desktop and mobile); all pass.
- [ ] **Step 4: Commit** — `feat: searchable currency picker with the user's currencies first`

### Task 9: Real-backend spec, smoke and docs

Frontend repo, same branch; the backend checkout on `feature/currency-list` (Tasks 1-5).

**Files:** new `tests/e2e-real/currencies.real.spec.ts`, `tests/e2e-real/auth.real.spec.ts`, the project guide in the repository root ("Real-backend suite": a `currencies` line; the auth-call count per run goes up by two).
- [ ] **Step 1: Write `currencies.real.spec.ts`** (helpers in the style of `tags.real.spec.ts`; the smoke backend has no rates, so only same-currency amounts): register a unique user; add a "Yen cash" account (Cash, JPY, 1500) and a "Dinar" account (Cash, KWD, 1.250); the cards show `¥1,500` and `KWD 1.250`; add a 300 JPY expense on "Yen cash" → its balance shows `¥1,200`; in Settings the base currency list has four options; delete the user. Update `auth.real.spec.ts` to the new base currency labels (`PLN — Polish Zloty`, `EUR — Euro`).
- [ ] **Step 2: Run** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/scripts/tg-real-smoke.sh` (it builds the backend checkout; copy it to `cl-real-smoke.sh` if the spec list is hard-coded); expected 8 passed, both smoke projects torn down, `pgdb` and `dock-mita-frontend-web-1` untouched.
- [ ] **Step 3: Run** `bun run lint && bun run typecheck && bun test tests/unit`; all green.
- [ ] **Step 4: Commit** — `test: real-backend spec for the extended currency list`

### Task 10: Pull requests

- [ ] Backend: rebase `feature/currency-list` on `origin/develop`, fold fix-ups into their task commits, run `dotnet build -warnaserror` and `dotnet test --logger junit` (Docker running), push with `--force-with-lease`, open a PR to `develop` with a plain description, wait for `check-source-branch`, `gh pr merge --rebase`. After the deploy, check the API log for one "Exchange rates sync stored N new rows" with N in the tens of thousands and no unsupported-currency warning.
- [ ] Frontend: after the backend PR is merged, the same flow for its `feature/currency-list`, with the mock E2E suite and the smoke green.

## Deferred (not in this plan)

Currencies in groups, debts and the converter (parts 2-5 use this list), a currency list served by the API, symbols or flags in the picker, per-currency amount limits, showing the effective rate date next to a stale-looking rate, the base currency beyond the four.
