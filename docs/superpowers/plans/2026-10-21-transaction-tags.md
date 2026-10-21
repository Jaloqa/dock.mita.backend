# Transaction Tags Implementation Plan

**Goal:** Free-form tags per transaction across both repos: entered as chips with suggestions in the transaction form (new tags created on the fly), shown as small chips on every list row, usable as a filter next to category and account (the base-currency `sum` follows it), found by the search, exported in the CSV, and managed (rename, delete) in Settings > Data.

**Architecture:**
- **Domain:** `Tag` (per user, unique name case-insensitively) and the join entity `TransactionTag` live in `Mita.Domain/Tags`. `Transaction` gets a read-only `Tags` navigation used only by the DTO projection; join rows are written by a shared helper, never through the navigation.
- **Writes:** `POST`/`PUT /v1/transactions` take `tags` as a list of names. Inside the existing database transaction, after `TransactionWriter.PrepareAsync` (settings → account → category locks), one `INSERT … ON CONFLICT ("UserId", lower("Name")) DO UPDATE … RETURNING` upserts and row-locks the tags in `lower(name)` order, then the transaction row is written and its join rows replaced. Lock order: settings → accounts → categories → tags → transactions.
- **Reads:** `TransactionDto` gains `tags: { id, name }[]`. `GET /v1/transactions` gains `tagIds` (comma-separated, any-of) and matches tag names in `search`. A new slice `Features/Tags/{ListTags,RenameTag,DeleteTag}` serves `GET /v1/tags`, `PUT /v1/tags/{id}`, `DELETE /v1/tags/{id}`. There is no `POST /v1/tags`.
- **Frontend:** everything lives in the transactions feature (`app/features/transactions`), the only feature using tags; `pages/settings.vue` composes its `TagManagerDialog` exactly as it already composes `useTransactionExport`. The mock backend implements the same rules so the mock E2E suite covers the UI.

**Tech Stack:** Backend: .NET 10, EF Core migrations (Npgsql), MediatR 12.5.0, FluentValidation, xUnit v3, Shouldly, Moq, Bogus, Testcontainers. Frontend: Nuxt 4, PrimeVue 4 (`AutoComplete` multiple), TanStack Query, Vuelidate, bun:test, Playwright 1.64.1. No new packages in either repo.

**Spec:** The user's decisions for this feature (chips with suggestions in the form, chips on rows, a tag filter next to category/account with the `sum` following it, rename and delete in Settings > Data next to "Manage categories"; no per-tag reports, colours, icons or tags on transfers), plus:
- Backend: the project guide in the repository root, `docs/STACK.md`, `docs/api-contract.md` ("Categories and transactions", "Errors"), `docs/superpowers/plans/2026-10-19-categories-and-transactions.md` (rules, lock order, search semantics), `src/Mita.Api/Features/Transactions/*`, `src/Mita.Api/Common/Transactions/*`, `src/Mita.Api/Common/Categories/*`, `src/Mita.Api/Common/Paging.cs`.
- Frontend: the project guide in the repository root, `docs/STACK.md`, `app/shared/api/contract.ts`, `app/shared/api/invalidation.ts`, `app/features/transactions/*`, `app/features/categories/components/CategoryManagerDialog.vue`, `app/features/settings/components/DataSection.vue`, `app/pages/settings.vue`, `app/pages/transactions/index.vue`, `mocks/db.ts`, `mocks/handlers/transactions.ts`, `mocks/seed/transactions.ts`, `tests/unit/i18n/locales.test.ts`.

## Decisions

| Topic | Decision | Cost if wrong |
|---|---|---|
| Tag name | Trimmed, 1 to 30 characters, no control characters (`char.IsControl`), stored with the first spelling the user typed. Internal whitespace is kept as typed | "Road  trip" and "Road trip" are two tags |
| Uniqueness | Per user, case-insensitive: unique index `IX_tags_user_name` on `("UserId", lower("Name"))`. When the input names an existing tag in another case, the stored spelling wins | None |
| Tags per transaction | At most 10 distinct names (after case-insensitive dedupe); duplicates in one request are merged silently | A user with more than 10 labels per row is refused |
| How tags are sent | `TransactionInput.tags: string[]` of names; the server upserts. Ids are never sent on writes. `tags` missing or `null` means no tags; on `PUT` it replaces the set, as `title` does | A future client that edits without sending `tags` clears them |
| Response shape | `TransactionDto.tags: { id, name }[]`, ordered by `lower(name)`, then `id`; always present (empty array) | One more property every list row carries |
| Filter | `GET /v1/transactions?tagIds=<id>,<id>`: up to 10 GUIDs, matches a transaction that has **any** of them, combined with the other filters by AND. Non-GUID or more than 10 → `tagIds: ["validation.invalidValue"]`; foreign or unknown ids just match nothing (as `categoryId`). Comma-separated because the client's query type is `Record<string, string \| number \| boolean>` | Users wanting "all of" need a second filter mode |
| Search | Also matches when one of the transaction's tag names contains the text (same literal, case-insensitive rule as the category name) | A search hit the user cannot see without the chips |
| Tag list | `GET /v1/tags?search&page&pageSize`: offset pages, `pageSize` default 100, max 100, ordered by `lower(name)`, then `id`; `search` trimmed, case-insensitive literal substring; items `TagDto { id, name, usage }` where `usage` is the number of the user's transactions with the tag | Prefix matches are not ranked first in suggestions |
| Rename collision | `PUT /v1/tags/{id}` to a name another tag of the user has (case-insensitively) → 400 `name: ["validation.tagNameTaken"]`; no merge. Changing only the case of the own name is allowed | Users must delete one tag and re-tag instead of merging |
| Delete | `DELETE /v1/tags/{id}` removes the tag and, by cascade, it from every transaction; 204 | None; nothing else references tags |
| Unused tags | Kept (usage 0, still suggested), deletable in Settings; no automatic cleanup | Typos linger until deleted by hand |
| Caching | Tags are not cached (usage changes with every transaction write, `search` is unbounded); no new cache tags | Every suggestion keystroke (debounced) reads PostgreSQL |
| Validation codes | `tags`: `validation.invalidValue` (not an array, an element not a string, blank or with a control character), `validation.tagTooLong` (> 30 after trim), `validation.tooManyTags` (> 10), first failing in that order. Rename `name`: `required`, `invalidValue`, `tagTooLong`, `tagNameTaken` | None for the UI |
| Concurrency | Upsert with `ON CONFLICT … DO UPDATE SET "Name" = tags."Name" RETURNING` (atomic even against a concurrent delete) in `lower(name)` order. Join rows are written only after that lock, so a concurrent tag delete either waits or has already finished (the upsert then recreates the tag); a `23503` on `FK_transaction_tags_tags_TagId` cannot occur and is not mapped | A rare wait while a tag used by a concurrent save is renamed or deleted |
| Quick add | The mobile quick-add panel stays without tags; tags are added in the full dialog | Mobile users tag in two steps |
| CSV | New last column `tags`, names joined by `"; "`, each name through `csvText` | A spreadsheet expecting the old column count gains one column |
| Frontend location | All tag code in `app/features/transactions` (`api/tagsApi.ts`, components, composables, `utils/tags.ts`); i18n under `transactions.tags.*` plus `settings.data.manageTags`. Moves to `shared/` when a third feature needs tags | A later move when reports per tag arrive |
| Suggestions | `AutoComplete` multiple with `auto-option-focus`: the first option is the exact case-insensitive match if one exists, else `+ "<text>"` (create), followed by up to 9 other matches not already chosen; Enter takes the focused option. The filter uses the same component without the create option | Enter on a partial text creates a new tag instead of picking the longer match (arrow down picks it) |
| Mock database | `MockState.tags: TagRow[]`, `TransactionRow.tagIds: string[]`; `version` stays 1, so an old `localStorage` state without `tags` fails `isState` and is reseeded | Mock data in a developer's browser resets once |
| Seed | Anna gets `abonament` (Internet UPC, Abonament Play, Karnet na siłownię), `praca` (Laptop — Media Expert, Zlecenie — projekt logo), `barcelona` (Booking.com — hotel Barcelona) and the unused `remont`; Oleh gets none | Test-only |

## Global Constraints

- Each repo's project guide (repository root) and `docs/STACK.md` are binding. No new packages. MediatR stays 12.5.0.
- Backend: warnings are errors; `Api → Infrastructure → Domain`; records, `sealed`, file-scoped namespaces, no comments; `CancellationToken` everywhere; every endpoint in the v1 group with `RequireAuthorization(AuthExtensions.UserPolicy)`; request fields bound as `JsonElement?`; a whitespace-only string counts as missing; first failing rule per field; validation (400) before lookup (404); routes use `{id:guid}`; every list paginated with `PagingRules`; tests `Should_X_When_Y`.
- Frontend: Biome clean, `bun run typecheck` 0 `error TS`, `bun test tests/unit` green, no `any`, every string through i18n in `en`, `pl`, `ru`, `uk` (parity test), plural forms for counts (3 forms in `pl`, `ru`, `uk`), inputs labelled, keyboard reachable, touch targets ≥ 44px.
- Contract, camelCase JSON, byte for byte between backend and mocks:
  - `TransactionTagDto { id, name }`; `TransactionDto` = the twelve existing fields plus `tags: TransactionTagDto[]`;
  - `TransactionInput` = the existing fields plus `tags?: string[]`;
  - `TransactionQuery` = the existing fields plus `tagIds?: string`;
  - `TagDto { id, name, usage }`; `TagInput { name }`; `GET /v1/tags` answers `Page<TagDto>`.
- Limits: `TAG_NAME_MAX = 30` (`Tag.NameMaxLength`), `TAGS_PER_TRANSACTION_MAX = 10` (`Tag.MaxPerTransaction`), filter `tagIds` at most 10.
- Commits: Conventional Commits, English; no tool attribution, no `Co-Authored-By`, no "Generated with" lines in commits, pull requests, code or committed files.

## Review Focus

- Two requests that create a transaction with the same new tag at the same moment must both succeed with one tag row, and a tag deleted while a transaction is being saved with it must neither answer 500 nor leave a dangling join row. Pinned in Task 3 (`Should_CreateOneTag_When_SameNewTagSavedConcurrently`) and Task 5 (`Should_NotFail_When_TagDeletedWhileTransactionSaved`).
- Typing a tag that exists in another case (`" GROCERIES-run "` vs `groceries-run`) must reuse the stored tag and spelling, never create a second one or answer `tagNameTaken`. Pinned in Task 3 (`Should_ReuseStoredSpelling_When_NameDiffersInCase`) and the mock unit test in Task 7.
- With two tags selected in the filter, a transaction carrying both must appear once, and `sum` and `total` must count it once. Pinned in Task 4 (`Should_CountTransactionOnce_When_ItHasSeveralFilteredTags`) and Task 7.
- Editing a transaction in the dialog without touching its tags must keep them; the quick-add panel must keep working without tags. Pinned in Task 8 (E2E "keeps tags when only the amount changes") and Task 3 (`Should_ClearTags_When_PutOmitsTags` documents the server side).
- Deleting or renaming a tag in Settings must update the chips on the transactions screen without a reload (cached transaction pages hold the old names). The filter state is page-local (`ref` in `useTransactionsScreen`), so leaving for Settings resets it and a deleted tag cannot stay selected. Pinned in Task 10 (`invalidationMap.tags`, E2E "renaming and deleting a tag updates the rows").

---

## File map

```
Backend (dock.mita.backend, branch feature/transaction-tags)
src/Mita.Domain/Tags/Tag.cs, TransactionTag.cs
src/Mita.Domain/Transactions/Transaction.cs (Tags navigation)
src/Mita.Infrastructure/Persistence/Configurations/TagConfiguration.cs, TransactionTagConfiguration.cs, TransactionConfiguration.cs
src/Mita.Infrastructure/Persistence/AppDbContext.cs, Migrations/<timestamp>_TransactionTags.cs
src/Mita.Api/Common/Tags/TagNames.cs, TagWriter.cs, TagDto.cs, TagQueries.cs, TransactionTagDto.cs
src/Mita.Api/Common/Transactions/{ITransactionInput,TransactionInputValidator,TransactionInputParser,TransactionDto,TransactionQueries}.cs
src/Mita.Api/Features/Transactions/{CreateTransaction,UpdateTransaction,ListTransactions}/*
src/Mita.Api/Features/Tags/{ListTags,RenameTag,DeleteTag}/*, TagsEndpoints.cs; Program.cs
docs/api-contract.md
tests/Mita.Api.UnitTests/Common/TagNamesTests.cs, Features/Tags/*, Features/Transactions/{CreateTransactionValidatorTests,ListTransactionsValidatorTests}.cs
tests/Mita.Api.IntegrationTests/Features/Tags/*, Features/Transactions/*, Persistence/MigrationTests.cs, Infrastructure/TestData.cs, OpenApiTests.cs

Frontend (dock.mita.frontend, branch feature/transaction-tags)
app/shared/api/contract.ts, app/shared/api/invalidation.ts
app/features/transactions/api/{tagsApi,keys,transactionsApi}.ts
app/features/transactions/utils/{tags,filters,csv}.ts, schemas/{transactionSchema,tagSchema}.ts, types.ts
app/features/transactions/composables/{useTagSuggestions,useTagList,useTagMutations,useTransactionsScreen,useTransactionExport}.ts
app/features/transactions/components/{TransactionTagField,TransactionTagFilter,TransactionTagChips,TagManagerDialog,TagManagerRow}.vue
app/features/transactions/components/{TransactionDialog,TransactionTable,TransactionDayList,TransactionFilterFields}.vue
app/features/settings/components/DataSection.vue, app/pages/settings.vue, app/pages/transactions/index.vue
mocks/db.ts, mocks/tags.ts, mocks/handlers/{tags,transactions,index}.ts, mocks/seed/{tags,transactions,index}.ts
i18n/locales/{en,pl,ru,uk}.json
tests/unit/mocks/tags.test.ts, tests/unit/mocks/transactions.test.ts, tests/unit/features/transactions/{tags,filters,csv,schemas}.test.ts, tests/unit/shared/invalidation.test.ts
tests/e2e/tags.spec.ts, tests/e2e/transactionsMobile.spec.ts, tests/e2e-real/tags.real.spec.ts
project guide in the repository root ("Real-backend suite" spec list)
```

---

### Task 1: Tag schema and the join table

Backend repo, branch `feature/transaction-tags`.

**Files:** `Mita.Domain/Tags/{Tag,TransactionTag}.cs`, `Mita.Domain/Transactions/Transaction.cs`, `Configurations/{TagConfiguration,TransactionTagConfiguration,TransactionConfiguration}.cs`, `AppDbContext.cs`, migration `TransactionTags`; tests `tests/Mita.Api.IntegrationTests/Persistence/MigrationTests.cs` (extended), `tests/Mita.Api.IntegrationTests/Persistence/TagCascadeTests.cs`, `Infrastructure/TestData.cs`.

**Interfaces:**
- Consumes: `IAuditable`, `AppUser`, `Transaction`, `TestData.AddAccountAsync/AddCategoryAsync/AddTransactionAsync`.
- Produces:
  - `sealed class Tag : IAuditable` with `Guid Id, Guid UserId, string Name`, `const int NameMaxLength = 30`, `const int MaxPerTransaction = 10`, `static Tag Create(Guid userId, string name)` (`Guid.CreateVersion7()`), `void Rename(string name)`.
  - `sealed class TransactionTag` with `Guid TransactionId, Guid TagId`, navigation `Tag Tag` (private setter), `static TransactionTag Create(Guid transactionId, Guid tagId)`.
  - `Transaction.Tags : IReadOnlyCollection<TransactionTag>` over a private `List<TransactionTag>` field (EF field access); `Create`/`Update` do not touch it.
  - `AppDbContext.Tags`, `AppDbContext.TransactionTags`.
  - Tables: `tags` (`Name` varchar(30) not null, FK user cascade, unique index `IX_tags_user_name` on `("UserId", lower("Name"))` as raw SQL in the migration); `transaction_tags` (PK `(TransactionId, TagId)`, FK `FK_transaction_tags_transactions_TransactionId` cascade, FK `FK_transaction_tags_tags_TagId` cascade, index `TagId`).
  - `TestData.AddTagAsync(WebApplicationFactory<Program>, Guid userId, string name) : Task<Tag>` and `TestData.TagTransactionAsync(WebApplicationFactory<Program>, Transaction transaction, params Tag[] tags) : Task`.
- [ ] **Step 1: Failing tests**
  - `Should_ApplyMigrations_When_DatabaseIsEmpty` (extended): tables `tags` and `transaction_tags` exist, index `IX_tags_user_name` exists.
  - `Should_RejectSecondTag_When_NameDiffersOnlyInCase`: adding `Trip` then `TRIP` for one user throws `DbUpdateException` with `PostgresException { SqlState: "23505", ConstraintName: "IX_tags_user_name" }`; `TRIP` for another user succeeds.
  - `Should_RemoveJoinRows_When_TagOrTransactionDeleted`: a transaction tagged `a` and `b`; deleting tag `a` leaves one join row; deleting the transaction leaves none and keeps tag `b`.
  - `Should_DeleteTags_When_UserDeleted`: `DELETE /v1/me` gives 204 and no `tags` or `transaction_tags` rows remain for that user.
- [ ] **Step 2: Run, see them fail.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/be-run.sh 'dotnet test --filter "FullyQualifiedName~MigrationTests|FullyQualifiedName~TagCascadeTests"'`
- [ ] **Step 3: Implement, then** `dotnet ef migrations add TransactionTags --project src/Mita.Infrastructure --startup-project src/Mita.Api` and add `CREATE UNIQUE INDEX "IX_tags_user_name" ON tags ("UserId", lower("Name"));` (and its `DROP INDEX` in `Down`) by hand.
- [ ] **Step 4: Run, see them pass,** then the full backend suite.
- [ ] **Step 5: Commit** — `feat: tags schema with a transaction join table`

### Task 2: Tag name parsing and the upsert helper

**Files:** `Common/Tags/{TagNames,TagWriter}.cs`; tests `tests/Mita.Api.UnitTests/Common/TagNamesTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Tags/TagWriterTests.cs`.

**Interfaces:**
- Consumes: `Tag`, `TransactionTag` (Task 1); `JsonValues`.
- Produces:
  - `static class TagNames`:
    - `const string Field = "tags"`, `InvalidValue = "validation.invalidValue"`, `TagTooLong = "validation.tagTooLong"`, `TooManyTags = "validation.tooManyTags"`;
    - `string? Error(JsonElement? element)`: `null` when valid; missing or JSON `null` is valid; else the first code of: not an array → invalidValue; an element not a string, blank after trim, or with a control character → invalidValue; an element over 30 after trim → tagTooLong; more than 10 distinct names (case-insensitive) → tooManyTags;
    - `IReadOnlyList<string> Parse(JsonElement? element)`: trimmed, deduped by `ToLowerInvariant()` keeping the first spelling, input order; empty for missing; throws `InvalidOperationException` when `Error` is not null;
    - `string? NameError(JsonElement? element)` for the rename body: `validation.required` / `invalidValue` / `tagTooLong`, and `bool TryParseName(JsonElement?, out string)`.
  - `static class TagWriter`:
    - `Task<IReadOnlyList<Guid>> UpsertAsync(this AppDbContext db, Guid userId, IReadOnlyList<string> names, CancellationToken ct)`: empty input returns empty without a query; otherwise one statement:
      ```sql
      INSERT INTO tags ("Id", "UserId", "Name", "CreatedAt", "UpdatedAt")
      SELECT n.id, {userId}, n.name, now(), now()
      FROM unnest({ids}::uuid[], {names}::text[]) AS n(id, name)
      ORDER BY lower(n.name)
      ON CONFLICT ("UserId", lower("Name")) DO UPDATE SET "Name" = tags."Name"
      RETURNING "Id" AS "Value"
      ```
      run through `db.Database.SqlQuery<Guid>`, where `ids` is a `Guid[]` built with `Guid.CreateVersion7()` per name. The `DO UPDATE` keeps the stored spelling and row-locks every tag until commit; the returned ids are a set (order not guaranteed).
    - `Task ReplaceAsync(this AppDbContext db, Guid transactionId, IReadOnlyList<Guid> tagIds, CancellationToken ct)`: `ExecuteDeleteAsync` of the transaction's join rows, then `AddRange` of the new ones (saved by the caller's `SaveChangesAsync`).
- [ ] **Step 1: Failing tests**
  - `TagNames.Error` rows: missing, `null`, `[]` → null; `"a"`, `{}` → invalidValue; `[5]`, `["  "]`, `["a\u0007"]` → invalidValue; `["a"*31]` → tagTooLong; `["a"*30]` → null; 11 distinct → tooManyTags; `["a","A"," a ", … 10 distinct]` (11 entries, 10 distinct) → null; `[5, "a"*31]` → invalidValue (first rule wins).
  - `TagNames.Parse`: `[" Trip ", "trip", "Food"]` → `["Trip", "Food"]`.
  - `TagNames.NameError`: missing → required, `5` → invalidValue, `"a\u0000"` → invalidValue, 31 characters → tagTooLong, `"  Trip "` → null and `TryParseName` gives `"Trip"`.
  - `Should_InsertMissingAndReuseExisting_When_Upserting`: stored `Trip`; upsert `["TRIP", "Food"]` returns two ids, one of them `Trip`'s id, and the stored names are `Trip`, `Food`.
  - `Should_CreateOneTag_When_UpsertedConcurrently`: 10 parallel scopes upsert `["Shared"]` for one user; every call returns the same id; one row exists.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.** `be-run.sh 'dotnet test --filter "FullyQualifiedName~TagNames|FullyQualifiedName~TagWriterTests"'`
- [ ] **Step 3: Commit** — `feat: tag name rules and an atomic tag upsert`

### Task 3: Transactions carry tags on create, update and read

**Files:** `Common/Tags/TransactionTagDto.cs`, `Common/Transactions/{ITransactionInput,TransactionInputValidator,TransactionInputParser,TransactionDto,TransactionQueries}.cs`, `Features/Transactions/CreateTransaction/{CreateTransactionCommand,CreateTransactionEndpoint,CreateTransactionHandler}.cs`, `Features/Transactions/UpdateTransaction/{UpdateTransactionCommand,UpdateTransactionEndpoint,UpdateTransactionHandler}.cs`; tests `tests/Mita.Api.UnitTests/Features/Transactions/CreateTransactionValidatorTests.cs` (extended), `tests/Mita.Api.IntegrationTests/Features/Transactions/TransactionTagsTests.cs`, the property-set assertions in `CreateTransactionTests.cs:48` and `ListTransactionsTests.cs:51`.

**Interfaces:**
- Consumes: `TagNames`, `TagWriter` (Task 2); `TransactionWriter.PrepareAsync`.
- Produces:
  - `sealed record TransactionTagDto(Guid Id, string Name)`.
  - `TransactionDto` gains a last parameter `IReadOnlyList<TransactionTagDto> Tags`; `TransactionQueries.ToDto` projects `transaction.Tags.OrderBy(link => link.Tag.Name.ToLower()).ThenBy(link => link.TagId).Select(link => new TransactionTagDto(link.TagId, link.Tag.Name)).ToList()` (same CA pragma as the search). `TransactionQueries.From`/`Compiled` are removed; both handlers return `await db.Transactions.AsNoTracking().Where(t => t.Id == id).Select(TransactionQueries.ToDto).SingleAsync(ct)` after commit.
  - `ITransactionInput.Tags : JsonElement?`; request and command records gain `JsonElement? Tags` last; endpoints pass `request?.Tags`.
  - `TransactionInputValidator`: `RuleFor(input => input.Tags).Must(value => TagNames.Error(value) is null)` with the code from `TagNames.Error` (use `.Custom` so the code is the computed one).
  - `ParsedTransaction` gains `IReadOnlyList<string> Tags` (from `TagNames.Parse`).
  - Handlers: after `PrepareAsync` succeeds, `var tagIds = await db.UpsertAsync(userId, input.Tags, ct)`; create adds the transaction then `ReplaceAsync(transaction.Id, tagIds)`; update locks the row `FOR UPDATE` (as now), `Update(details)`, `ReplaceAsync`; one `SaveChangesAsync`, commit.
- [ ] **Step 1: Failing tests**
  - Validator rows: `tags` `"a"` → invalidValue, `["a"*31]` → tagTooLong, 11 distinct → tooManyTags, missing and `null` → valid.
  - The two property-set assertions now list thirteen names ending with `"tags"`.
  - `Should_CreateTransactionWithTags_When_NamesGiven`: `tags: [" Trip ", "food", "trip"]` → 201, `tags` is `[{id, "food"}, {id, "Trip"}]`; `GET /v1/tags` has both with `usage` 1.
  - `Should_ReuseStoredSpelling_When_NameDiffersInCase`: stored `groceries-run`; creating with `" GROCERIES-run "` returns `groceries-run` with the stored id; still one tag.
  - `Should_ReturnEmptyTags_When_NoneGiven`: `tags` absent → `[]`.
  - `Should_ReplaceTags_When_Updated`: `[a, b]` then PUT `[b, c]` → `[b, c]`; `a` stays with `usage` 0.
  - `Should_ClearTags_When_PutOmitsTags`: PUT without `tags` → `[]`.
  - `Should_ReturnTagErrors_When_TagsInvalid`: 11 names → 400 `tags: ["validation.tooManyTags"]`; nothing is stored (no new tag rows).
  - `Should_NotStoreTags_When_TransactionRejected`: a valid `tags` with a foreign `accountId` → 400 and no tag rows created.
  - `Should_CreateOneTag_When_SameNewTagSavedConcurrently`: 10 parallel creates with `tags: ["Shared"]` → all 201, one `Shared` tag with `usage` 10.
  - `Should_IsolateTagsBetweenUsers`: user B creating `Trip` gets a different id than user A's `Trip`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite (overview, reports and budgets read transactions and must stay green).
- [ ] **Step 3: Commit** — `feat: tags on transactions, created on the fly by name`

### Task 4: Filter and search transactions by tags

**Files:** `Features/Transactions/ListTransactions/{ListTransactionsEndpoint,ListTransactionsQuery,ListTransactionsValidator,ListTransactionsHandler}.cs`; tests `tests/Mita.Api.UnitTests/Features/Transactions/ListTransactionsValidatorTests.cs` (extended), `tests/Mita.Api.IntegrationTests/Features/Transactions/ListTransactionsTests.cs` (extended).

**Interfaces:**
- Consumes: `AppDbContext.TransactionTags` (Task 1), `Tag.MaxPerTransaction`.
- Produces:
  - `ListTransactionsQuery` gains `string? TagIds` after `AccountId`; the endpoint binds `string? tagIds = null`.
  - `static class TagIdList` next to the query: `bool TryParse(string? value, out IReadOnlyList<Guid> ids)`: null or whitespace → true with empty; split on `,`, trim, every part a GUID, 1 to 10 distinct ids.
  - Validator: `tagIds` invalidValue when `TryParse` fails.
  - Handler filter: `transactions.Where(t => db.TransactionTags.Any(link => link.TransactionId == t.Id && ids.Contains(link.TagId)))`; search adds `|| db.TransactionTags.Any(link => link.TransactionId == t.Id && link.Tag.Name.ToLower().Contains(search))`.
- [ ] **Step 1: Failing tests**
  - Validator rows: `tagIds=abc`, `tagIds=<guid>,abc`, eleven GUIDs → invalidValue; one GUID, `"<g1>, <g2>"`, and a duplicated GUID → valid.
  - `Should_FilterByAnyTag_When_TagIdsGiven`: t1 `[trip]`, t2 `[food]`, t3 `[]`: `tagIds=trip` → t1; `tagIds=trip,food` → t1, t2; with `kind=income` → empty; a foreign tag id → empty page, 200.
  - `Should_CountTransactionOnce_When_ItHasSeveralFilteredTags`: t1 `[trip, food]` expense 1 000, t2 `[food]` expense 500: `tagIds=trip,food` → `total` 2, `sum` −1 500, t1 listed once.
  - `Should_MatchSearch_InTagNames`: t1 `[Barcelona]` titled `Hotel`: `search=barc` → t1; `search=%` matches only a tag named `100%`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: filter and search transactions by tags`

### Task 5: List, rename and delete tags

**Files:** `Common/Tags/{TagDto,TagQueries}.cs`, `Features/Tags/ListTags/{ListTagsEndpoint,ListTagsQuery,ListTagsValidator,ListTagsHandler}.cs`, `Features/Tags/RenameTag/{RenameTagEndpoint,RenameTagCommand,RenameTagValidator,RenameTagNameValidator,RenameTagHandler}.cs`, `Features/Tags/DeleteTag/{DeleteTagEndpoint,DeleteTagCommand,DeleteTagHandler}.cs`, `Features/Tags/TagsEndpoints.cs`, `Program.cs`; tests `tests/Mita.Api.UnitTests/Features/Tags/RenameTagValidatorTests.cs`, `ListTagsValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Tags/TagsTests.cs`.

**Interfaces:**
- Consumes: `TagNames.NameError/TryParseName` (Task 2), `PagingRules`, `PageResponse<T>`, `ToPageAsync`.
- Produces:
  - `sealed record TagDto(Guid Id, string Name, int Usage)`; `TagQueries.ProjectTags(this AppDbContext db, Guid userId, string? search) : IQueryable<TagDto>` (no tracking, `Usage = db.TransactionTags.Count(link => link.TagId == tag.Id)`, order `lower(Name)`, `Id`) and `ProjectTag(db, userId, id)`.
  - `ListTagsQuery(string? Search, int Page, int PageSize) : IRequest<Result<PageResponse<TagDto>>>`, not cached; endpoint defaults 1 and 100; validator: paging rules only. `GET /v1/tags`.
  - `RenameTagRequest(JsonElement? Name)`, `RenameTagCommand(Guid Id, JsonElement? Name)`; `RenameTagValidator` (sync `NameError`), `RenameTagNameValidator(AppDbContext, ICurrentUser)`: `name: validation.tagNameTaken` when another tag of the user has `lower(name)`, skipped when the name failed. Handler: load the tag tracked (`404` when absent or foreign), `Rename`, save; `23505` on `IX_tags_user_name` → the same 400. `PUT /v1/tags/{id:guid}` → 200 `TagDto`.
  - `DeleteTagCommand(Guid Id)`: `ExecuteDeleteAsync` scoped to the user; 0 rows → 404; `DELETE /v1/tags/{id:guid}` → 204.
  - `TagsEndpoints.MapTagsEndpoints` with tag `Tags`, registered in `Program.cs` after `MapTransactionsEndpoints`.
- [ ] **Step 1: Failing tests**
  - Rename validator rows from Task 2's `NameError` through the command; list validator: `pageSize=101`, `page=0` → invalidValue.
  - `Should_ListTagsWithUsage_OrderedByName`: tags `beta` (2 transactions), `Alpha` (0): items `Alpha` 0, `beta` 2; property set `id, name, usage`; `pageSize` 100.
  - `Should_FilterTagsBySearch`: `search=AL` → `Alpha`; `search=%` → only `100%`; `search=  ` → all.
  - `Should_PageTags`: 3 tags, `pageSize=2&page=2` → one item, `total` 3.
  - `Should_RenameTag_When_NameFree`: 200 trimmed name; the transaction's `tags` shows the new name.
  - `Should_AllowCaseChange_When_RenamingOwnTag`: `trip` → `Trip` 200.
  - `Should_ReturnNameTaken_When_RenamingToExistingTag`: `" FOOD "` while `food` exists → 400 `name: ["validation.tagNameTaken"]`.
  - `Should_DeleteTag_AndRemoveItFromTransactions`: 204; the transaction's `tags` no longer has it; `GET /v1/tags` lacks it.
  - `Should_Return404_When_TagBelongsToAnotherUser` for PUT and DELETE; an invalid body on an unknown id → 400; `Should_Return401_When_NotAuthenticated` for all three.
  - `Should_NotFail_When_TagDeletedWhileTransactionSaved`: 20 rounds of a fresh tag `T` used by one transaction, then in parallel `DELETE /v1/tags/{T}` and `POST /v1/transactions` with `tags: ["T"]`; every delete 204, every create 201, none 500, and every stored join row points at an existing tag (the create either reused `T` before the delete or recreated it after).
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: list, rename and delete tags`

### Task 6: API contract, OpenAPI and the backend check

**Files:** `docs/api-contract.md`, `tests/Mita.Api.IntegrationTests/OpenApiTests.cs`.
- [ ] **Step 1: Failing test** — `Should_DescribeTags`: paths `/v1/tags` and `/v1/tags/{id}` with tag `Tags`; the `/v1/transactions` GET lists the `tagIds` parameter.
- [ ] **Step 2: Contract.** Title gains "tags". Endpoint rows: `GET /v1/tags?search&page&pageSize` (200 `Page<Tag>`; 400, 401), `PUT /v1/tags/{id}` (`TagInput`, 200 `Tag`; 400, 401, 403, 404), `DELETE /v1/tags/{id}` (204; 401, 403, 404); the transactions list row gains `tagIds`. Types: `TransactionTag`, `Tag`, `TagInput`, `tags` on `Transaction` and `TransactionInput`. A "Tags" subsection under "Categories and transactions" with every rule of the Decisions table (name rules, uniqueness and stored spelling, 10 per transaction, replace semantics on `PUT`, any-of filter, search, list order and `usage`, rename collision, delete cascade, unused tags kept, not cached). Validation table: `validation.tagTooLong`, `validation.tooManyTags`, `validation.tagNameTaken`; `invalidValue` mentions `tags` and the `tagIds` query.
- [ ] **Step 3: Run** `be-run.sh 'dotnet build -warnaserror && dotnet test'`; all green.
- [ ] **Step 4: Commit** — `docs: api contract for transaction tags`

### Task 7: Frontend contract and the mock backend

Frontend repo, branch `feature/transaction-tags` from `origin/develop`.

**Files:** `app/shared/api/contract.ts`, `mocks/db.ts`, new `mocks/tags.ts`, new `mocks/handlers/tags.ts`, `mocks/handlers/transactions.ts`, `mocks/handlers/index.ts`, new `mocks/seed/tags.ts`, `mocks/seed/transactions.ts`, `mocks/seed/index.ts`; tests new `tests/unit/mocks/tags.test.ts`, `tests/unit/mocks/transactions.test.ts`, `tests/unit/mocks/seedTransactions.test.ts`, `tests/unit/mocks/db.test.ts`.

**Interfaces:**
- Produces:
  - `contract.ts`: `TransactionTagDto = { id: string; name: string }`; `TransactionDto.tags: TransactionTagDto[]`; `TransactionInput.tags?: string[]`; `TransactionQuery.tagIds?: string`; `TagDto = { id: string; name: string; usage: number }`; `TagInput = { name: string }`; `TAG_NAME_MAX = 30`, `TAGS_PER_TRANSACTION_MAX = 10`.
  - `mocks/db.ts`: `TagRow = { id: string; userId: string; name: string }`; `MockState.tags: TagRow[]`; `TransactionRow.tagIds: string[]`.
  - `mocks/tags.ts`: `tagsError(value: unknown): string | undefined`, `parseTags(value: unknown): string[]`, `upsertTags(state: MockState, userId: string, names: readonly string[]): string[]` (ids `newId('tag')`), `tagRefs(state: MockState, ids: readonly string[]): TransactionTagDto[]` (sorted by `toLocaleLowerCase()` then id), `tagUsage(state, userId): Map<string, number>`.
  - `mocks/handlers/tags.ts`: `registerTagHandlers(router)` for `GET /v1/tags`, `PUT /v1/tags/:id`, `DELETE /v1/tags/:id` with the backend rules and codes; delete also strips the id from every `tagIds`.
  - Transactions handler: validates `tags` (under `tags`, after the other fields' checks, before pricing), upserts only after every check passed, writes `tagIds`, returns `tags` in `toDto`, filters `tagIds` (any-of; more than 10 ids → `tagIds: invalidValue`; mock ids are not GUIDs, so no GUID check) and matches tag names in `search`.
  - `mocks/seed/tags.ts`: `demoTags(): TagRow[]` with ids `tag_anna_abonament`, `tag_anna_praca`, `tag_anna_barcelona`, `tag_anna_remont`; `Planned`/`Extra` gain `tags?: string[]` (ids) and the six planned rows from Decisions carry them; every other row gets `tagIds: []`.
- [ ] **Step 1: Failing tests** (`bun:test`, style of `tests/unit/mocks/transactions.test.ts`)
  - creates with `tags: [' Trip ', 'trip', 'food']` → `tags` `[{ name: 'food' }, { name: 'Trip' }]`; a second create with `'TRIP'` reuses the id and spelling;
  - `tags: 'a'` → `field('tags', 'validation.invalidValue')`; 31 characters → `tagTooLong`; 11 names → `tooManyTags`; a rejected create stores no tag;
  - PUT without `tags` clears them;
  - `tagIds` with two ids returns a doubly-tagged row once and `sum` counts it once; `search: 'barc'` finds the Barcelona row;
  - `GET /v1/tags` lists Anna's four seed tags in name order with usage (`remont` 0), `search`, `pageSize=101` → invalidValue;
  - rename to `' PRACA '` from `abonament` → `field('name', 'validation.tagNameTaken')`; rename `praca` → `Praca` 200; delete → 204 and no transaction keeps the id; foreign id → 404;
  - `db.test.ts`: a stored state without `tags` is reseeded.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/wsl-run.sh 'bun test tests/unit && bun run lint && bun run typecheck'`
- [ ] **Step 3: Commit** — `feat: tags in the api contract and the mock backend`

### Task 8: Tag field in the transaction form

**Files:** new `app/features/transactions/api/tagsApi.ts`, `api/keys.ts` (`tagKeys`), new `utils/tags.ts`, `schemas/transactionSchema.ts`, new `composables/useTagSuggestions.ts`, new `components/TransactionTagField.vue`, `components/TransactionDialog.vue`, `i18n/locales/*.json`; tests new `tests/unit/features/transactions/tags.test.ts`, `tests/unit/features/transactions/schemas.test.ts`, new `tests/e2e/tags.spec.ts`.

**Interfaces:**
- Consumes: Task 7 contract and mocks.
- Produces:
  - `tagsApi(api)`: `list(query: { search?: string; page: number; pageSize: number })`, `rename(id, body: TagInput)`, `remove(id)`.
  - `tagKeys = { all: ['tags'], list: ['tags', 'list'], suggestions: (search: string) => ['tags', 'suggestions', search] as const }`.
  - `utils/tags.ts`: `type TagOption = { name: string; create: boolean }`; `normalizeTag(name: string): string` (trim, lower-case); `tagSuggestions(text: string, existing: readonly string[], chosen: readonly string[]): TagOption[]` (the Decisions order, max 10, empty for blank text, nothing already chosen); `addTag(chosen: readonly string[], name: string): string[]` (trimmed, case-insensitive dedupe, keeps order).
  - `useTagSuggestions()`: `{ options: Ref<TagOption[]>, search(text: string): void }`, debounced 200 ms, `useQuery` on `tagKeys.suggestions(text)` with `pageSize: 10`.
  - `TransactionTagField.vue`: `defineModel<string[]>({ required: true })`, props `id`, `invalid`, `describedBy`; PrimeVue `AutoComplete` `multiple`, `auto-option-focus`, `option-label="name"`, option template shows `t('transactions.tags.create', { name })` for create options; chip remove buttons labelled `t('transactions.tags.remove', { name })`.
  - `TransactionForm.tags: string[]`; rule `tags: { tooMany: helpers.withMessage(t('validation.tooManyTags'), (v) => v.length <= TAGS_PER_TRANSACTION_MAX), tooLong: helpers.withMessage(t('validation.tagTooLong'), (v) => v.every((n) => n.trim().length <= TAG_NAME_MAX)) }`.
  - Dialog: a "Tags" `FormRow` after Category, `reset` fills `transaction.tags.map(tag => tag.name)` or `[]`, `submit` always sends `tags`, `fields` gains `'tags'` so server errors show.
  - i18n (all four locales): `transactions.tags.label`, `.placeholder`, `.create` (`+ “{name}”`), `.remove`, `validation.tagTooLong` ("Use at most 30 characters."), `validation.tooManyTags` ("Use at most 10 tags."), `validation.tagNameTaken` ("A tag with this name already exists.").
- [ ] **Step 1: Failing unit tests**
  - `tagSuggestions('tr', ['Trip', 'Travel', 'Food'], [])` → `[{ 'tr', create }, Trip, Travel]`; `('trip', ['Trip', 'Trips'], [])` → `[Trip, Trips]` (no create); `('trip', ['Trip'], ['TRIP'])` → `[]`; `('  ', …)` → `[]`; 15 matches → 10 options.
  - `addTag(['Trip'], ' trip ')` → `['Trip']`; `addTag([], ' Food ')` → `['Food']`.
  - Schema: 11 tags → tooManyTags; a 31-character tag → tagTooLong.
- [ ] **Step 2: Failing E2E** (`tests/e2e/tags.spec.ts`, both projects)
  - "adds an existing and a new tag to a transaction": open "Add transaction", fill amount, type `prac` in "Tags", pick `praca`, type `kino` and press Enter; the field shows both chips; save; the edited row's dialog shows both chips.
  - "keeps tags when only the amount changes": open a seeded `Booking.com — hotel Barcelona` row, change the amount, save, reopen: `barcelona` chip still there.
  - "refuses an eleventh tag": add ten tags; the eleventh shows "Use at most 10 tags." on save.
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite as in `dock.mita-work/environment.md` (port 3100, `NUXT_PUBLIC_API_MOCKS=true`); all pass.
- [ ] **Step 4: Commit** — `feat: tags with suggestions in the transaction form`

### Task 9: Chips on rows, tag filter and CSV

**Files:** `app/features/transactions/types.ts`, `utils/filters.ts`, `utils/tags.ts`, `utils/csv.ts`, `composables/useTransactionsScreen.ts`, `composables/useTransactionExport.ts`, new `components/TransactionTagChips.vue`, new `components/TransactionTagFilter.vue`, `components/{TransactionTable,TransactionDayList,TransactionFilterFields}.vue`, `app/pages/transactions/index.vue`, `i18n/locales/*.json`; tests `tests/unit/features/transactions/{filters,tags,csv}.test.ts`, `tests/e2e/tags.spec.ts`, `tests/e2e/transactionsMobile.spec.ts`.

**Interfaces:**
- Consumes: Task 8 (`useTagSuggestions`, `tagKeys`).
- Produces:
  - `TransactionView.tags: string[]`.
  - `TransactionFilters.tags: TransactionTagDto[]` (default `[]`); `hasNonPeriodFilters` counts it; `toQuery` adds `tagIds: tags.map((tag) => tag.id).join(',')` when non-empty.
  - `visibleTags(names: readonly string[], limit: number): { shown: string[]; hidden: number }`.
  - `TransactionTagChips.vue`: props `tags: string[]`, `limit: number`; a `<ul>` labelled `t('transactions.tags.label')`, `+N` with `t('transactions.tags.more', { count }, count)` (plural). Table: under the title, limit 3. Day list: under the category line, limit 2.
  - `TransactionTagFilter.vue`: `defineModel<TransactionTagDto[]>`, same `AutoComplete` without create options, placeholder `t('transactions.filters.allTags')`, at most 10. `TransactionFilterFields` gains `v-model:tags` and renders it after Account, so the desktop row and the mobile drawer both get it.
  - CSV: `csvColumns` gains `'tags'` last; the cell is `item.tags.map((tag) => csvText(tag.name)).join('; ')`; header `transactions.csv.tags`.
  - i18n: `transactions.tags.more` (plural), `transactions.filters.tags`, `transactions.filters.allTags`, `transactions.csv.tags`; `transactions.filters.searchPlaceholder` mentions tags ("Title, note, tag or amount").
- [ ] **Step 1: Failing unit tests** — `toQuery` with two tags gives `tagIds: 'tag_a,tag_b'` and none without; `isDefaultFilters` false with a tag; `emptyStateOf` with a tag is `filtered`; `visibleTags(['a','b','c','d'], 2)` → `{ shown: ['a','b'], hidden: 2 }`; CSV header ends with `Tags` and a row with tags `=x` and `y` ends with `'=x; y` (formula guard per name).
- [ ] **Step 2: Failing E2E**
  - `tags.spec.ts` (desktop): "filters by tag and the sum follows": period "All time", pick `barcelona` in the "Tags" filter; only the Booking.com row remains, its row shows the `barcelona` chip, the summary says `Found: 1 transaction` and the sum equals that row's base amount; add `praca` to the filter; rows with either tag remain; clear it.
  - `tags.spec.ts`: "exports tags in the CSV": filter by `barcelona`, export; the downloaded file's last column header is `Tags` and the row ends with `barcelona`.
  - `transactionsMobile.spec.ts`: "shows tag chips on cards and filters by tag in the drawer": a card of the Barcelona row shows the chip; the drawer's "Tags" filter narrows the list.
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck, mock E2E; all pass.
- [ ] **Step 4: Commit** — `feat: tag chips, tag filter and tags in the csv export`

### Task 10: Tag management in Settings

**Files:** new `app/features/transactions/composables/{useTagList,useTagMutations}.ts`, new `schemas/tagSchema.ts`, new `components/{TagManagerDialog,TagManagerRow}.vue`, `app/shared/api/invalidation.ts`, `app/features/transactions/composables/useTransactionMutations.ts` (unchanged API, now invalidates tags through the map), `app/features/settings/components/DataSection.vue`, `app/pages/settings.vue`, `i18n/locales/*.json`; tests `tests/unit/shared/invalidation.test.ts`, `tests/unit/features/transactions/schemas.test.ts`, `tests/e2e/tags.spec.ts`, `tests/e2e/settings.spec.ts`.

**Interfaces:**
- Consumes: `tagsApi`, `tagKeys` (Task 8).
- Produces:
  - `DataChange` gains `'tags'`: `[['tags'], ['transactions']]`; `transactions` gains `['tags']`.
  - `useTagList()`: `useInfiniteQuery` on `tagKeys.list`, `pageSize: 100`, `{ tags, total, hasNextPage, fetchNextPage, isPending, isError, refetch }`.
  - `useTagMutations()`: `rename` (`{ id, input: TagInput }`), `remove`, both `invalidateAfter(queryClient, 'tags')`; `isMissing(error)` as categories.
  - `tagRules(t, takenNames)`: `name` required, `tagTooLong` (> 30 trimmed), `tagNameTaken` (case-insensitive against the other loaded tags; the server stays the authority).
  - `TagManagerDialog.vue` (`v-model:visible`, header `t('transactions.tagManager.title')`): a list labelled "Tags", each `TagManagerRow` with name, usage `t('transactions.tagManager.usage', { count }, count)`, "Rename {name}" and "Delete {name}" buttons; rename edits inline (input labelled "Name", Save/Cancel, focus returns to the row's rename button); delete confirms with `t('transactions.tagManager.deleteMessage', { name, count }, count)` ("“{name}” will be removed from {count} transactions.") or `deleteUnusedMessage`; "Show more" when `hasNextPage`; empty state `t('transactions.tagManager.empty')`; toasts `renamed`, `deleted`.
  - `DataSection` emits `manageTags` from a "Manage tags" button (`settings.data.manageTags`) right after "Manage categories"; `settings.vue` renders `<TagManagerDialog v-model:visible="tagsOpen" />`.
- [ ] **Step 1: Failing unit tests** — `invalidationMap.tags` equals `[['tags'], ['transactions']]` and `invalidationMap.transactions` contains `['tags']`; `tagRules` rows (blank → required, 31 → tagTooLong, `' PRACA '` with `praca` taken → tagNameTaken, own name in another case → valid).
- [ ] **Step 2: Failing E2E**
  - `settings.spec.ts`: "Data" lists "Manage tags" after "Manage categories".
  - `tags.spec.ts` (both projects): "renaming and deleting a tag updates the rows": in Settings, "Manage tags" shows four tags with `remont` "0 transactions"; renaming `abonament` to `praca` shows "A tag with this name already exists."; rename it to `subskrypcja`; on `/transactions` (All time) a Play row shows `subskrypcja`; filter by `subskrypcja`, go back to Settings, delete it (the confirm names the count), return: no row shows the chip and the "Tags" filter is empty.
  - `tags.spec.ts`: "tag manager is localized" (`/pl/settings`: button and dialog title in Polish).
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck, mock E2E (desktop and mobile); all pass.
- [ ] **Step 4: Commit** — `feat: rename and delete tags in settings`

### Task 11: Real-backend spec, smoke and docs

Frontend repo, same branch; the backend checkout on `feature/transaction-tags` (Tasks 1-6).

**Files:** new `tests/e2e-real/tags.real.spec.ts`, the project guide in the repository root ("Real-backend suite": one line for `tags`; the auth-call count per run goes up by two).
- [ ] **Step 1: Write `tags.real.spec.ts`** (helpers in the style of `transactions.real.spec.ts`): register a unique user; add the account "Wallet" (Cash, PLN, 100); add an expense 12.50 "Bread" in "Groceries" with tags `food` (Enter) and `weekly` (Enter); add an expense 30.00 "Train" in "Transport" with `travel`; the "Bread" row shows `food` and `weekly`; the filter `food` leaves only "Bread" and the summary shows `−zł 12.50`; open "Train", type `fo`, pick the suggested `food`, save; the filter now shows both rows and `−zł 42.50`; in Settings rename `weekly` to `groceries-run` and delete `travel`; on `/transactions` "Bread" shows `groceries-run`, "Train" shows only `food`; delete the user in Settings.
- [ ] **Step 2: Run** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/scripts/rb-real-smoke.sh`; expected 7 passed, both smoke projects torn down, `pgdb` and `dock-mita-frontend-web-1` untouched.
- [ ] **Step 3: Run** `bun run lint && bun run typecheck && bun test tests/unit`; all green.
- [ ] **Step 4: Commit** — `test: real-backend spec for transaction tags`

### Task 12: Pull requests

- [ ] Backend: rebase `feature/transaction-tags` on `origin/develop`, fold fix-ups into their task commits, run `dotnet build -warnaserror` and `dotnet test --logger junit` (Docker running), push with `--force-with-lease`, open a PR to `develop` with a plain description, wait for `check-source-branch`, `gh pr merge --rebase`.
- [ ] Frontend: after the backend PR is merged, the same flow for its `feature/transaction-tags`, with the mock E2E suite and the smoke green.

## Deferred (not in this plan)

Reports and budgets per tag, tag colours and icons, tags on transfers and goal contributions, an "all of" filter mode, merging tags on rename, tags in the quick-add panel, ranking suggestions by usage or prefix.
