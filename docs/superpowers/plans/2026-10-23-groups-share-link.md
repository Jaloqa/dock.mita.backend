# Expense Groups Share Link Implementation Plan

**Goal:** The owner of an expense group can turn on a share link. Anyone with the link opens the group in a browser without signing in, picks "who am I" from the members once (remembered per browser, changeable), and then reads everything and adds, edits, deletes and restores every expense and repayment and adds group categories, acting as that member; a link holder can never change members or the group, never reach another group or any other API, and anonymous traffic is rate limited.

**Scope:** Plan 3b of Delivery item 3; plan 3a (`2026-10-23-groups.md`, merged) built groups for the owner. Out of scope (part 4): offering a signed-in visitor to link the chosen member to their account, email invites, personal accounting.

**Architecture:**
- **Backend:** a migration adds `groups.ShareToken` (null = link off). Owner routes `POST`/`DELETE /v1/groups/{id}/share`. A second route group `/v1/shared/groups/{token}` outside the user policy maps the read and entry slices of 3a again, guarded by a `GroupLink` authorization policy (the token must name a group whose link is on; failure answers 404) and a per-IP `groupLink` rate limit applied before authentication. `GroupScope` becomes owner-or-link, so `GroupAccess` resolves a link scope by token and the entry handlers stay as they are, except that writes open the group through `OpenForWriteAsync`, which requires the `X-Group-Member` header to name a current member (403 `group.memberRequired`) and records that member as the actor.
- **Frontend:** `GroupScope` gains `{ kind: 'link', token, memberId }`; the API client carries optional headers; the group components already talk through the scope, so a new page `/g/[token]` with its own layout renders them for a link after a "Who are you?" choice. The owner gets a Share dialog. The mock backend implements the same routes, header and rules.

**Tech Stack:** Backend: .NET 10, ASP.NET Core authorization (`IAuthorizationHandler`, `IAuthorizationMiddlewareResultHandler`), `Microsoft.AspNetCore.RateLimiting`, EF Core migrations (Npgsql), MediatR 12.5.0, FluentValidation, xUnit v3, Shouldly, Testcontainers. Frontend: Nuxt 4, PrimeVue 4.5, TanStack Query, Vuelidate, bun:test, Playwright 1.64.1. No new packages in either repo.

**Spec:** `dock.mita.frontend/docs/superpowers/specs/2026-10-22-tools-design.md`: Groups "Only the owner changes the group itself" (enables, disables and regenerates the share link), "Share link" (except the account-linking offer, part 4), "Authorship" (a link holder appears as the member they picked), Category suggestion's "Any member can add a group-specific category, link holders included", Anti-goal "Leaked groups", Left to the builder "Link security" and "Mobile behaviour for the who am I choice". Also binding: both repos' project guides and `docs/STACK.md`, backend `docs/api-contract.md` ("Expense groups" and "Rate limiting and lockout"), plan 3a's Decisions. Code to reuse: backend `Common/Groups/*`, `Features/Groups/*`, `Extensions/{AuthExtensions,RateLimitingExtensions,ApiVersioningExtensions}.cs`, `Authentication/{XsrfMiddleware,CurrentUser}.cs`, `Configuration/RateLimitingOptions.cs`, `Features/Rates/GetRates/*`, `Program.cs`; frontend `app/features/groups/*`, `app/shared/api/{client,transport,transports/http}.ts`, `app/shared/auth/publicRoutes.ts`, `app/middleware/auth.global.ts`, `app/shared/rates/{ratesApi,useRates}.ts`, `app/layouts/auth.vue`, `AuthPreferences.vue` (composed by a layout, as `auth.vue` does), `mocks/{api,router,db,groups}.ts`, `mocks/handlers/{groups,groupExpenses,groupRepayments,rates}.ts`, `mocks/seed/groups.ts`.

## Decisions

| Topic | Decision | Cost if wrong |
|---|---|---|
| Token | 32 bytes from `RandomNumberGenerator`, base64url without padding: 43 characters of `[A-Za-z0-9_-]`. Stored in clear in `groups.ShareToken` (unique index; null = link off) so the owner can copy the link again at any time | A database leak also leaks working links until the owner makes a new one (the leak already exposes the group itself) |
| Owner routes | `POST /v1/groups/{id}/share` → 200 `Group` with a new token: turns the link on, or replaces it (the old token stops working at once). `DELETE /v1/groups/{id}/share` → 204, link off (also when it was already off). Owner only, user policy, 404 for an unknown or foreign group. `Group.shareToken` is the token for the owner and `null` on the link view | None |
| Link routes | Under `/v1/shared/groups/{token}`: `GET` (the group), `POST /categories`, the six expense routes and the six repayment routes of 3a, `GET /balances`, `GET /rates` (same rules as `/v1/rates`, so the dialogs can suggest the official rate). Nothing else: no group edit or delete, no members, no share management, and every other API keeps its own policy | None (anti-goal "Leaked groups") |
| Access policy | `AuthExtensions.GroupLinkPolicy` ("GroupLink") with a `GroupLinkRequirement`: met when the route `token` has the token shape and a group has that `ShareToken`; the caller's identity is ignored (anonymous or signed in, never the owner's powers). A failed requirement answers 404 "Not Found" (not 401 or 403) through a decorating `IAuthorizationMiddlewareResultHandler`, so unknown, malformed and turned-off tokens look like a missing page. Handlers resolve the group again by token under their row lock, so a link turned off in between answers 404 | None |
| Rate limiting | `RateLimitingExtensions.GroupLinkPolicy` ("groupLink"): fixed one-minute window per client IP, `RateLimiting:GroupLinkPermitLimit` (default 120, validated ≥ 1), on every link route; 429 with `Retry-After`. `app.UseRateLimiter()` moves before `app.UseAuthentication()` so floods of guessed tokens are throttled before any token lookup. Auth and session policies keep their limits; `DELETE /v1/me` now also counts attempts that fail authentication or XSRF | A user who fails authentication on `DELETE /v1/me` uses up the auth bucket sooner |
| XSRF | Link routes keep the XSRF middleware unchanged: an anonymous request has no auth cookie and needs no XSRF token; a signed-in visitor's cookie-authenticated write needs `X-XSRF-TOKEN` like every other write | None |
| Who am I (backend) | Link writes (categories and every entry write) need the header `X-Group-Member: <member id>` naming a current member of that group; missing, not a GUID, or not a member → 403 `group.memberRequired`. Reads ignore it. The member is the actor of the history event (`{ kind: 'member', memberId, name }`). It is a choice, not a proof of identity: anyone with the link can act as any member, as the spec intends | A link holder can act as someone else; every change shows who it claimed to be |
| Order of checks (link routes) | rate limit 429 → token 404 (policy, before the endpoint runs) → XSRF 403 (only a signed-in visitor's cookie-authenticated write) → shape 400 → group 404 (handler re-check under its lock) → member 403 (writes) → entry 404 → references 400 → 503 → converted amount 400 | None |
| Signed-in link visitors | Treated exactly like anonymous link holders on link routes (the policy ignores identity); the owner using the link acts as the member they picked | None for part 3; part 4 adds the "link my account" offer |
| Link page | `/g/[token]` (localised prefixes work: `/pl/g/...`). Open to everybody: the auth middleware neither redirects anonymous visitors to the login nor signed-in ones away. Its own layout `link`: logo, the language and theme preferences of the login layout (`AuthPreferences`), and "You are {name} · Change" once a member is chosen; no sidebar, no bottom bar, no call to a user endpoint besides the session probe | None |
| Who am I (frontend) | First visit: "Who are you?" with one 44px button per member (full width on phones). The choice is kept in `localStorage['mita.groupMember.<token>']` (every access in try/catch); "Change" reopens the choice. A remembered member that is no longer in the group, or a 403 `group.memberRequired` on a write, clears it and reopens the choice with "Choose who you are again." | Clearing site data asks again |
| Link view content | The group page components through the link scope: summary, balances (with "Show in"), expenses, repayments, history, restore, "Add expense", "Record repayment", "New category". Owner actions (Members, Edit group, Share) are hidden (`canManage`). Pinned currencies on the link page are the group's main currency only (no user settings); the dialogs' rate suggestion uses `/v1/shared/groups/{token}/rates` | None |
| Dead link | A 404 on the link's group shows "This link does not work. Ask the group's owner for a new one." with no app navigation | None |
| Share dialog | Owner's group page header action "Share" (desktop button, mobile icon button): link off → text "Anyone with the link can see the group and add expenses and repayments without an account." and "Create link"; link on → the URL in a read-only field labelled "Link", "Copy link" (clipboard, toast "Link copied"), "New link" (confirm "Make a new link?" / "The current link stops working."), "Turn off link" (confirm "Turn off the link?" / "People with the link can no longer open the group."); toasts "Link created", "New link created", "Link turned off" | None |
| Link URL | `window.location.origin` + the owner's current locale path of `/g/{token}` | Friends see the owner's language first and can switch on the page |
| Mock data | Albania 2026's link is on with the token `albania-2026-demo-link-0123456789abcdefghij` (43 characters) so the E2E can open it in a fresh browser context; `MockState.version` becomes 5. The mock answers link routes without a session, checks `X-Group-Member` like the backend, and does not rate limit | Developer mock data resets once |
| Real smoke | `gr-real-smoke.sh` unchanged; the real groups spec gains a second test with an anonymous browser context | None |

## Global Constraints

- Each repo's project guide and `docs/STACK.md` are binding. No new packages. MediatR stays 12.5.0.
- Backend: warnings are errors; `Api → Infrastructure → Domain`; a slice never references another slice's internals; records, `sealed`, file-scoped namespaces, no comments except for a genuinely controversial decision; `CancellationToken` everywhere; first failing rule per field; never edit an applied migration; tests `Should_X_When_Y`; tests never call the real NBP API. Link routes never use `ICurrentUser` and no group command implements `IInvalidatesCache` or `ICachedQuery` with user scope.
- Frontend: Biome clean, `bun run typecheck` 0 `error TS`, `bun test tests/unit` green, no `any`, every string in `en`, `pl`, `ru`, `uk`, inputs labelled, keyboard reachable, touch targets ≥ 44px; component and composable names unique across features; no unused code.
- Contract, byte for byte between backend and mocks: the "Share link" part of the "Expense groups" section written in Task 3: routes, `shareToken`, the header name `X-Group-Member`, 403 `group.memberRequired`, 404 for any bad token, 429 on link routes, the order of checks.
- Commits: Conventional Commits, English; no tool attribution, no `Co-Authored-By`, no "Generated with" lines in commits, pull requests, code or committed files.

## Review Focus

- A link token must open exactly one group: with group A's token, an entry id, member id or category of group B answers 404 or `unknownMember`/`unknownCategory`, and no link route exposes members management, the group's edit or delete, the share token or another API. Pinned in Task 2 (`Should_ConfineLink_ToItsGroup`) and Task 3 (`Should_RefuseOtherGroupsEntries_OnLink`).
- Turning the link off or making a new one must lock out the old token immediately (handlers also re-check the token under their lock). Pinned in Task 1 (`Should_ReplaceToken_When_LinkRegenerated`), Task 2 (`Should_Return404_When_TokenUnknownMalformedOrOff`) and Task 3 (`Should_Return404_When_LinkTurnedOffBeforeWrite`).
- Anonymous traffic must be throttled even when every token is wrong: unknown tokens count against the `groupLink` bucket and answer 429 once it is used up. Pinned in Task 2 (`Should_RateLimitLinkRoutes_IncludingUnknownTokens`).
- A link holder's write without a valid "who am I" must be refused and write nothing, and a valid one must appear as that member in the history. Pinned in Task 3 (`Should_RequireMember_ForLinkWrites`, `Should_RecordMemberAsActor_When_LinkHolderWrites`) and Task 6 (E2E).
- A visitor must never be bounced to the login page, and a signed-in visitor's write through the link must still pass XSRF. Pinned in Task 3 (`Should_CheckXsrf_When_SignedInVisitorWritesThroughLink`) and Task 6 (E2E in a fresh context and in the signed-in context).

---

## File map

```
Backend (dock.mita.backend, branch feature/groups-share-link)
src/Mita.Domain/Groups/{Group,ShareTokens}.cs
src/Mita.Infrastructure/Persistence/{Configurations/GroupConfiguration.cs, Migrations/<timestamp>_GroupShareLinks.cs}
src/Mita.Api/Authentication/{GroupLinkRequirement,GroupLinkAuthorizationHandler,GroupLinkAuthorizationResultHandler}.cs
src/Mita.Api/Extensions/{AuthExtensions,RateLimitingExtensions,ApiVersioningExtensions}.cs, src/Mita.Api/Configuration/RateLimitingOptions.cs, src/Mita.Api/Program.cs
src/Mita.Api/Common/Groups/{GroupScope,GroupAccess,GroupErrors,GroupDto,GroupQueries}.cs
src/Mita.Api/Features/Groups/{GroupsEndpoints.cs, SharedGroupsEndpoints.cs, ShareGroup/*, UnshareGroup/*, and the endpoint files of GetGroup, GetGroupBalances, ListExpenses, ExpenseHistory, ListGroupRepayments, GroupRepaymentHistory, AddCategory, CreateExpense, UpdateExpense, DeleteExpense, RestoreExpense, CreateGroupRepayment, UpdateGroupRepayment, DeleteGroupRepayment, RestoreGroupRepayment and their handlers' open calls}
src/Mita.Api/Features/Rates/GetRates/GetRatesEndpoint.cs
docs/api-contract.md
tests/Mita.Api.UnitTests/{Groups/ShareTokensTests.cs, Configuration/OptionsValidatorTests.cs}
tests/Mita.Api.IntegrationTests/{Infrastructure/{MitaApiFactory,TestData,LinkClient}.cs, Persistence/MigrationTests.cs, Features/Groups/{GroupShareTests,GroupLinkAccessTests,GroupLinkWritesTests}.cs, OpenApiTests.cs}

Frontend (dock.mita.frontend, branch feature/groups-share-link)
app/shared/api/{contract,client,transport}.ts, app/shared/api/transports/http.ts, app/shared/auth/publicRoutes.ts, app/middleware/auth.global.ts, app/shared/rates/{ratesApi,useRates}.ts
app/features/groups/{utils/scope.ts, api/{groupsApi,entriesApi}.ts, composables/{useGroupMutations,useLinkMember,useGroupCurrencies}.ts, components/{ShareDialog,WhoAmI,LinkBar}.vue, components/{BalancesCard,ExpenseDialog,GroupRepaymentDialog}.vue}
app/layouts/link.vue, app/pages/g/[token].vue, app/pages/tools/groups/[id].vue
mocks/{api,router,db,groups}.ts, mocks/handlers/{groups,groupExpenses,groupRepayments,rates}.ts, mocks/seed/groups.ts
i18n/locales/{en,pl,ru,uk}.json
tests/unit/{shared/{api,httpTransport,publicRoutes}.test.ts, mocks/{groupLink,db}.test.ts, features/groups/scope.test.ts}
tests/e2e/{groupShare,groupLink}.spec.ts, tests/e2e-real/groups.real.spec.ts
project guide in the repository root ("Real-backend suite")
```

---

### Task 1: Share token and the owner's share routes

Backend repo, branch `feature/groups-share-link`.

**Files:** `Mita.Domain/Groups/Group.cs`, new `Mita.Domain/Groups/ShareTokens.cs`, `Persistence/Configurations/GroupConfiguration.cs`, migration `GroupShareLinks`, `Common/Groups/{GroupDto,GroupQueries}.cs`, new `Features/Groups/{ShareGroup,UnshareGroup}/*`, `Features/Groups/GroupsEndpoints.cs`, `docs/api-contract.md` (the two rows and `shareToken` only); tests new `tests/Mita.Api.UnitTests/Groups/ShareTokensTests.cs`, new `tests/Mita.Api.IntegrationTests/Features/Groups/GroupShareTests.cs`, `Persistence/MigrationTests.cs`, `Features/Groups/GroupsTests.cs`, `OpenApiTests.cs`.

**Interfaces:**
- Produces:
  - `ShareTokens`: `const int Length = 43`; `static string New()` (32 bytes of `RandomNumberGenerator`, base64url without padding); `static bool IsWellFormed(string? value)` (exactly 43 characters of `[A-Za-z0-9_-]`).
  - `Group.ShareToken` (`string?`, private setter), `void TurnOnShareLink(string token)` (throws `ArgumentException` for a malformed token), `void TurnOffShareLink()`.
  - Configuration: `ShareToken` max length 43, unique index (filtered on not null).
  - `GroupDto` gains `string? ShareToken` as its last property; `GroupQueries.LoadAsync(db, groupId, includeShareToken: bool, ct)` (true on owner routes, false on link routes).
  - `ShareGroupCommand(Guid Id) : IRequest<Result<GroupDto>>`: transaction, `OpenOwnedAsync(Update)` (404), `TurnOnShareLink(ShareTokens.New())`, save, `LoadAsync(…, true)`; `POST /v1/groups/{id:guid}/share` → 200.
  - `UnshareGroupCommand(Guid Id) : IRequest<Result>`: `OpenOwnedAsync(Update)` (404), `TurnOffShareLink`; `DELETE /v1/groups/{id:guid}/share` → 204.
- [ ] **Step 1: Failing tests**
  - `ShareTokensTests`: `New()` is 43 characters of the alphabet, two calls differ, `IsWellFormed` true for it; false for `null`, 42 or 44 characters, `+`, `/`, `=` and a space.
  - `GroupShareTests`: `Should_TurnOnLink`: `POST …/share` → 200 with `shareToken` well formed; `GET /v1/groups/{id}` shows the same token. `Should_ReplaceToken_When_LinkRegenerated`: a second `POST` → a different token, the first no longer in the database. `Should_TurnOffLink`: `DELETE` → 204, `shareToken null`; `DELETE` again → 204. `Should_Return404_When_GroupUnknownOrForeign`: both routes with a random id and another user's group → 404, the foreign token unchanged. `Should_Return401_When_NotAuthenticated`.
  - `GroupsTests`: the group body properties end with `shareToken` (`null` for a new group).
  - `MigrationTests`: `groups` has `ShareToken` and its unique index.
  - `OpenApiTests.Should_DescribeGroups`: `/v1/groups/{id}/share` (post, delete).
- [ ] **Step 2: Run, see them fail, implement, add the migration, run, see them pass,** then the full backend suite.
- [ ] **Step 3: Commit** — `feat: share link token for expense groups`

### Task 2: Read access through the link, its policy and rate limit

**Files:** new `Authentication/{GroupLinkRequirement,GroupLinkAuthorizationHandler,GroupLinkAuthorizationResultHandler}.cs`, `Extensions/{AuthExtensions,RateLimitingExtensions,ApiVersioningExtensions}.cs`, `Configuration/RateLimitingOptions.cs`, `Program.cs`, `Common/Groups/{GroupScope,GroupAccess}.cs`, new `Features/Groups/SharedGroupsEndpoints.cs`, the endpoint files of `GetGroup`, `GetGroupBalances`, `ListExpenses`, `ExpenseHistory`, `ListGroupRepayments`, `GroupRepaymentHistory`, `Features/Rates/GetRates/GetRatesEndpoint.cs`, every 3a endpoint that builds `new GroupScope(id)`; tests `tests/Mita.Api.UnitTests/Configuration/OptionsValidatorTests.cs`, `Infrastructure/MitaApiFactory.cs`, new `Infrastructure/LinkClient.cs`, new `Features/Groups/GroupLinkAccessTests.cs`, `OpenApiTests.cs`.

**Interfaces:**
- Consumes: Task 1 (`ShareTokens.IsWellFormed`, `Group.ShareToken`, `LoadAsync(…, includeShareToken)`).
- Produces:
  - `abstract record GroupScope` with `sealed record OwnerGroupScope(Guid GroupId) : GroupScope` and `sealed record LinkGroupScope(string Token, string? MemberHeader) : GroupScope`; `static GroupScope Owner(Guid groupId)`, `static GroupScope Link(string token, string? memberHeader)`. Every `new GroupScope(id)` becomes `GroupScope.Owner(id)`.
  - `GroupAccess.OpenAsync(GroupScope scope, GroupLock mode, CancellationToken ct)`: owner → `OpenOwnedAsync`; link → the group whose `ShareToken` equals the token (row-locked as asked), `null` when none; the context's `Actor` for a link read is irrelevant (Task 3 adds the member).
  - `ApiVersioningExtensions.MapVersioned(this IEndpointRouteBuilder app)` — the versioned `/v{version}` group without a policy; `MapV1()` = `MapVersioned().RequireAuthorization(AuthExtensions.UserPolicy)` (unchanged behaviour).
  - `AuthExtensions.GroupLinkPolicy = "GroupLink"`: policy with `GroupLinkRequirement` only; `GroupLinkAuthorizationHandler(AppDbContext db)` (scoped): the resource is the `HttpContext`, the route value `token` must pass `ShareTokens.IsWellFormed` and match a group (`AsNoTracking`, `Any`); `GroupLinkAuthorizationResultHandler` decorates the default `AuthorizationMiddlewareResultHandler`: a failure with a failed `GroupLinkRequirement` writes 404 "Not Found" problem details through `ProblemDetailsWriter`, everything else is delegated.
  - `RateLimitingOptions.GroupLinkPermitLimit` (default 120, validated ≥ 1); `RateLimitingExtensions.GroupLinkPolicy = "groupLink"` (per client IP, fixed window of one minute, as the other two); `Program.cs`: `app.UseRateLimiter()` right after `app.UseCors()`.
  - `SharedGroupsEndpoints.MapSharedGroupsEndpoints(this IEndpointRouteBuilder app)`: `MapVersioned().MapGroup("/shared/groups/{token}").WithTags("Shared groups").RequireAuthorization(AuthExtensions.GroupLinkPolicy).RequireRateLimiting(RateLimitingExtensions.GroupLinkPolicy)`; this task maps `GET ""` (group, `includeShareToken: false`), `GET /balances`, `GET /expenses`, `GET /expenses/{expenseId:guid}/history`, `GET /repayments`, `GET /repayments/{repaymentId:guid}/history`, `GET /rates`. Each slice's endpoint file gains a `MapShared…` method beside its owner one (same query, `GroupScope.Link(token, header)`), never adding the user policy. `Program.cs` maps it after the groups endpoints.
  - `LinkClient` (tests): an anonymous `HttpClient` helper with `GetAsync(token, path)` and `SendAsync(method, token, path, body, member)` that sets `X-Group-Member`.
- [ ] **Step 1: Failing tests** (`GroupLinkAccessTests`; a group with the 3a TestData expenses, repayment and a deleted expense, link on)
  - `Should_ReadGroupAnonymously_ThroughLink`: `GET /v1/shared/groups/{token}` without credentials → 200, the owner's body except `shareToken null`; `/balances`, `/expenses`, `/expenses?deleted=true`, `/repayments`, both histories and `/rates?to=EUR` → 200 with the owner routes' bodies.
  - `Should_Return404_When_TokenUnknownMalformedOrOff`: a well-formed unknown token, `abc`, a 44-character token, and the token after `DELETE …/share` → 404 "Not Found" problem details (not 401) on the group route and on `/expenses`.
  - `Should_ConfineLink_ToItsGroup`: with group A's token, history of an expense of group B (same owner) → 404; `PUT` and `DELETE /v1/shared/groups/{token}` → 405 (only `GET` is mapped there); `POST …/{token}/members`, `PUT …/{token}/members/{memberId}` and `POST`/`DELETE …/{token}/share` → 404 (no such routes); `GET /v1/groups/{idA}`, `/v1/accounts`, `/v1/me` anonymously → 401.
  - `Should_IgnoreIdentity_OnLink`: another signed-in user reads A's link → 200, and still 404 on `/v1/groups/{idA}`.
  - `Should_RateLimitLinkRoutes_IncludingUnknownTokens`: factory with `RateLimiting:GroupLinkPermitLimit` = 2 → two requests with an unknown well-formed token (404, 404), the third with the valid token → 429 with `Retry-After`; `GET /v1/groups` of a signed-in user in the same factory is not limited by it.
  - `OptionsValidatorTests`: `GroupLinkPermitLimit` 0 invalid, 1 valid, default 120.
  - `OpenApiTests.Should_DescribeSharedGroups`: the seven paths under `/v1/shared/groups/{token}` tagged `Shared groups`.
  - `MitaApiFactory` sets `RateLimiting:GroupLinkPermitLimit` to 1000 like the other limits.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite (the existing auth rate-limit and forwarded-header tests stay green with the moved limiter).
- [ ] **Step 3: Commit** — `feat: read expense groups through their share link`

### Task 3: Writes through the link, "who am I" and the contract

**Files:** `Common/Groups/GroupAccess.cs`, new `Common/Groups/GroupErrors.cs`, `Features/Groups/SharedGroupsEndpoints.cs`, the endpoint files and handlers of `AddCategory`, `CreateExpense`, `UpdateExpense`, `DeleteExpense`, `RestoreExpense`, `CreateGroupRepayment`, `UpdateGroupRepayment`, `DeleteGroupRepayment`, `RestoreGroupRepayment`, `docs/api-contract.md`; tests new `tests/Mita.Api.IntegrationTests/Features/Groups/GroupLinkWritesTests.cs`, `OpenApiTests.cs`.

**Interfaces:**
- Consumes: Task 2.
- Produces:
  - `GroupErrors.MemberRequired = Error.Forbidden("group.memberRequired")`.
  - `GroupAccess.OpenForWriteAsync(GroupScope scope, GroupLock mode, CancellationToken ct) : Task<Result<GroupContext>>`: owner → as `OpenAsync` with the owner actor (404 when null); link → the group by token (404), then the header must parse as a GUID of a member of that group → actor `GroupActor.Member(id)`, else `GroupErrors.MemberRequired`. The nine write handlers call it instead of `OpenAsync` (same lock as today); reads keep `OpenAsync`.
  - Shared write routes: `POST /categories`, `POST /expenses`, `PUT`/`DELETE /expenses/{expenseId:guid}`, `POST /expenses/{expenseId:guid}/restore`, the same four for `/repayments`; each binds `[FromHeader(Name = "X-Group-Member")] string? member`.
  - Contract: endpoint rows for the two owner routes and the 16 link routes (link errors: 400, 403 `group.memberRequired` on writes, 403 `auth.xsrf` for a signed-in visitor's write without the header, 404, 429, 503 where the owner route has it); `Group.shareToken`; a "Share link" subsection in "Expense groups" stating every Decisions row of this plan that the API shows (token, owner routes, link routes, policy and 404, rate limit, XSRF, header and 403, actors, order of checks, identity ignored); the 403 and 429 rows of the error tables and "Rate limiting and lockout" gain the link routes and `groupLink`.
- [ ] **Step 1: Failing tests** (`GroupLinkWritesTests`)
  - `Should_RequireMember_ForLinkWrites`: `POST /expenses` through the link without the header, with `x`, with a random GUID, with a member of another group → 403 `group.memberRequired`, nothing written; an invalid body without the header → 400 (shape first); a valid body with a member → 201.
  - `Should_RecordMemberAsActor_When_LinkHolderWrites`: as Olya → `createdBy { kind: "member", memberId: olya, name: "Olya" }`; the owner edits it → `updatedBy` the owner; Olya deletes and restores it → history actors `[member Olya, member Olya, owner, member Olya]` (newest first: restored, deleted, updated, created); Olya becomes `inUse true`.
  - `Should_WriteRepaymentsAndCategories_ThroughLink`: a repayment Max → Anna as Max, edit, delete, restore → 200/201/204/200; `POST /categories` "Ferry" as Olya → 201.
  - `Should_RefuseOtherGroupsEntries_OnLink`: with group A's token, `PUT`/`DELETE`/restore of an expense and a repayment of group B → 404; a payer or category of group B → 400 `unknownMember`/`unknownCategory`.
  - `Should_CheckXsrf_When_SignedInVisitorWritesThroughLink`: a signed-in user's cookie-authenticated `POST /expenses` through the link without `X-XSRF-TOKEN` → 403 `auth.xsrf`; with it → 201; an anonymous one needs none.
  - `Should_Return404_When_LinkTurnedOffBeforeWrite`: token off, then a valid write → 404.
  - `OpenApiTests.Should_DescribeSharedGroups` (extended): the nine write operations.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite.
- [ ] **Step 3: Commit** — `feat: write to expense groups through their share link`
- [ ] **Step 4: Contract** as above; `be-run.sh 'dotnet build -warnaserror && dotnet test'` green. **Commit** — `docs: api contract for the group share link`

### Task 4: Frontend contract, request headers and the mock link routes

Frontend repo, branch `feature/groups-share-link` from `origin/develop`.

**Files:** `app/shared/api/{contract,transport,client}.ts`, `app/shared/api/transports/http.ts`, `mocks/{api,router,db,groups}.ts`, `mocks/handlers/{groups,groupExpenses,groupRepayments,rates}.ts`, `mocks/seed/groups.ts`; tests `tests/unit/shared/{api,httpTransport}.test.ts`, new `tests/unit/mocks/groupLink.test.ts`, `tests/unit/mocks/db.test.ts`.

**Interfaces:**
- Consumes: the Task 3 contract.
- Produces:
  - `contract.ts`: `GroupDto.shareToken: string | null`; `GROUP_MEMBER_HEADER = 'X-Group-Member'`; `SHARE_TOKEN_LENGTH = 43`.
  - `ApiRequest.headers?: Record<string, string>`; `ApiClient` methods take an optional last argument `options?: { headers?: Record<string, string> }`; the HTTP transport merges those headers with its own (its own `Accept-Language` and `X-XSRF-TOKEN` win on a clash); the mock transport hands them to the handler.
  - `MockContext.headers: Record<string, string>` (lower-cased names); `createMockApi` treats `/v1/shared/` paths as open (no session needed).
  - Mock: `POST`/`DELETE /v1/groups/:id/share` (owner); every link route of the contract under `/v1/shared/groups/:token` reusing the owner handlers' logic through a resolved group; bad or turned-off token → 404; writes need a member header → 403 `{ status: 403, title: 'Forbidden', code: 'group.memberRequired' }`; the actor of link writes is the member; `GET /v1/shared/groups/:token/rates` answers like `/v1/rates`; token generation through `crypto.getRandomValues` (43 base64url characters).
  - Seed: `grp_albania.shareToken = 'albania-2026-demo-link-0123456789abcdefghij'`; `GroupRow.shareToken: string | null`; `MockState.version` 5 (`isState` checks 5).
- [ ] **Step 1: Failing unit tests**
  - `api.test.ts` and `httpTransport.test.ts`: headers given to `post` reach the fetcher; the transport's `X-XSRF-TOKEN` and `Accept-Language` are kept.
  - `groupLink.test.ts`: the `GroupLinkAccessTests` and `GroupLinkWritesTests` rows that the mock can express (anonymous reads equal owner reads with `shareToken null`; unknown or turned-off token 404; regenerate replaces the token; missing, malformed, foreign member → 403 `group.memberRequired`; shape 400 before 403; member actor in history; the owner's edit is the owner actor; other group's entries 404).
  - `db.test.ts`: a stored state with `version: 4` is reseeded.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then lint, typecheck and the mock E2E suite.
- [ ] **Step 3: Commit** — `feat: group share link in the mock backend`

### Task 5: Share dialog for the owner

**Files:** `app/features/groups/{api/groupsApi.ts, composables/useGroupMutations.ts, components/ShareDialog.vue}`, `app/pages/tools/groups/[id].vue`, `i18n/locales/*.json`; tests new `tests/e2e/groupShare.spec.ts`.

**Interfaces:**
- Consumes: Task 4.
- Produces:
  - `groupsApi.share(id) → GroupDto`, `groupsApi.unshare(id)`; `useGroupMutations` gains `share` and `unshare` (invalidating `'groups'`).
  - `ShareDialog` (`group: GroupDto`, `v-model:visible`): Decisions "Share dialog" and "Link URL" (`useLocalePath`), focus moves to the field after creating or replacing a link and to "Create link" after turning it off.
  - Page: header action "Share" (desktop button, mobile icon button, owner only).
- [ ] **Step 1: Failing E2E** (`groupShare.spec.ts`, both projects): "shows the seeded link" (Albania: Share → the field ends with `/g/albania-2026-demo-link-0123456789abcdefghij`); "makes a new link" (New link → confirm → the field holds another 43-character token, toast "New link created"); "turns the link off and on" (Turn off link → confirm → "Create link" visible, toast "Link turned off"; Create link → a field with a token, toast "Link created"); "copies the link" (grant `clipboard-read`/`clipboard-write` where the browser supports it, otherwise only the toast "Link copied"); `/pl/tools/groups/grp_albania` → the copied URL contains `/pl/g/`.
- [ ] **Step 2: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite; all pass.
- [ ] **Step 3: Commit** — `feat: share dialog for expense groups`

### Task 6: The link page with "who am I"

**Files:** `app/features/groups/utils/scope.ts`, `app/features/groups/api/{groupsApi,entriesApi}.ts`, new `app/features/groups/{composables/useLinkMember.ts, composables/useGroupCurrencies.ts, components/WhoAmI.vue, components/LinkBar.vue}`, `components/{BalancesCard,ExpenseDialog,GroupRepaymentDialog}.vue`, `app/shared/rates/{ratesApi,useRates}.ts`, `app/shared/auth/publicRoutes.ts`, `app/middleware/auth.global.ts`, new `app/layouts/link.vue`, new `app/pages/g/[token].vue`, `i18n/locales/*.json`; tests `tests/unit/shared/publicRoutes.test.ts`, `tests/unit/features/groups/scope.test.ts`, new `tests/e2e/groupLink.spec.ts`.

**Interfaces:**
- Consumes: Tasks 4-5.
- Produces:
  - `scope.ts`: `GroupScope = { kind: 'owner'; groupId: string } | { kind: 'link'; token: string; memberId: string | null }`; `linkScope(token, memberId)`; `scopePath` → `/v1/shared/groups/<token>` for a link; `scopeKey` → `['link', token]` (the member is not part of the key); `canManage` false for a link; `ratesPath(scope)` → `/v1/rates` or `/v1/shared/groups/<token>/rates`; `memberHeaders(scope)` → `{ 'X-Group-Member': memberId }` for a link with a member, else `{}`.
  - `entriesApi` and `groupsApi.addCategory` send `memberHeaders(scope)` on writes.
  - `ratesApi.list(query, path = '/v1/rates')`; `useRates(to, { date, enabled, path })`.
  - `useGroupCurrencies(group)` → pinned currencies: owner `[group.currency, baseCurrency]` (user settings), link `[group.currency]` without reading user settings; used by `BalancesCard`, `ExpenseDialog`, `GroupRepaymentDialog`, which also pass `ratesPath(scope)` to `useRates`.
  - `useLinkMember(token)` → `{ memberId: Ref<string | null>; choose(id: string): void; forget(): void }` on `localStorage['mita.groupMember.<token>']`, read on mount, every access in try/catch.
  - `publicRoutes.ts`: `isOpenRoute(routeBaseName)` true for `g-token`; the middleware never redirects an open route (anonymous or signed in); `isPublicRoute` unchanged.
  - Layout `link`: logo, `AuthPreferences`, the slot; `LinkBar` ("You are {name}" and "Change", 44px); `WhoAmI` (`members`, emits `choose`): heading "Who are you?", hint "Pick your name. This browser remembers it.", one button per member, the message "Choose who you are again." when reopened after a refusal.
  - Page `/g/[token]`: `definePageMeta({ layout: 'link' })`; `provideGroupScope(linkScope(token, memberId))`; `useGroup` → dead link state (Decisions "Dead link"); no member chosen, or the remembered one missing from `group.members` → `WhoAmI`; otherwise the group components as on the owner page without owner actions; a 403 `group.memberRequired` from any write → `forget()` and `WhoAmI` with the message; `useHead` title = the group name.
- [ ] **Step 1: Failing unit tests**: `scope.test.ts` (paths, keys, headers, `canManage`, `ratesPath` for both kinds); `publicRoutes.test.ts` (`g-token` open, `login` not open).
- [ ] **Step 2: Failing E2E** (`groupLink.spec.ts`; desktop and mobile; anonymous tests use `test.use({ storageState: { cookies: [], origins: [] } })`):
  - "asks who you are and remembers it": `/g/albania-2026-demo-link-0123456789abcdefghij` → "Who are you?" with Anna, Olya, Max, Kasia → Olya → heading "Albania 2026", "You are Olya", balances "Anna +€42.32"; reload → still Olya, no question; Change → "Who are you?".
  - "adds an expense as the chosen member": as Olya, Add expense, Paid by Olya, 8, "Ice cream" → the row shows "Added by Olya"; History of Ice cream → "Added by Olya".
  - "hides what only the owner can do": no "Members", "Edit group" or "Share"; no link to the app's main navigation.
  - "asks again when the member is gone": localStorage holds a member id that is not in the group → "Who are you?" with "Choose who you are again."
  - "shows a dead link": `/g/unknown-token-unknown-token-unknown-token-x` → "This link does not work. Ask the group's owner for a new one."
  - signed-in context (default storage state): Anna opens the link → "Who are you?" → Anna → adds "Coffee" 3 EUR → "Added by Anna" (member), not "Anna Nowak"; `/tools/groups/grp_albania` lists Coffee "Added by Anna".
  - `/pl/g/<token>` shows "Kim jesteś?".
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite; all pass.
- [ ] **Step 4: Commit** — `feat: open expense groups through their share link`

### Task 7: Real-backend link flow and docs

Frontend repo, same branch; the backend checkout on `feature/groups-share-link` (Tasks 1-3).

**Files:** `tests/e2e-real/groups.real.spec.ts`; the project guide ("Real-backend suite": the `groups` line mentions the link; the auth-call count per run rises by two).
- [ ] **Step 1: Add a second test to `groups.real.spec.ts`**: register a unique user; New group "Porto", EUR, members the user, "Olya", "Max"; Share → Create link → read the URL from the field; a new browser context without storage opens it → "Who are you?" → Olya → Add expense "Taxi" 30 € paid by Olya, equally → balances Olya +€20.00, the user -€10.00, Max -€10.00 and the row "Added by Olya"; the owner's page (reload) lists Taxi "Added by Olya"; the owner turns the link off → the anonymous page (reload) shows "This link does not work."; close the anonymous context; delete the group; delete the user.
- [ ] **Step 2: Run** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/scripts/gr-real-smoke.sh`; every real spec passes, the X-Forwarded-For check passes, both smoke projects are torn down, `pgdb`, `dockmitabackend` and `dock-mita-frontend-web-1` untouched.
- [ ] **Step 3: Run** `bun run lint && bun run typecheck && bun test tests/unit`; all green.
- [ ] **Step 4: Commit** — `test: real-backend share link flow for expense groups`

### Task 8: Pull requests

- [ ] Backend: rebase `feature/groups-share-link` on `origin/develop`, fold fix-ups into their task commits (tree unchanged), `dotnet build -warnaserror` and `dotnet test --logger junit`, push with `--force-with-lease`, open a PR to `develop` with a plain description, wait for `check-source-branch`, `gh pr merge --rebase`.
- [ ] Frontend: after the backend PR is merged, the same flow with lint, typecheck, unit, the mock E2E suite and the real smoke green.
- [ ] Both checkouts back on `develop`, pulled; local `feature/groups-share-link` branches deleted.

## Deferred (not in this plan)

Part 4: offering a signed-in visitor to link the chosen member to their account, email invites, personal accounting. Also: an expiry date on links, per-link permissions (read-only links), showing on the owner's page who used the link last, and the 3a deferred list.
