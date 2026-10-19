# Categories and Transactions Implementation Plan

**Goal:** Real categories and transactions on the backend. Category CRUD with a per-user starter set, kind lock and reassign-on-delete, and transaction CRUD with filters, search, sorting, a signed sum in the base currency, currency conversion into the account currency at the transaction date, and balances that include transactions, exactly as the frontend contract and mocks define them.

**Architecture:**
- **Domain:** `Category` and `Transaction` live in `Mita.Domain` (`Categories/`, `Transactions/`). The starter set (18 categories, names in en, pl, ru, uk) is static domain data.
- **Balances:** the transactions migration replaces the view `account_movements` with `CREATE OR REPLACE VIEW` and adds one leg per transaction (`+AccountAmount` for income, `-AccountAmount` for expense). `AccountQueries`, `DeleteAccountValidator` and `UpdateAccountCurrencyValidator` stay untouched and now see transactions in `balance`, `inUse`, `currencyLocked` and `accountInUse`.
- **Amounts:** a transaction stores `Amount` (entered currency), `AccountAmount` (account currency), `Rate` and `BaseAmount` (the user's current base currency). A change of the base currency in `PUT /v1/settings` recomputes `BaseAmount` for all of the user's transactions in one SQL statement, so reads, the `amount` sort and the `sum` are plain column reads.
- **Slices:** `Features/Categories/{ListCategories,CreateCategory,UpdateCategory,DeleteCategory}` and `Features/Transactions/{ListTransactions,GetTransaction,CreateTransaction,UpdateTransaction,DeleteTransaction}`. Shared request parsing and rate resolution move from the transfers slice to `Common/` first.

**Tech Stack:** .NET 10, EF Core migrations (Npgsql), MediatR 12.5.0, FluentValidation, HybridCache over Redis, xUnit v3, Shouldly, Moq, Bogus, Testcontainers. Frontend task: Nuxt, Playwright. No new packages.

**Spec:** This plan argues from these documents:
- The backend project guide in the repository root, `docs/STACK.md`, `docs/api-contract.md` and the merged plans in `docs/superpowers/plans/` (especially `2026-10-18-accounts-and-rates.md`, whose interfaces this plan consumes).
- The frontend contract in the frontend repo:
  - `docs/superpowers/specs/2026-10-15-frontend-mocks-design.md` (a transaction may be entered in any currency and is converted into the account currency; totals in the base currency; minor units; "saving a transaction stores `accountAmount` and `baseAmount`, so history never changes when rates change"; "system categories cannot be deleted; used ones are reassigned to other"; "each new user gets a starter set of categories");
  - `app/shared/api/contract.ts` (`CategoryDto`, `CategoryInput`, `TransactionDto`, `TransactionInput`, `TransactionQuery`, `TransactionSort`, `TransactionPage`);
  - `mocks/handlers/categories.ts`, `mocks/categories.ts`, `mocks/seed/categories.ts`, `mocks/handlers/transactions.ts`, `mocks/transactions.ts`, `mocks/ledger.ts`, `mocks/rates.ts`, `mocks/paginate.ts`, `mocks/validation.ts` (rules and codes);
  - `app/shared/categories/categoriesApi.ts`, `app/features/transactions/api/transactionsApi.ts`, `app/features/transactions/composables/useTransactions.ts` (offset pages through `useInfiniteQuery`), `app/features/transactions/utils/filters.ts` (the client turns financial periods into `from`/`to`), `app/shared/api/invalidation.ts`.
- The deferred follow-ups in the accounts ledger (`dock.mita-work/accounts-rates-ledger.md`).

## Decisions

| Topic | Decision |
|---|---|
| Scope | Categories, transactions, the base-currency recompute they force on `PUT /v1/settings`, and the cheap startup-migration lock from the accounts ledger. Budgets, goals, overview and reports stay out (see "What later plans need") |
| Starter categories | Seeded in the registration transaction in the registration locale (`RegisterCommand.Locale`). Users that exist before this migration get them from the migration, in their `user_settings.Locale`. The migration holds the 18×4 names as literal SQL, so a later change to the C# starter set never changes an applied migration. Backfilled ids use `gen_random_uuid()` |
| Category order | Kind (`expense` first), then system rank (none 0, `other` 1, `transfer` 2), then `CreatedAt`, `Position`, `Id`. `Position` is the starter index for seeded rows (they share one `CreatedAt`) and 0 for user-created rows. This reproduces the mock's insertion order |
| Category names | Unique per user and kind, case-insensitive after trimming: unique index on `("UserId", "Kind", lower("Name"))`. A lost race (`23505`) answers `name: ["validation.categoryNameTaken"]` |
| System categories | One `other` per user and kind (partial unique index), one `transfer` (expense). Both can be renamed and recoloured; their kind is locked; they cannot be deleted (`id: ["validation.systemCategory"]`). `transfer` cannot be used by a transaction (`validation.unknownCategory`), as in the mock |
| Category delete | In one database transaction: lock the category, move its transactions to the `other` category of the same kind, delete it. The budgets plan adds the budget merge to this handler |
| Category `inUse` | `EXISTS` a transaction with the category. The budgets plan ORs budgets into `CategoryQueries` |
| Stored transaction values | `Amount`, `Currency`, `Rate numeric(18,6)`, `AccountAmount`, `BaseAmount`. No `AccountCurrency` column (an account in use cannot change currency) and no `BaseCurrency` column (`BaseAmount` is always in the current base currency) |
| Pricing | `rate` = 1 when the entered currency equals the account currency (a sent rate is still validated, then ignored); else the manual rate rounded to 6 decimals; else the stored rate of the transaction date. `accountAmount = round(amount × rate)`. `baseAmount = amount` when the entered currency is the base, else `round(accountAmount × rate(accountCurrency → base, date))`. All rounding half away from zero, as `priceTransaction` in the mock |
| Base currency change | `PUT /v1/settings` with a new base currency locks the settings row and runs one `UPDATE transactions … FROM accounts` using a new SQL function `pln_value(currency, date)` (the `RateTable` lookup rule). When a needed currency has no stored rate at all, the answer is 503 `rates.unavailable` and nothing changes. This is a new status on `PUT /v1/settings` |
| Missing rates | Creating or updating a transaction that needs a rate that is not stored answers 503 `rates.unavailable` (the mock throws) |
| Caps | `amount` ≤ 100 000 000 000; a manual `rate` ≤ 1 000 000 (the mock has no cap, transfers have it); `accountAmount` ≤ 0 → `rate: ["validation.positiveAmount"]`; `accountAmount` above the max → `rate: ["validation.invalidValue"]` (as transfers, ruling R8). `baseAmount` is not capped: stored cross rates keep it below 5 × 10¹², which is JS-safe |
| Texts | `title` is optional, stored trimmed, `""` when missing; `note` is trimmed and `null` when blank. Over the limit: `validation.titleTooLong` (> 100) and `validation.noteTooLong` (> 200), as the mock (transfers keep `invalidValue`). A non-string or a control character gives `validation.invalidValue` for titles, notes and category names |
| Pagination | Offset, `{ items, page, pageSize, total }` plus `sum` and `currency` for transactions. The client pages with `useInfiniteQuery` on `page`/`total` and the CSV export pages with `pageSize=100`, so no cursor |
| Search | Server-side, as the mock: trimmed and lower-cased; matches when the title, the note or the category name contains it, or, when it looks like an amount (`^\d+([.,]\d{0,2})?$`), when the entered amount formatted as `units.cc` starts with it (comma read as a dot). Literal matching, so `%`, `_` and `\` are plain characters. Done in LINQ (`Contains` translates to `strpos`, the amount text is `(Amount / 100).ToString() + "." + (Amount % 100).ToString().PadLeft(2, '0')`) |
| Sorting | `-date` (default): `Date desc, CreatedAt desc, Id desc`; `date`: `Date, CreatedAt, Id`; `-amount`: `BaseAmount desc`, then the `-date` order; `amount`: `BaseAmount`, then the `-date` order. An update keeps `CreatedAt`, as the mock keeps the row index |
| Filter ids | A non-GUID `categoryId` or `accountId` query value is 400 `invalidValue` (the mock returns an empty page), as `accountId` on transfers. `from > to` gives an empty page |
| Periods | Financial periods stay on the client: it sends `from`/`to`. The server needs `monthStartDay` periods only in the budgets, overview and reports plans |
| Caching | `GET /v1/categories` is cached (user-scoped, tag `categories`). Transactions are not cached: the filter space (free-text search) is unbounded and every write would invalidate it anyway. Category commands invalidate `categories`; transaction commands invalidate `categories` (`inUse`) and `accounts` (`balance`, `inUse`) |
| Races and lock order | Every writer locks in the order settings → accounts → categories → transactions. Transaction create and update lock the settings row `FOR SHARE`, the account and the category `FOR SHARE`, the transaction row `FOR UPDATE` (update only), and re-check ownership, archived and kind under the locks. Category delete locks the category `FOR UPDATE` before moving transactions. A transaction write that loses against a category delete gets `23503`, mapped to `categoryId: ["validation.unknownCategory"]`; an account delete that loses against a new transaction keeps its existing mapping to `id: ["validation.accountInUse"]` |
| 404 vs 400 | As accounts: validation runs first, so an invalid body on an unknown id answers 400; valid bodies on unknown or foreign ids answer 404. Routes use `{id:guid}` |
| Status codes | `POST /v1/categories` and `POST /v1/transactions` answer 201 with `Location` (`/v1/categories/{id}`, `/v1/transactions/{id}`), `PUT` 200, `DELETE` 204 |
| Shared code | `CreateTransferFields` parsing (date, GUID, manual rate, note) and `ResolveRateAsync` move to `Common/Wire`, `Common/Time` and `Common/Rates` before transactions use them (a slice must not reference another slice) |
| Accounts ledger follow-ups | Taken: the startup-migration race (Task 9, a Postgres advisory lock around `MigrateAsync`). Left listed: rates cache-date bounds (R12, needs a contract change), `RateTable` round trips, the `ValidationCodes` constants class, PagingRules compilation, untested account race fallbacks |
| Hybrid frontend | `/v1/accounts`, `/v1/transfers`, `/v1/categories`, `/v1/transactions` become real in the hybrid default. Budgets, goals, overview and reports stay mocked on the cloned demo data and do not see real accounts, categories or transactions until their plans; the frontend project guide says so |

## Global Constraints

- All foundation, auth and accounts constraints apply: warnings are errors; central packages; `Api → Infrastructure → Domain`; records, `sealed`, no comments, English; `CancellationToken` everywhere; every endpoint in the v1 group with `RequireAuthorization("User")`; ProblemDetails for every error; XSRF on unsafe cookie requests (already global); request fields bound as `JsonElement?`; a whitespace-only string counts as missing; first failing rule per field only (a database validator skips a field that already failed a synchronous rule).
- Contract with the frontend, byte for byte (camelCase JSON):
  - `CategoryDto { id, name, kind: "expense"|"income", color: "chart-1"…"chart-6", system: "transfer"|"other"|null, inUse }`;
  - `CategoryInput { name, kind, color }`;
  - `TransactionDto { id, kind, date: "YYYY-MM-DD", title, note: string|null, categoryId, accountId, amount, currency, accountAmount, rate, baseAmount }`;
  - `TransactionInput { kind, date, title?, note?, categoryId, accountId, amount, currency, rate? }`;
  - `TransactionPage = Page<TransactionDto> & { sum, currency }` where `sum` is Σ(+baseAmount for income, −baseAmount for expense) over every filtered row, not only the page, and `currency` is the base currency.
- Endpoints:
  - `GET /v1/categories?kind&page&pageSize`: default pageSize 100, max 100;
  - `POST /v1/categories`, `PUT /v1/categories/{id}`, `DELETE /v1/categories/{id}`;
  - `GET /v1/transactions?search&categoryId&accountId&kind&from&to&sort&page&pageSize`: default pageSize 20, max 100; `from`/`to` inclusive; `sort` one of `-date`, `date`, `-amount`, `amount`;
  - `GET /v1/transactions/{id}`, `POST /v1/transactions`, `PUT /v1/transactions/{id}`, `DELETE /v1/transactions/{id}`.
- Validation codes, exactly as the mocks:
  - categories: `name` required / invalidValue / nameTooLong (> 40 after trim) / categoryNameTaken; `kind` required / invalidValue / categoryKindLocked (update: a system category, or one in use, to another kind); `color` required / invalidValue; delete of a system category gives `id: ["validation.systemCategory"]`; `kind` query invalidValue;
  - transactions: `kind` required / invalidValue; `date` required / invalidValue / dateTooFar (after today UTC plus one year); `title` invalidValue / titleTooLong; `note` invalidValue / noteTooLong; `categoryId` required / unknownCategory (not the user's, `transfer`, or not a GUID) / categoryKindMismatch (only when `kind` is valid); `accountId` required / unknownAccount / archivedAccount (except, on update, the account the transaction already has); `amount` required / invalidValue (not a safe integer or above the max) / positiveAmount (≤ 0); `currency` required / invalidValue; `rate` invalidValue (present and not a number > 0, or above 1 000 000); then `rate` positiveAmount / invalidValue for the converted amount; query values `kind`, `from`, `to`, `sort`, `categoryId`, `accountId`, `page`, `pageSize` invalidValue.
- Every category and transaction is scoped to the current user. A foreign id answers exactly like an unknown id.
- A stored transaction never changes when rates change; only a base-currency change rewrites `baseAmount`.
- Commit messages: Conventional Commits; no tool attribution and no `Co-Authored-By` trailers.

## Review Focus

- Changing the base currency while transactions in several currencies exist must leave `sum`, the `amount` sort and every `baseAmount` in the new currency, equal to what a fresh create would store, never a mix. Pinned in Task 8 (`Should_MatchFreshPricing_When_BaseCurrencyChanges`, `Should_Return503AndKeepAmounts_When_RatesMissing`).
- Deleting a used category while another request creates a transaction in it must never answer 500 or leave a transaction on a missing category. Pinned in Task 5 (`Should_NotFail_When_CategoryDeletedWhileTransactionCreated`).
- A search for `%`, `_`, `\`, `"12,5"` or `"Groceries"` (a category name) must behave like the mock: literal text match, amount prefix, category name. Pinned in Task 6 (`Should_TreatSearchLiterally_When_ContainsWildcards`, `Should_MatchAmountPrefix_When_SearchLooksLikeAmount`).
- Editing an old transaction whose account was archived since must still save when the account stays the same, and must refuse moving it to another archived account. Pinned in Task 7 (`Should_AllowArchivedAccount_When_TransactionKeepsIt`).
- Users registered before this release must find the starter categories in their locale, and deleting a user with categories and transactions must leave no rows. Pinned in Task 1 (`Should_BackfillStarterCategories_When_UserExistedBeforeMigration`, `Should_DeleteUserWithCategoriesAndTransactions`).

## What later plans need

- Budgets: expense categories without `transfer` (`validation.unknownCategory`, `expenseCategoryOnly`); `spent` = Σ expense `BaseAmount` per category in the period; budgets in `CategoryQueries` `inUse` and in the category-delete reassign (merge limits per period, as `budgetReference` in the mock); financial periods from `monthStartDay` (including `lastWorkingDay`) on the server; the index `(UserId, Date)` on `transactions`.
- Goals: `linkedGoals` in `AccountQueries`; contributions as transfers (`transferId`).
- Overview and reports: income and expense totals from `BaseAmount`, excluding the `transfer` category, per period; `pln_value` for any further conversion.

---

## File map

```
src/Mita.Domain/Categories/Category.cs, CategoryKind.cs, CategoryColor.cs, SystemCategory.cs, StarterCategories.cs
src/Mita.Domain/Transactions/Transaction.cs, TransactionDetails.cs
src/Mita.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs, TransactionConfiguration.cs
src/Mita.Infrastructure/Persistence/Migrations/<timestamp>_CategoriesTransactions.cs
src/Mita.Api/Extensions/MigrationExtensions.cs
src/Mita.Api/Common/Wire/JsonValues.cs, Common/Time/UtcCalendar.cs, Common/Rates/RateTable.cs, Common/Rates/ManualRate.cs
src/Mita.Api/Common/Categories/CategoryDto.cs, CategoryWire.cs, CategoryQueries.cs, CategoryCacheTags.cs, ICategoryInput.cs, CategoryInputValidator.cs, CategoryNameValidator.cs, CategoryInputParser.cs
src/Mita.Api/Common/Transactions/TransactionDto.cs, TransactionQueries.cs, TransactionPrice.cs, ITransactionInput.cs, TransactionInputValidator.cs, TransactionReferencesValidator.cs, TransactionInputParser.cs, TransactionWriter.cs, TransactionBaseAmounts.cs
src/Mita.Api/Features/Categories/{ListCategories,CreateCategory,UpdateCategory,DeleteCategory}/*, CategoriesEndpoints.cs
src/Mita.Api/Features/Transactions/{ListTransactions,GetTransaction,CreateTransaction,UpdateTransaction,DeleteTransaction}/*, TransactionsEndpoints.cs
src/Mita.Api/Features/Transfers/CreateTransfer/* (uses the moved helpers), Features/Auth/Register/RegisterHandler.cs, Features/Settings/UpdateSettings/*
tests/Mita.Api.UnitTests/{Categories,Common,Features/Categories,Features/Transactions}/...
tests/Mita.Api.IntegrationTests/Features/{Categories,Transactions,Settings}/..., Persistence/MigrationTests.cs, Infrastructure/TestData.cs
```

---

### Task 1: Domain model, schema, starter backfill and the ledger view

**Files:** `Mita.Domain/Categories/*`, `Mita.Domain/Transactions/*`, both configurations, `AppDbContext` (DbSets `Categories`, `Transactions`), migration `CategoriesTransactions`; tests `tests/Mita.Api.UnitTests/Categories/StarterCategoriesTests.cs`, `tests/Mita.Api.IntegrationTests/Persistence/MigrationTests.cs` (extended), `tests/Mita.Api.IntegrationTests/Persistence/LedgerTests.cs`, `Infrastructure/TestData.cs`.

**Interfaces:**
- Consumes: `Account`, `Currency`, `Locale`, `IAuditable`, `TestData.AddAccountAsync`.
- Produces:
  - `enum CategoryKind { Expense, Income }`, `enum CategoryColor { Chart1, Chart2, Chart3, Chart4, Chart5, Chart6 }`, `enum SystemCategory { Transfer, Other }`, all stored as strings.
  - `sealed class Category : IAuditable` with `Guid Id, Guid UserId, string Name, CategoryKind Kind, CategoryColor Color, SystemCategory? System, int Position`, `const int NameMaxLength = 40`, `static Category Create(Guid userId, string name, CategoryKind kind, CategoryColor color)` (system null, position 0), `static Category Starter(Guid userId, StarterCategory starter, Locale locale, int position)` and `void Update(string name, CategoryKind kind, CategoryColor color)`.
  - `sealed record StarterCategory(string Key, CategoryKind Kind, CategoryColor Color, SystemCategory? System, IReadOnlyDictionary<Locale, string> Names)` and `static class StarterCategories { IReadOnlyList<StarterCategory> All; IReadOnlyList<Category> For(Guid userId, Locale locale); }`: the 18 entries of `mocks/seed/categories.ts` `starterSet` in that order, with its keys, colours, systems and names.
  - `sealed record TransactionDetails(CategoryKind Kind, DateOnly Date, string Title, string? Note, Guid CategoryId, Guid AccountId, long Amount, Currency Currency, decimal Rate, long AccountAmount, long BaseAmount)`.
  - `sealed class Transaction : IAuditable` with the `TransactionDetails` fields plus `Guid Id, Guid UserId`, `const int TitleMaxLength = 100`, `const int NoteMaxLength = 200`, `static Transaction Create(Guid userId, TransactionDetails details)` and `void Update(TransactionDetails details)`.
  - Tables:
    - `categories`: FK to the user cascade; `Name` varchar(40); unique index `IX_categories_user_kind_name` on `("UserId", "Kind", lower("Name"))` (raw SQL in the migration); partial unique index on `("UserId", "Kind") WHERE "System" = 'Other'`; index `(UserId, Kind)`.
    - `transactions`: FK to the user cascade; FKs to `accounts` and `categories` `NO ACTION`; `Title` varchar(100) not null; `Note` varchar(200); `Rate numeric(18,6)`; indexes `(UserId, Date)`, `AccountId`, `CategoryId`; check constraints `"Amount" > 0`, `"AccountAmount" > 0`.
  - The migration, after the tables:
    1. `CREATE OR REPLACE VIEW account_movements AS` the two transfer selects `UNION ALL SELECT "UserId", "AccountId", CASE WHEN "Kind" = 'Income' THEN "AccountAmount" ELSE -"AccountAmount" END FROM transactions;`
    2. `CREATE FUNCTION pln_value(p_currency text, p_date date) RETURNS numeric LANGUAGE sql STABLE`: 1 for `'PLN'`, else the latest `exchange_rates."PlnValue"` on or before `p_date`, else the earliest, else null.
    3. The backfill: `INSERT INTO categories (…) SELECT gen_random_uuid(), u."Id", v.name, v.kind, v.color, v.system, v.position, now(), now() FROM "AspNetUsers" u JOIN user_settings s ON s."UserId" = u."Id" JOIN (VALUES …) v(locale, name, kind, color, system, position) ON v.locale = s."Locale" WHERE NOT EXISTS (SELECT 1 FROM categories c WHERE c."UserId" = u."Id")`, with the 72 literal rows written out.
    - `Down`: restore the transfers-only view, drop the function, drop `transactions`, then `categories`.
  - `TestData`: `Task<Category> AddCategoryAsync(WebApplicationFactory<Program>, Guid userId, CategoryKind kind = Expense, string? name = null)` and `Task<Transaction> AddTransactionAsync(WebApplicationFactory<Program>, Account account, Category category, long amount, string date = "2026-10-01")` (same currency as the account, rate 1, `BaseAmount = amount`).
- [ ] **Step 1: Failing tests**
  - `Should_MatchMockStarterSet`: 18 entries; expense 10 and income 8; exactly one `Other` per kind and one `Transfer` (expense); every name in every locale is non-empty and at most 40 characters; `For(user, Pl)[0]` is `Spożywcze`, `Expense`, `Chart2`, position 0.
  - `Should_ApplyMigrations_When_DatabaseIsEmpty` (extended): tables `categories`, `transactions`, function `pln_value`.
  - `Should_BackfillStarterCategories_When_UserExistedBeforeMigration`: on a fresh database (`postgres.NewDatabaseConnectionString()`), migrate to `AccountsTransfersRates` with `IMigrator`, insert one user with `Locale = 'Pl'` and one with `'Uk'`, migrate to the latest; each has 18 categories with the names of `StarterCategories.For(…, locale)` and positions 0..17.
  - `Should_IncludeTransactionLegsInBalance`: an account with opening balance 10 000, an expense of 1 200 and an income of 500 through `TestData`; `GET /v1/accounts` shows `balance` 9 300 and `inUse` true.
  - `Should_DeleteUserWithCategoriesAndTransactions`: `DELETE /v1/me` gives 204, and no `categories` or `transactions` rows remain for that user.
  - `Should_ResolvePlnValue_LikeRateTable`: with EUR rows on 2026-03-06 (4.27) and 2026-03-09 (4.30), `pln_value('EUR', '2026-03-08') = 4.27`, `'2020-01-01'` gives 4.27, `'PLN'` gives 1, `'USD'` gives null (isolated data).
- [ ] **Step 2: Run, see them fail.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/be-run.sh 'dotnet test --filter "FullyQualifiedName~StarterCategories|FullyQualifiedName~MigrationTests|FullyQualifiedName~LedgerTests|FullyQualifiedName~DeleteUserWithCategories"'`
- [ ] **Step 3: Implement, then add the migration** with `dotnet ef migrations add CategoriesTransactions --project src/Mita.Infrastructure --startup-project src/Mita.Api`, and append the SQL by hand.
- [ ] **Step 4: Run, see them pass,** then the full suite (account balances now read the new view).
- [ ] **Step 5: Commit** — `feat: categories and transactions schema with transaction legs in the ledger`

### Task 2: Share request parsing and rate resolution

Refactor only; no behaviour changes, every transfers test stays green.

**Files:** `Common/Wire/JsonValues.cs`, new `Common/Time/UtcCalendar.cs`, new `Common/Rates/ManualRate.cs`, `Common/Rates/RateTable.cs`, `Features/Transfers/CreateTransfer/{CreateTransferFields,CreateTransferValidator,CreateTransferHandler}.cs`; tests `tests/Mita.Api.UnitTests/Common/JsonValuesTests.cs`, `tests/Mita.Api.UnitTests/Common/UtcCalendarTests.cs`.

**Interfaces:**
- Produces:
  - `JsonValues.TryGetIsoDate(JsonElement?, out DateOnly)`, `JsonValues.TryGetGuid(JsonElement?, out Guid)`, `JsonValues.TryGetSafeInteger(JsonElement?, out double)` (the validator's `IsSafeInteger`), and `JsonValues.TryGetOptionalText(JsonElement?, out string?)`: true with `null` when missing or blank, true with the trimmed text when a string without control characters, false otherwise. Length checks stay with the callers.
  - `static class UtcCalendar` with `DateOnly Today(TimeProvider)` and `DateOnly LatestAllowedDate(TimeProvider)` (today plus one year).
  - `static class ManualRate` with `const decimal Max = 1_000_000m` and `bool TryParse(JsonElement?, out decimal)` (the current `TryGetRate`).
  - `RateTable.ResolveAsync(this AppDbContext db, Currency from, Currency to, decimal? manualRate, DateOnly date, CancellationToken ct) : Task<decimal?>` (the current `ResolveRateAsync`).
  - `CreateTransferFields` keeps only the account-error logic and the field names.
- [ ] **Step 1: Failing tests** — `JsonValues` rows: `"2026-02-30"` and `"2026-1-01"` are not dates, `"2026-03-02"` is; `"acc_x"` is not a GUID; `"  a  "` gives `"a"`, `"   "` and `null` give `null`, `5` and `"a\u0000"` fail; `UtcCalendar.LatestAllowedDate` at 2026-10-19T23:59Z is 2027-10-19.
- [ ] **Step 2: Run, see them fail, move the code, run the unit tests and every `TransfersTests`, see them pass.**
- [ ] **Step 3: Commit** — `refactor: share request field parsing and rate resolution`

### Task 3: List and create categories, starter set on registration

**Files:** `Common/Categories/{CategoryDto,CategoryWire,CategoryQueries,CategoryCacheTags,ICategoryInput,CategoryInputValidator,CategoryNameValidator,CategoryInputParser}.cs`, `Features/Categories/ListCategories/*`, `Features/Categories/CreateCategory/*`, `Features/Categories/CategoriesEndpoints.cs`, `Program.cs`, `Features/Auth/Register/RegisterHandler.cs`; tests `tests/Mita.Api.UnitTests/Features/Categories/CreateCategoryValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Categories/CategoriesTests.cs`, `Features/Auth/RegisterTests.cs` (extended).

**Interfaces:**
- Consumes: `Category`, `StarterCategories` (Task 1); `JsonValues` (Task 2); `PagingRules`, `PageResponse<T>`, `ToPageAsync`.
- Produces:
  - `sealed record CategoryDto(Guid Id, string Name, string Kind, string Color, string? System, bool InUse)`.
  - `static class CategoryWire`: `ToWire`/`TryParse(JsonElement?)`/`TryParse(string?)` for `CategoryKind` (`expense`, `income`) and `CategoryColor` (`chart-1`…`chart-6`), `ToWire(SystemCategory?)` (`transfer`, `other`, null).
  - `CategoryQueries.ProjectCategories(this AppDbContext db, Guid userId) : IQueryable<CategoryDto>` and `ProjectCategory(db, userId, id)`: no tracking, order from Decisions, `InUse = db.Transactions.Any(t => t.CategoryId == category.Id)`.
  - `CategoryCacheTags.Categories = "categories"`.
  - `interface ICategoryInput` with `JsonElement? Name, Kind, Color`; `abstract class CategoryInputValidator<T>`: the synchronous rules; `CategoryNameValidator<T>(AppDbContext, ICurrentUser)` with an abstract `Guid? ExistingId(T)`: `name: categoryNameTaken` when name and kind parse and another category of the user in that kind has the same `lower(trim(name))`; skipped when `name` or `kind` failed.
  - `CategoryInputParser.Parse(ICategoryInput) → ParsedCategory(string Name, CategoryKind Kind, CategoryColor Color)` (trimmed).
  - `ListCategoriesQuery(string? Kind, int Page, int PageSize) : ICachedQuery`: key `categories:{kind ?? "*"}:{page}:{pageSize}`, tag `categories`; endpoint defaults 1 and 100. `ListCategoriesValidator`: paging rules and `kind` invalidValue.
  - `CreateCategoryCommand : ICategoryInput, IInvalidatesCache` (tag `categories`). `POST /v1/categories` answers 201, `Location: /v1/categories/{id}`, body `CategoryDto`. A `23505` on save answers `name: ["validation.categoryNameTaken"]`.
  - `RegisterHandler` adds `StarterCategories.For(user.Id, command.Locale)` in the same transaction as the settings.
- [ ] **Step 1: Failing tests**
  - Validator rows: `name` missing, `"   "` → required; `5` → invalidValue; 41 characters after trim → nameTooLong; `"a\u0007"` → invalidValue; `kind` missing → required, `"transfer"`, `"Expense"` → invalidValue; `color` missing → required, `"chart-7"`, `"red"` → invalidValue.
  - `Should_SeedStarterCategories_When_UserRegisters`: with `Accept-Language: pl`, `GET /v1/categories` gives 18 items; names in order `Spożywcze, Transport, Mieszkanie, Kawiarnie, Zdrowie, Zakupy, Rozrywka, Prezenty, Inne, Przelewy, Wynagrodzenie, Freelance, Prezent, Odsetki, Zwrot, Sprzedaż, Premia, Inne`; `system` is `other` on both `Inne`, `transfer` on `Przelewy`, null elsewhere; every `inUse` false. Without the header the first name is `Groceries`.
  - `Should_CreateCategory_When_InputValid`: 201, `Location` set, the property set equals the six contract names, name trimmed, `system` null; the list shows it after `Gifts` and before `Other` in the expense block.
  - `Should_ReturnNameTaken_When_NameExistsInKind`: `" groceries "` expense → `name: ["validation.categoryNameTaken"]`; `"Groceries"` income → 201.
  - `Should_FilterByKind`: `kind=income` gives 8 items; `kind=x` → `kind: invalidValue`; `pageSize=101` → `pageSize: invalidValue`.
  - `Should_ShowNewCategory_When_ListWasCached`, `Should_IsolateUsers`, `Should_Return401_When_NotAuthenticated`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: list and create categories with a starter set per user`

### Task 4: Update and delete categories

**Files:** `Features/Categories/UpdateCategory/*` (including `UpdateCategoryKindValidator.cs`), `Features/Categories/DeleteCategory/*`; tests `tests/Mita.Api.UnitTests/Features/Categories/UpdateCategoryValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Categories/UpdateDeleteCategoryTests.cs`.

**Interfaces:**
- Consumes: Task 3 (`CategoryInputValidator<T>`, `CategoryNameValidator<T>`, `CategoryInputParser`, `ProjectCategory`, `CategoryCacheTags`); `TestData.AddTransactionAsync` (Task 1).
- Produces:
  - `UpdateCategoryCommand(Guid Id, …ICategoryInput) : IInvalidatesCache` (tag `categories`). `PUT /v1/categories/{id:guid}` answers 200 `CategoryDto`, or 404.
  - `UpdateCategoryKindValidator(AppDbContext, ICurrentUser)`: `kind: validation.categoryKindLocked` when the kind parses, the category exists for the user, the kind differs, and it is a system category or in use; skipped otherwise. The handler locks the row `FOR UPDATE` and re-checks the same rule.
  - `DeleteCategoryCommand(Guid Id) : IInvalidatesCache` (tag `categories`). `DeleteCategoryValidator`: `id: validation.systemCategory` for an existing system category of the user. Handler, in one database transaction: lock the category `FOR UPDATE` (404 when absent), `ExecuteUpdateAsync` its transactions to the user's `other` of the same kind, remove it, commit; 204.
- [ ] **Step 1: Failing tests**
  - Validator: the Task 3 rows hold for the update command (one theory through `ICategoryInput`).
  - `Should_UpdateNameAndColor_When_SystemCategory`: renaming `Other` to `Misc` with `chart-3` gives 200.
  - `Should_ReturnKindLocked_When_CategoryInUse` and `…_When_SystemCategory`; `Should_ChangeKind_When_CategoryUnused`.
  - `Should_AllowOwnName_When_Updating` (same name, other case) and `Should_ReturnNameTaken_When_RenamingToSibling`.
  - `Should_ReturnSystemCategory_When_DeletingSystemCategory`: 400 `id: ["validation.systemCategory"]` for `Other` and `Transfers`.
  - `Should_ReassignTransactionsToOther_When_DeletingUsedCategory`: 204; the transaction's `categoryId` is now the expense `Other`; the list no longer has the category; `Other.inUse` is true.
  - `Should_DeleteCategory_When_Unused`; `Should_Return404_When_CategoryBelongsToAnotherUser` for PUT and DELETE.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: update and delete categories with kind lock and reassignment`

### Task 5: Create and read transactions

**Files:** `Common/Transactions/{TransactionDto,TransactionQueries,TransactionPrice,ITransactionInput,TransactionInputValidator,TransactionReferencesValidator,TransactionInputParser,TransactionWriter}.cs`, `Features/Transactions/CreateTransaction/*`, `Features/Transactions/GetTransaction/*`, `Features/Transactions/TransactionsEndpoints.cs`, `Program.cs`; tests `tests/Mita.Api.UnitTests/Features/Transactions/TransactionPriceTests.cs`, `CreateTransactionValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Transactions/CreateTransactionTests.cs`.

**Interfaces:**
- Consumes: `Transaction`, `TransactionDetails` (Task 1); `JsonValues`, `UtcCalendar`, `ManualRate`, `RateTable` (Task 2); `CategoryWire`, `CategoryCacheTags` (Task 3); `AccountCacheTags`, `CurrencyWire`, `IsoDate`.
- Produces:
  - `sealed record TransactionDto(Guid Id, string Kind, string Date, string Title, string? Note, Guid CategoryId, Guid AccountId, long Amount, string Currency, long AccountAmount, double Rate, long BaseAmount)` and `TransactionQueries.ToDto` as an expression for `Select`.
  - `sealed record TransactionPrice(decimal Rate, long AccountAmount, long BaseAmount)` with `static TransactionPrice? Compute(long amount, Currency currency, Currency accountCurrency, Currency baseCurrency, decimal? manualRate, IReadOnlyDictionary<Currency, decimal> plnValues)`: the Pricing rule; `null` when a needed rate is absent.
  - `interface ITransactionInput` with `JsonElement? Kind, Date, Title, Note, CategoryId, AccountId, Amount, Currency, Rate` and `Guid? ExistingId`.
  - `abstract class TransactionInputValidator<T>(TimeProvider)`: the synchronous rules; a non-GUID `categoryId` is `unknownCategory`, a non-GUID `accountId` `unknownAccount`.
  - `TransactionReferencesValidator<T>(AppDbContext, ICurrentUser)`: one query for the category (`unknownCategory` when missing, foreign or `Transfer`; `categoryKindMismatch` when `kind` parses and differs) and one for the account (`unknownAccount`; `archivedAccount` unless it equals the existing transaction's account); runs only for fields that passed the synchronous rules.
  - `TransactionInputParser.Parse(ITransactionInput) → ParsedTransaction(CategoryKind Kind, DateOnly Date, string Title, string? Note, Guid CategoryId, Guid AccountId, long Amount, Currency Currency, decimal? ManualRate)`.
  - `sealed class TransactionWriter(AppDbContext db)` (scoped, registered where the API registers its other services) with `Task<Result<TransactionDetails>> PrepareAsync(Guid userId, ParsedTransaction input, Guid? existingAccountId, CancellationToken ct)`, called inside the caller's database transaction:
    1. `SELECT "BaseCurrency" FROM user_settings WHERE "UserId" = … FOR SHARE`;
    2. lock the account and the category `FOR SHARE` and re-check the reference rules (same codes on a race);
    3. `TransactionPrice.Compute` with `db.PlnValuesAsync(date)`; `null` → `Error.Unavailable("rates.unavailable")`; `AccountAmount ≤ 0` → `rate: positiveAmount`; above `MinorUnits.MaxAmount` → `rate: invalidValue`.
  - `CreateTransactionCommand : ITransactionInput, IInvalidatesCache` (tags `accounts`, `categories`). `POST /v1/transactions` answers 201, `Location: /v1/transactions/{id}`, `TransactionDto`. A `23503` on save answers `categoryId: ["validation.unknownCategory"]`.
  - `GetTransactionQuery(Guid Id)`: `GET /v1/transactions/{id:guid}` answers 200 `TransactionDto` or 404. Not cached.
- [ ] **Step 1: Failing tests**
  - `TransactionPrice` rows (EUR 4.2717, USD 3.93, PLN account unless stated):
    - PLN 1 000, base PLN → rate 1, account 1 000, base 1 000;
    - EUR 10 000, base PLN → rate 4.2717, account 42 717, base 42 717;
    - EUR 10 000, base EUR → account 42 717, base 10 000;
    - EUR 10 000, base USD → account 42 717, base 10 869;
    - EUR 10 000, manual 4.5, base PLN → rate 4.5, account 45 000, base 45 000;
    - PLN 1 000 into a PLN account, manual 4.5 → rate 1 (ignored);
    - EUR with no EUR value → `null`; PLN into a PLN account with base USD and no USD value → `null`.
  - Validator rows: `kind` missing → required, `"transfer"` → invalidValue; `date` as in transfers (required, invalidValue, dateTooFar at today + 1 year + 1 day); `title` `5` → invalidValue, 101 characters → titleTooLong, `"a\u0007"` → invalidValue; `note` 201 characters → noteTooLong; `categoryId` missing → required, `"cat_x"` → unknownCategory; `accountId` `"acc_x"` → unknownAccount; `amount` `0`, `-5` → positiveAmount, `1.5`, `"10"`, `100000000001` → invalidValue; `currency` `"GBP"` → invalidValue; `rate` `0`, `"4"`, `1000001` → invalidValue, `1e-9` passes.
  - `Should_CreateTransaction_When_SameCurrency`: expense 1 250 PLN on a PLN account with `title: "  Bread "`, `note: "   "`: 201, `Location` set, the property set equals the twelve contract names, `title` `"Bread"`, `note` null, rate 1, `accountAmount` and `baseAmount` 1 250; without `title` the stored title is `""`; the account balance drops by 1 250, the account and the category are `inUse`; an income raises the balance.
  - `Should_ConvertIntoAccountCurrency_When_CurrencyDiffers` (isolated data, EUR 4.2717 on 2026-03-02): EUR 10 000 into a PLN account gives rate 4.2717, 42 717, 42 717; manual 4.5 gives 45 000.
  - `Should_UseBaseRate_When_BaseCurrencyDiffers` (isolated data, EUR 4.2717 and USD 3.93; base set to USD through `PUT /v1/settings`): `baseAmount` 10 869.
  - `Should_Return503_When_RatesMissing` (isolated data, no rates): cross-currency without a manual rate → 503 `rates.unavailable`.
  - `Should_ReturnPositiveAmount_When_ConvertedAmountIsZero` (rate `1e-9`) and `Should_ReturnInvalidRate_When_ConvertedAmountAboveMax` (100 000 000 000 PLN into an EUR account at rate 2).
  - `Should_ReturnFieldErrors_When_ReferencesInvalid`: a foreign category and the `Transfers` category → unknownCategory; an income category with `kind: "expense"` → categoryKindMismatch; a foreign account → unknownAccount; an archived account → archivedAccount.
  - `Should_GetTransaction_When_Owned`: equals the created body; a foreign id → 404.
  - `Should_NotFail_When_CategoryDeletedWhileTransactionCreated`: 20 rounds of a fresh category, `DELETE /v1/categories/{id}` and `POST /v1/transactions` in parallel; every delete answers 204, every create 201 or 400 `categoryId: ["validation.unknownCategory"]`, none 500, and every stored transaction points at an existing category.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: create transactions converted into the account currency`

### Task 6: List transactions with filters, search, sorting and sum

**Files:** `Features/Transactions/ListTransactions/*`; tests `tests/Mita.Api.UnitTests/Features/Transactions/ListTransactionsValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Transactions/ListTransactionsTests.cs`.

**Interfaces:**
- Consumes: `TransactionDto`, `TransactionQueries` (Task 5); `CategoryWire.TryParse(string?)` (Task 3); `PagingRules`, `IsoDate`.
- Produces:
  - `ListTransactionsQuery(string? Search, string? CategoryId, string? AccountId, string? Kind, string? From, string? To, string? Sort, int Page, int PageSize) : IRequest<Result<TransactionPageResponse>>`, not cached; endpoint defaults 1 and 20.
  - `sealed record TransactionPageResponse(IReadOnlyList<TransactionDto> Items, int Page, int PageSize, int Total, long Sum, string Currency)`.
  - `ListTransactionsValidator`: paging rules; `kind`, `from`, `to`, `sort`, `categoryId`, `accountId` invalidValue when present and unparseable.
  - The handler builds one filtered `IQueryable<Transaction>` (user, kind, category, account, `Date` between `from` and `to`, search from Decisions with the category name through a correlated `db.Categories` lookup), then: `CountAsync`, `SumAsync(t => t.Kind == Income ? t.BaseAmount : -t.BaseAmount)`, ordered page, and the base currency from `user_settings`.
- [ ] **Step 1: Failing tests**
  - Validator rows: `kind=x`, `from=2026-02-30`, `to=bad`, `sort=price`, `categoryId=abc`, `accountId=abc`, `pageSize=101`, `page=0` → invalidValue under that name; all null is valid.
  - `Should_ReturnTransactionPage_When_NoFilters`: properties `items, page, pageSize, total, sum, currency`; `pageSize` 20; `currency` `"PLN"`.
  - `Should_SortByDateThenInsertion`: t1 on 2026-10-01, t2 and t3 on 2026-10-02 (inserted in that order): default gives t3, t2, t1; `sort=date` gives t1, t2, t3.
  - `Should_SortByBaseAmount_When_SortIsAmount` (isolated data, EUR 4.2717): EUR 1 000 (base 4 272) and PLN 2 000; `-amount` gives EUR first, `amount` PLN first; equal amounts fall back to newest first.
  - `Should_Filter_ByKindCategoryAccountAndDates`: each filter alone and combined; `from`/`to` inclusive; `from > to` gives an empty page.
  - `Should_MatchSearch_InTitleNoteAndCategoryName`: `"BREAD"` matches title `Bread`, `"birthday"` matches note `Birthday cake`, `"groc"` matches the `Groceries` category; `"  "` is no filter.
  - `Should_MatchAmountPrefix_When_SearchLooksLikeAmount`: amounts 1 250, 12 000, 31 250: `"12"` gives 1 250 and 12 000, `"12,5"` gives 1 250, `"312"` gives 31 250, `"12.505"` gives none.
  - `Should_TreatSearchLiterally_When_ContainsWildcards`: titles `100% juice`, `a_b`, `back\slash`, `plain`: `"%"`, `"_"` and `"\"` each match exactly one.
  - `Should_SumSignedBaseAmounts_OverAllPages`: income 5 000 and expense 1 200 with `pageSize=1` give `total` 2 and `sum` 3 800 on both pages.
  - `Should_IsolateUsers`, `Should_Return401_When_NotAuthenticated`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.** If Npgsql cannot translate the amount text, the handler uses a Dapper query with the same SQL semantics; record it in the ledger.
- [ ] **Step 3: Commit** — `feat: list transactions with filters, search, sorting and a base currency sum`

### Task 7: Update and delete transactions

**Files:** `Features/Transactions/UpdateTransaction/*`, `Features/Transactions/DeleteTransaction/*`; tests `tests/Mita.Api.IntegrationTests/Features/Transactions/UpdateDeleteTransactionTests.cs`.

**Interfaces:**
- Consumes: `TransactionInputValidator<T>`, `TransactionReferencesValidator<T>`, `TransactionInputParser`, `TransactionWriter` (Task 5).
- Produces:
  - `UpdateTransactionCommand(Guid Id, …ITransactionInput) : IInvalidatesCache` (tags `accounts`, `categories`), `ExistingId = Id`. Handler: read the row's account without a lock (404 when absent or foreign), begin a transaction, `TransactionWriter.PrepareAsync(…, existingAccountId)`, then lock the transaction row `FOR UPDATE` (404 if deleted meanwhile), `Update(details)`, commit. `PUT /v1/transactions/{id:guid}` answers 200 `TransactionDto`. Pricing is recomputed with the current base currency, as the mock does.
  - `DeleteTransactionCommand(Guid Id) : IInvalidatesCache` (tags `accounts`, `categories`). `DELETE /v1/transactions/{id:guid}` answers 204 or 404.
- [ ] **Step 1: Failing tests**
  - `Should_UpdateAllFields_When_InputValid`: moving an expense of 1 000 from account A to B with amount 1 500 restores A and lowers B by 1 500; `GET` shows every new field.
  - `Should_RecomputeAmounts_When_UpdateChangesCurrency` (isolated data, EUR 4.2717): PLN 1 000 changed to EUR 1 000 gives rate 4.2717 and 4 272.
  - `Should_AllowArchivedAccount_When_TransactionKeepsIt`: archive the account, change the title, 200; `Should_ReturnArchivedAccount_When_MovingToArchivedAccount`: 400 `accountId: ["validation.archivedAccount"]`.
  - `Should_KeepListPosition_When_Updated`: updating the older of two same-day transactions keeps it second in `-date` order.
  - `Should_Return404_When_TransactionBelongsToAnotherUser` for PUT and DELETE; an invalid body on an unknown id answers 400.
  - `Should_DeleteTransaction_And_RestoreBalance`: 204; the balance returns to the opening balance, `inUse` is false, and `DELETE /v1/accounts/{id}` now answers 204.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: update and delete transactions`

### Task 8: Recompute base amounts when the base currency changes

**Files:** `Common/Transactions/TransactionBaseAmounts.cs`, `Features/Settings/UpdateSettings/UpdateSettingsHandler.cs`; tests `tests/Mita.Api.IntegrationTests/Features/Settings/BaseCurrencyChangeTests.cs`.

**Interfaces:**
- Consumes: `pln_value` (Task 1), `TransactionWriter` lock order (Task 5).
- Produces:
  - `static class TransactionBaseAmounts` with `Task<bool> RecomputeAsync(this AppDbContext db, Guid userId, Currency baseCurrency, CancellationToken ct)`:
    1. returns false when any of the user's transactions with `Currency <> base` needs a currency (its account's currency or the base) for which `pln_value` is null;
    2. else runs one `ExecuteSqlAsync`: `UPDATE transactions t SET "BaseAmount" = CASE WHEN t."Currency" = {base} THEN t."Amount" ELSE round(t."AccountAmount" * round(pln_value(a."Currency", t."Date") / pln_value({base}, t."Date"), 6)) END FROM accounts a WHERE a."Id" = t."AccountId" AND t."UserId" = {userId}`, and returns true.
  - `UpdateSettingsHandler`: in a database transaction, loads the settings row `FOR UPDATE`; when the base currency changes, calls `RecomputeAsync`; false → `Error.Unavailable("rates.unavailable")` and nothing is saved.
- [ ] **Step 1: Failing tests** (isolated data, EUR 4.2717 and USD 3.93 on 2026-03-02)
  - `Should_MatchFreshPricing_When_BaseCurrencyChanges`: with base PLN, create EUR 10 000 and PLN 2 000 expenses on a PLN account; switch to EUR: `baseAmount` 10 000 and 468 (`round(2 000 × round(1 / 4.2717, 6))`), `currency` `"EUR"`, `sum` −10 468; switch to USD: every `baseAmount` equals the one a new identical transaction gets under base USD (create it, compare, delete it).
  - `Should_Return503AndKeepAmounts_When_RatesMissing` (no rates, one EUR transaction with a manual rate): `PUT /v1/settings` to USD gives 503 `rates.unavailable`; settings and `baseAmount` are unchanged.
  - `Should_ChangeBaseCurrency_When_UserHasNoTransactions` (no rates): 200.
  - `Should_Return503_When_SameCurrencyTransactionsNeedBaseRate` (no rates, one PLN transaction on a PLN account): switching to EUR needs the EUR value, so 503.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: recompute transaction base amounts when the base currency changes`

### Task 9: Serialise startup migrations

**Files:** `Extensions/MigrationExtensions.cs`; tests `tests/Mita.Api.IntegrationTests/Persistence/MigrationTests.cs` (extended).

**Interfaces:**
- Produces: `ApplyMitaMigrationsAsync` opens the context connection, takes `SELECT pg_advisory_lock(7362543157)` (a fixed key), runs `MigrateAsync`, and releases it with `pg_advisory_unlock` in `finally` before closing the connection.
- [ ] **Step 1: Failing test** — `Should_MigrateOnce_When_TwoHostsStartTogether`: two factories on one fresh database with `Database:ApplyMigrationsOnStartup=true` start in parallel (`Task.WhenAll` of `CreateClient`), both answer `/health/ready` 200, and `__EFMigrationsHistory` lists each migration once. Repeat 5 times on fresh databases.
- [ ] **Step 2: Run, see it fail (42P07 or a duplicate key), implement, run, see it pass.**
- [ ] **Step 3: Commit** — `fix: serialise startup migrations with an advisory lock`

### Task 10: API contract, OpenAPI and the Docker smoke

**Files:** `docs/api-contract.md` (endpoint rows; `Category`, `CategoryInput`, `Transaction`, `TransactionInput`, `TransactionPage` types; the query parameters; the new codes `categoryNameTaken`, `categoryKindLocked`, `systemCategory`, `unknownCategory`, `categoryKindMismatch`, `titleTooLong`, `noteTooLong`; 503 `rates.unavailable` also on `POST`/`PUT /v1/transactions` and `PUT /v1/settings`; `balance` and `inUse` now include transactions; the starter set; search, sort and `sum` semantics), `tests/Mita.Api.IntegrationTests/OpenApiTests.cs`.
- [ ] **Step 1: Failing test** — `Should_DescribeCategoriesAndTransactions`: the document contains `/v1/categories`, `/v1/categories/{id}`, `/v1/transactions`, `/v1/transactions/{id}` with tags Categories and Transactions.
- [ ] **Step 2: Implement, run, see it pass;** Docker smoke (script in `dock.mita-work/scripts/ct-docker-smoke.sh`):
  1. `docker compose up -d --build`;
  2. register with a curl cookie jar; `GET /v1/categories` has 18 items;
  3. create a PLN account with 10 000; create an expense of 1 250 in `Groceries`; the account balance is 8 750 and `GET /v1/transactions` has `sum` −1 250;
  4. once rates are synced, `PUT /v1/settings` with base EUR gives 200 and the list `currency` is `EUR`;
  5. `DELETE` the `Groceries` category gives 204 and the transaction is in `Other`; delete the transaction, then the account (204 each);
  6. `docker compose down -v`.
- [ ] **Step 3: Commit** — `docs: api contract for categories and transactions`

### Task 11: Frontend hybrid mode against real categories and transactions

Frontend repo, branch `feature/hybrid-categories-and-transactions` from `origin/develop`.

**Files:** `.env.example` (`NUXT_PUBLIC_API_REAL_PATHS=/v1/auth,/v1/me,/v1/settings,/v1/rates,/v1/accounts,/v1/transfers,/v1/categories,/v1/transactions`), the project guide in the repository root (section "Running against the backend": the eight prefixes are real by default; budgets, goals, overview and reports stay on mocks with cloned demo data and do not see real accounts, categories or transactions yet; the accounts spec no longer needs an opt-in), new `tests/e2e-hybrid/transactions.hybrid.spec.ts`; outside the repo `dock.mita-work/scripts/al-hybrid-smoke.sh` (the same eight prefixes).
- [ ] **Step 1: Write `transactions.hybrid.spec.ts`** (English UI; labels checked against the real components):
  1. register a unique user; `/transactions` shows the first-use empty state;
  2. add the account "Wallet" (Cash, PLN, 100) on `/accounts`;
  3. add an expense of 12.50 "Bread" in "Groceries" from "Wallet" on `/transactions`; the row shows "Bread" and the "Wallet" card shows 87.50;
  4. search "groc" keeps the row, search "zzz" shows the filtered empty state;
  5. edit the amount to 20.00; the card shows 80.00;
  6. in Settings, manage categories: add "Pets", delete "Groceries"; the transaction now shows "Other";
  7. delete the transaction; the card shows 100.00;
  8. `/` and `/budgets` render their headings without the error state;
  9. delete the user in Settings.
- [ ] **Step 2: Run** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/scripts/al-hybrid-smoke.sh` (backend branch from Tasks 1-10 checked out); the three specs pass.
- [ ] **Step 3: Run** `bun run lint && bun run typecheck && bun run test` in the frontend; all green.
- [ ] **Step 4: Commit** — `test: hybrid e2e against real categories and transactions`

### Task 12: Pull requests

- [ ] Backend:
  1. Rebase on `origin/develop` and fold fix commits into their task commits.
  2. Run `dotnet build -warnaserror` and `dotnet test --logger junit` (Docker running); all must be green.
  3. Push, open a PR to `develop` (plain description), wait for `check-source-branch`, then `gh pr merge --rebase`.
- [ ] Frontend: same flow for `feature/hybrid-categories-and-transactions` after the backend PR is merged.
