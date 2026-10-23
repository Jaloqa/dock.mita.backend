# Group Accounting 4a: Members Linked to Accounts Implementation Plan

**Goal:** A member of an expense group can be linked to a dock.mita account: the owner marks one member as themselves, invites other dock.mita users by email (the invitation appears in the invitee's app, where they accept or decline), and a signed-in visitor of the share link can link the member they picked to their account. A linked user sees the group in their own app, reads it and writes entries as their member, and can leave it. Personal starter categories get a standard key (the input of category suggestions in 4b). The Debts page lists the user's balance in every group they are linked to, and the debt summary counts it.

**Scope:** Delivery item 4 of the spec is split into two plans. This plan (4a) builds linking, invites, linked members' access, the standard category keys and group balances on the Debts page. Plan 4b (`2026-10-23-group-accounting-4b.md`, written after this one is merged) adds the "To confirm" list, confirmation into personal records (transactions and account legs), settlements of specific shares and repayments that cover shares. Nothing in this plan touches a linked user's accounts, budgets or reports.

**Architecture:**
- **Domain:** `GroupMember` gains `UserId` (the linked account), `InvitedEmail` and `InvitedAt` (one pending invitation per member). `Category` gains `StandardKey`.
- **Persistence:** migrations `CategoryStandardKeys` (column, index, keys derived from names in the four locales) and `GroupMemberLinks` (columns, FK to users `SET NULL`, unique link per group).
- **API:** `GroupScope.Owner`/`OwnerGroupScope` becomes `GroupScope.User`/`UserGroupScope`: the `/v1/groups/{id}` routes open a group for its owner or for a linked user, and `GroupContext` carries the caller's `Role` and `MyMemberId`. Owner-only routes keep `OpenOwnedAsync`. New owner routes link the owner and invite members; new invitee routes list, accept and decline invitations; a linked user leaves a group; a signed-in link visitor links their picked member. `Common/Groups/GroupMemberFigures` computes member balances for the balances route, the Debts page lines (`GET /v1/debts/groups`) and the debt summary.
- **Frontend:** the group scope kind `owner` becomes `user`; management rights come from `Group.role`. The members dialog gets account controls, the Groups page an "Invitations" section and shared groups, the group page "Leave group" for linked members, the link page an offer to link the picked member, and the Debts page a "Groups" section. The mock backend implements the same routes and rules.

**Tech Stack:** Backend: .NET 10, EF Core migrations (Npgsql), MediatR 12.5.0, FluentValidation, xUnit v3, Shouldly, Testcontainers. Frontend: Nuxt 4, PrimeVue 4.5, TanStack Query, Vuelidate, bun:test, Playwright 1.64.1. No new packages in either repo.

**Spec:** `dock.mita.frontend/docs/superpowers/specs/2026-10-22-tools-design.md`: Groups "Members" (any member can be linked to an account), "Only the owner changes the group itself" (invites by email), "Invite by email", "Share link" (a signed-in visitor is offered to link the chosen member), Private debts "Group balances" (one line per group on the Debts page), calls "Standard category keys" and "Group balances on the Debts page", "Unlinking or deleting", Anti-goal "Leaked groups", Delivery item 4. Also binding: both repos' project guides and `docs/STACK.md`, backend `docs/api-contract.md`, the Decisions of `2026-10-23-groups.md` (3a) and `2026-10-23-groups-share-link.md` (3b). Code to reuse: backend `Common/Groups/*`, `Features/Groups/*`, `Common/Debts/{DebtSummaries,DebtSummaryDto}.cs`, `Features/Debts/*`, `Features/Overview/GetOverview/*`, `Common/Users/EmailAddress.cs`, `Domain/Categories/{Category,StarterCategories}.cs`; frontend `app/features/groups/*`, `app/features/debts/*`, `app/features/overview/utils/overviewView.ts`, `app/shared/auth/useSession.ts`, `app/shared/api/invalidation.ts`, `mocks/{db,groups,debts}.ts`, `mocks/handlers/{groups,debts}.ts`, `mocks/seed/groups.ts`.

## Decisions

| Topic | Decision | Cost if wrong |
|---|---|---|
| Plan split | 4a (this plan, both repos, own PRs), then 4b ("To confirm", confirmation into personal records, settlements, own PRs) | Personal accounting ships in a second pair of PRs |
| Link storage | `group_members.UserId` (nullable, FK `users` `ON DELETE SET NULL`, unique `(GroupId, UserId)` where not null). A member is linked to at most one account and an account to at most one member per group | None |
| Invitation storage | `group_members.InvitedEmail` (lower-cased, trimmed, max 254) and `InvitedAt`: one pending invitation per member, replaced by a new one. It is matched against the invitee's current email at read time; inviting never looks a user up, so it never reveals whether an email has an account, and a person can register after being invited | An invitation to an email that never registers stays until the owner cancels it |
| How the invitee learns | No email is sent (the stack has no mail delivery). The invitation appears on the invitee's Groups page (Tools → Groups) with Accept and Decline; the members dialog tells the owner "Let them know: the invitation waits for them under Tools → Groups in their dock.mita app." | The invitee notices it only when opening Groups |
| Acceptance | Explicit Accept (links the member to the invitee, clears the invitation, the group appears in their list) or Decline (clears the invitation). Invitations of a group where the user is already linked are not listed | None |
| Owner as a member | The owner links themselves with "This is me" on one member (`PUT …/members/{memberId}/link`). No automatic link at creation, so existing groups stay unchanged | The owner takes one step before 4b's accounting works for them |
| Who manages links | The owner links themselves, invites, cancels invitations and unlinks any member. A linked user leaves (`DELETE /v1/groups/{id}/membership`). A signed-in link visitor links the member they picked (`PUT /v1/shared/groups/{token}/membership`). Linking a member clears its invitation. Removing an unused member drops its link and invitation (3a removal rule unchanged). A linked user who deletes their account is unlinked by the FK | Anyone with the link and an account can claim an unlinked member; the owner sees who (`linkedName`) and can unlink |
| Linked users' access | `/v1/groups/{id}` read and entry routes open for the owner and for linked users. A linked user who is not the owner acts as their member (`GroupActor.Member`), so history shows the member's name, and `ActorUserId` stays the owner's only: `GroupActors` keeps its invariant (closes the 3a deferred item). Group update and delete, members, share link and invitation routes stay owner-only: anyone else gets 404 | None |
| Concurrency | Link, unlink, invite, accept, decline, leave and share-link linking lock the group row `FOR UPDATE` (as member changes do), so they serialize with each other and with entry writes (`FOR SHARE`) | None |
| Personal data | `Group.members[].invitedEmail` and `linkedName` (the account's user name) are filled for the owner only; `linked` is shown to everyone, link holders included; `myMemberId` is `null` on the link routes. Link routes never answer anything about the caller's account | None (anti-goal "Leaked groups") |
| Scope naming | Backend `OwnerGroupScope`/`GroupScope.Owner` → `UserGroupScope`/`GroupScope.User`; frontend scope kind `owner` → `user` (`userScope`); `canManage` takes the group and is `role === 'owner'` | None |
| Group roles on the wire | `Group.role`: `owner`, `member` (linked user) or `link` (share link); `Group.myMemberId`: the caller's linked member or `null`; `GroupSummary.role`: `owner` or `member`. `GET /v1/groups` lists owned and linked groups, newest created first | None |
| Standard category keys | `categories.StandardKey` (max 20, unique `(UserId, Kind, StandardKey)` where not null), set from the starter key at registration. The migration derives it for existing categories: `System` `Other` → `other`, `Transfer` → `transfer`; otherwise a name equal to a starter name of the same kind in en, pl, ru or uk, compared case-insensitively; one category per user, kind and key (lowest `Position`, then `CreatedAt`, then `Id`). Renaming keeps the key, deleting drops it. Not exposed by the API; the first consumer is 4b | A starter category renamed before this migration gets no key; its suggestion falls back to Other |
| Group balances on the Debts page | `GET /v1/debts/groups?page&pageSize`: one line per group where the user is linked (`groupId`, `name`, `currency`, `balance` = the member's balance over live entries in the main currency, positive = the others owe the user), settled groups included, in the order of `GET /v1/groups`. These are the group's figures, confirmed or not | None |
| Debt summary | `DebtSummary` gains `groups` (linked groups with a non-zero balance). `owedToMe` adds positive and `owedByMe` negative group balances, each group converted separately into the base currency at today's latest stored rate (503 when never stored). `Overview.debts` is the same object; the Overview debts card shows when `open + groups > 0` | The balance with debts adds group figures that 4b's confirmation has not booked yet |
| Mock data | New group "Kraków" owned by Oleh (PLN; members Oleh linked to Oleh, Anna invited as `anna@demo.mita.app`, Taras; Oleh paid 300.00 zł for all three, "Pierogi", today − 5). Anna stays unlinked in "Albania 2026", so existing figures stay. `MockState.version` becomes 6 | Developer mock data resets once |
| Real smoke | `dock.mita-work/scripts/ga-real-smoke.sh`: a copy of `gr-real-smoke.sh` with projects `dock-mita-backend-ga-smoke`, `dock-mita-frontend-ga-smoke`, image `dock-mita-frontend-ga-smoke` | None |

## Global Constraints

- Each repo's project guide and `docs/STACK.md` are binding. No new packages. MediatR stays 12.5.0.
- Backend: warnings are errors; `Api → Infrastructure → Domain`; a slice never references another slice's internals (share through `Common/Groups`); records, `sealed`, file-scoped namespaces, no comments except for a genuinely controversial decision; `CancellationToken` everywhere; first failing rule per field, all failing fields in one 400; shape (400) before lookup (404); ids in paths are GUIDs; never edit an applied migration; tests `Should_X_When_Y`; tests never call the real NBP API; tests that seed rates use `factory.WithIsolatedData()` and dates relative to today. Group commands never implement `IInvalidatesCache` and group data is never cached.
- Frontend: Biome clean, `bun run typecheck` 0 `error TS`, `bun test tests/unit` green, no `any`, every string in `en`, `pl`, `ru`, `uk` (`tests/unit/i18n/locales.test.ts` keeps key sets equal), inputs labelled, keyboard reachable, touch targets ≥ 44px, component and composable names unique across features, no unused code (API calls and mutations land in the task that first uses them).
- Contract, byte for byte between backend and mocks: the routes, types, codes and order of checks written in Task 5.
- New validation codes (all four locales, added with the mock in Task 6): `validation.memberLinked` "This member is already linked to an account.", `validation.alreadyLinked` "You are already linked to another member of this group.", `validation.inviteSelf` "This is your own email. Use “This is me” instead.", `validation.alreadyInvited` "Another member of this group is already invited with this email."
- Commits: Conventional Commits, English; no tool attribution, no `Co-Authored-By`, no "Generated with" lines in commits, pull requests, code or committed files.

## Review Focus

- A user who is neither the owner nor linked must never open a group through `/v1/groups/{id}`, and a user who left or was unlinked loses access at once; group update and delete, members, share link and invitation routes stay owner-only for linked users. Pinned in Task 2 (`Should_KeepOwnerRoutes_ForOwnerOnly`, `Should_Return404_When_UserLeftOrWasUnlinked`).
- Invited emails and linked account names must reach the owner only, never a linked member or a link holder; `myMemberId` is never filled on a link route. Pinned in Task 2 (`Should_ShowLinkDetails_ToOwnerOnly`).
- One account can never hold two members of a group and a member never two accounts, also when two requests race (two share-link links by one user, an accept against a share-link link). Pinned in Task 4 (`Should_LinkOnce_When_RequestsRace`).
- An invitation must be accepted only by the account whose current email matches it, case-insensitively, and never after it was cancelled or the member got linked. Pinned in Task 4 (`Should_AcceptInvite_OnlyForMatchingEmail`).
- The debt summary must add a linked group's balance with the right sign, converted per group, and never count a group where the user is not linked (the owner's unlinked groups included). Pinned in Task 5 (`Should_IncludeGroupBalances_InSummary`, `Should_IgnoreGroups_WhereUserIsNotLinked`).

---

## File map

```
Backend (dock.mita.backend, branch feature/group-accounting)
src/Mita.Domain/Categories/Category.cs, src/Mita.Domain/Groups/GroupMember.cs
src/Mita.Infrastructure/Persistence/{Configurations/{CategoryConfiguration,GroupMemberConfiguration}.cs, Migrations/<timestamp>_CategoryStandardKeys.cs, Migrations/<timestamp>_GroupMemberLinks.cs}
src/Mita.Api/Common/Groups/{GroupScope,GroupRole,GroupContext,GroupAccess,GroupWire,GroupDto,GroupMemberDto,GroupSummaryDto,GroupQueries,GroupLinkRules,GroupMemberFigures,GroupInviteEmails}.cs
src/Mita.Api/Common/Debts/{DebtSummaries,DebtSummaryDto}.cs
src/Mita.Api/Features/Groups/{GroupsEndpoints.cs, SharedGroupsEndpoints.cs, LinkMember/*, UnlinkMember/*, InviteMember/*, CancelInvite/*, LeaveGroup/*, LinkThroughShare/*, every endpoint file that builds GroupScope.Owner, GetGroup/*, ListGroups/*, CreateGroup/*, UpdateGroup/*, ShareGroup/*, GetGroupBalances/*}
src/Mita.Api/Features/GroupInvites/{GroupInvitesEndpoints.cs, ListGroupInvites/*, AcceptGroupInvite/*, DeclineGroupInvite/*}
src/Mita.Api/Features/Debts/{DebtsEndpoints.cs, ListGroupDebts/*}
src/Mita.Api/Program.cs, docs/api-contract.md
tests/Mita.Api.UnitTests/{Categories/StarterCategoriesTests.cs, Groups/GroupMemberTests.cs, Features/Groups/InviteMemberValidatorTests.cs}
tests/Mita.Api.IntegrationTests/{Infrastructure/TestData.cs, Persistence/MigrationTests.cs, Features/Groups/{GroupsTests,GroupMembersTests,GroupLinkAccessTests,LinkedMembersTests,MemberLinksTests,GroupInvitesTests}.cs, Features/Debts/GroupDebtsTests.cs, Features/Overview/OverviewTests.cs, Features/Users/MeTests.cs, OpenApiTests.cs}

Frontend (dock.mita.frontend, branch feature/group-accounting)
app/shared/api/{contract,invalidation}.ts
app/features/groups/{utils/scope.ts, api/groupsApi.ts, api/keys.ts, composables/{useGroupMutations,useGroupInvites,useGroupCurrencies}.ts, schemas/inviteSchema.ts, components/{MembersDialog,MemberAccountControls,GroupInvites,GroupCard,BalancesCard,LinkAccountOffer}.vue}
app/features/debts/{api/{debtsApi,keys}.ts, composables/useGroupDebts.ts, components/GroupDebtList.vue}
app/features/overview/utils/overviewView.ts
app/pages/{tools/groups/index.vue, tools/groups/[id].vue, g/[token].vue, tools/debts.vue}
mocks/{db,groups,debts}.ts, mocks/handlers/{groups,groupLinks,debts,index}.ts, mocks/seed/groups.ts
i18n/locales/{en,pl,ru,uk}.json
tests/unit/{mocks/{groupLinks,seedGroups,debts,db}.test.ts, features/groups/{scope,inviteSchema}.test.ts, features/overview/overviewView.test.ts, shared/invalidation.test.ts}
tests/e2e/{groupLinks,debts}.spec.ts, tests/e2e-real/group-links.real.spec.ts
project guide in the repository root ("Real-backend suite")
```

---

### Task 1: Standard keys on personal categories

Backend repo, branch `feature/group-accounting`.

**Files:** `Mita.Domain/Categories/Category.cs`, `Persistence/Configurations/CategoryConfiguration.cs`, migration `CategoryStandardKeys`; tests `tests/Mita.Api.UnitTests/Categories/StarterCategoriesTests.cs`, `tests/Mita.Api.IntegrationTests/Persistence/MigrationTests.cs`.

**Interfaces:**
- Produces: `Category.StandardKey` (`string?`, private setter), `const int StandardKeyMaxLength = 20`; `Category.Starter(...)` sets it to `starter.Key`, `Category.Create` leaves it `null`, `Update` never touches it. Configuration: max length 20, unique index `IX_categories_user_kind_standard_key` on `(UserId, Kind, StandardKey)` filtered `"StandardKey" IS NOT NULL`. Migration `CategoryStandardKeys` (`dotnet ef migrations add CategoryStandardKeys …`) plus, by hand after the column and before the index, one `UPDATE` that derives the keys by the Decisions rule (a `VALUES` list of kind, key and the four names of every starter category written into the migration, `lower()` on both sides, `row_number()` over `(UserId, Kind, key)` ordered by `Position`, `CreatedAt`, `Id` keeping the first). `Down` drops the index and the column.
- [ ] **Step 1: Failing tests**
  - `StarterCategoriesTests.Should_SetStandardKey_When_CategoryIsStarter`: `StarterCategories.For(user, Locale.Pl)` → keys equal `StarterCategories.All` keys in order (18, `other` twice); `Category.Create(user, "Food", Expense, Chart1).StandardKey` is null.
  - `MigrationTests.Should_DeriveStandardKeys_When_CategoriesExistedBeforeMigration`: migrate to `GroupShareLinks`; insert a user with settings (`InsertUserWithSettings`) and expense categories `Spożywcze` (pos 0), `transport` (1), `Food` (2), `Shopping` (3), `Zakupy` (7), `Inne` with `System` `Other` (8), `Перекази` with `System` `Transfer` (9), and income categories `Prezent` (10), `Transport` (11), `Другое` with `System` `Other` (12); migrate to the latest → keys `groceries`, `transport`, null, `shopping`, null, `other`, `transfer`, `gift`, null, `other`; the index exists.
- [ ] **Step 2: Run, see them fail.** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/be-run.sh 'dotnet test --filter "FullyQualifiedName~StarterCategoriesTests|FullyQualifiedName~MigrationTests"'`
- [ ] **Step 3: Implement, add the migration, run, see them pass,** then `be-run.sh 'dotnet build -warnaserror && dotnet test'`.
- [ ] **Step 4: Commit** — `feat: standard keys on starter categories`

### Task 2: Member links and linked users' access

**Files:** `Mita.Domain/Groups/GroupMember.cs`, `Persistence/Configurations/GroupMemberConfiguration.cs`, migration `GroupMemberLinks`, `Common/Groups/{GroupScope,GroupRole,GroupContext,GroupAccess,GroupWire,GroupDto,GroupMemberDto,GroupSummaryDto,GroupQueries}.cs`, every endpoint file with `GroupScope.Owner`, the handlers of `GetGroup`, `ListGroups`, `CreateGroup`, `UpdateGroup`, `ShareGroup`; tests new `tests/Mita.Api.UnitTests/Groups/GroupMemberTests.cs`, `Infrastructure/TestData.cs`, `Persistence/MigrationTests.cs`, new `Features/Groups/LinkedMembersTests.cs`, `Features/Groups/{GroupsTests,GroupMembersTests,GroupLinkAccessTests}.cs`, `Features/Users/MeTests.cs`.

**Interfaces:**
- Produces:
  - `GroupMember`: `Guid? UserId`, `string? InvitedEmail`, `DateTimeOffset? InvitedAt` (private setters), `const int EmailMaxLength = 254`, `bool IsLinked`; `void LinkTo(Guid userId)` (sets `UserId`, clears the invitation; throws `InvalidOperationException` when linked to another user, no-op for the same user); `void Unlink()`; `void Invite(string email, DateTimeOffset at)` (throws `InvalidOperationException` when linked, `ArgumentException` for an email that is blank, longer than 254 or not lower-case trimmed); `void CancelInvite()`.
  - Configuration: FK `UserId` → users `SetNull`, unique index `IX_group_members_group_user` on `(GroupId, UserId)` filtered `"UserId" IS NOT NULL`, `InvitedEmail` max 254 with index `IX_group_members_invited_email` filtered `"InvitedEmail" IS NOT NULL`. Migration `GroupMemberLinks`.
  - `TestData.LinkMemberAsync(WebApplicationFactory<Program> factory, GroupMember member, Guid userId) : Task` and `TestData.InviteMemberAsync(factory, GroupMember member, string email) : Task` (lower-cased, `InvitedAt` now).
  - `abstract record GroupScope` with `static GroupScope User(Guid groupId)` and `sealed record UserGroupScope(Guid GroupId)` (replaces `Owner`/`OwnerGroupScope` everywhere); `enum GroupRole { Owner, Member, Link }`; `GroupWire.ToWire(GroupRole)` → `owner`/`member`/`link`.
  - `sealed record GroupContext(Guid GroupId, Guid OwnerId, Currency Currency, GroupActor Actor, GroupRole Role, Guid? MyMemberId)`.
  - `GroupAccess.OpenAsync(UserGroupScope)`: the group whose `OwnerId` is the user or that has a member linked to the user (one raw SQL with `EXISTS`, `FOR SHARE OF`/`FOR UPDATE OF` the group row as asked); `Role` `Owner` when the user owns it, else `Member`; `MyMemberId` the user's linked member (`null` for an unlinked owner); `Actor` `GroupActor.Owner(userId)` for the owner, `GroupActor.Member(MyMemberId)` for a linked user. `OpenOwnedAsync` keeps the owner-only rule and fills `Role` `Owner` and `MyMemberId`. Link scope: `Role` `Link`, `MyMemberId` null.
  - `sealed record GroupMemberDto(Guid Id, string Name, bool InUse, bool Linked, string? LinkedName, string? InvitedEmail)`; `GroupDto` gains `string Role, Guid? MyMemberId` after `ShareToken`; `GroupSummaryDto` gains `string Role` last.
  - `GroupQueries.LoadAsync(this AppDbContext db, GroupContext group, CancellationToken ct)` replaces the `includeShareToken` form: `ShareToken`, `LinkedName` (the user's `Name`) and `InvitedEmail` only when `Role` is `Owner`; `Role` and `MyMemberId` from the context. `ProjectSummaries(db, userId)` adds the role. `ListGroupsHandler`: groups owned or linked.
- [ ] **Step 1: Failing tests**
  - `GroupMemberTests`: `LinkTo(u1)` clears an invitation and sets `IsLinked`; `LinkTo(u1)` again is a no-op; `LinkTo(u2)` throws; `Invite("a@b.pl", now)` on a linked member throws; `Invite("A@b.pl", …)` throws; `Unlink()` clears `UserId`; `CancelInvite()` clears email and time.
  - `MigrationTests.Should_ApplyMigrations_When_DatabaseIsEmpty` (extended): `group_members` has `UserId`, `InvitedEmail`, `InvitedAt` and both indexes.
  - `GroupsTests` and `GroupMembersTests`: group bodies end with `shareToken, role, myMemberId` (`role "owner"`, `myMemberId null`); member bodies are exactly `id, name, inUse, linked, linkedName, invitedEmail`; summaries end with `role`.
  - `LinkedMembersTests` (owner A with group "Trip" Anna, Olya, Max; user B):
    - `Should_OpenGroup_ForLinkedUser`: B linked to Olya → `GET /v1/groups/{id}` as B 200 with `role "member"`, `myMemberId` Olya, `shareToken null` although the link is on, `linked` `[false, true, false]`.
    - `Should_ShowLinkDetails_ToOwnerOnly`: A linked to Anna, B to Olya, Max invited as `max@example.com` → as A: `role "owner"`, `myMemberId` Anna, `linkedName` `[<A's name>, <B's name>, null]`, `invitedEmail` `[null, null, "max@example.com"]`; as B: every `linkedName` and `invitedEmail` null; through the link (`LinkClient`): `role "link"`, `myMemberId null`, `linked` `[true, true, false]`, every `linkedName` and `invitedEmail` null.
    - `Should_RecordLinkedMemberAsActor`: B creates an expense paid by Olya → `createdBy { kind: "member", memberId: Olya, name: "Olya" }`; B edits, deletes and restores it → history actors all member Olya; A's edit shows the owner.
    - `Should_KeepOwnerRoutes_ForOwnerOnly`: as B, `PUT` and `DELETE` the group, `POST`/`PUT`/`DELETE` members, `POST`/`DELETE …/share` → 404 and nothing changed; `POST …/categories` as B → 201.
    - `Should_ListOwnedAndLinkedGroups`: B owns "Home" (created later) and is linked in A's "Trip" → B's list `["Home" role owner, "Trip" role member]`; A's list has only "Trip".
    - `Should_Return404_When_UserLeftOrWasUnlinked`: B linked, then `TestData` unlinks → `GET` and `POST …/expenses` as B 404; a user who was never linked 404.
  - `MeTests.Should_UnlinkMember_When_LinkedUserDeleted`: B linked to Olya in A's group, B `DELETE /v1/me` → 204, Olya's `UserId` null, A's group intact.
  - `GroupLinkAccessTests.Should_ReadGroupAnonymously_ThroughLink` (adjusted): the link body equals the owner's except `shareToken null`, `role "link"`, `myMemberId null` and no owner-only member details.
- [ ] **Step 2: Run, see them fail.** `be-run.sh 'dotnet test --filter "FullyQualifiedName~Group|FullyQualifiedName~MigrationTests|FullyQualifiedName~MeTests"'`
- [ ] **Step 3: Implement, add the migration, run, see them pass,** then the full backend suite.
- [ ] **Step 4: Commit** — `feat: open expense groups to linked members`

### Task 3: The owner links and invites members

**Files:** new `Common/Groups/GroupLinkRules.cs`, new `Features/Groups/{LinkMember,UnlinkMember,InviteMember,CancelInvite}/*`, `Features/Groups/GroupsEndpoints.cs`; tests new `tests/Mita.Api.UnitTests/Features/Groups/InviteMemberValidatorTests.cs`, new `tests/Mita.Api.IntegrationTests/Features/Groups/MemberLinksTests.cs`, `OpenApiTests.cs`.

**Interfaces:**
- Consumes: Task 2 (`GroupMember` link methods, `OpenOwnedAsync`, `GroupMemberDto`).
- Produces:
  - `GroupLinkRules`: `const string MemberLinked = "validation.memberLinked"`, `AlreadyLinked = "validation.alreadyLinked"`, `InviteSelf = "validation.inviteSelf"`, `AlreadyInvited = "validation.alreadyInvited"`; `static Task<GroupMemberDto> MemberDtoAsync(this AppDbContext db, GroupContext group, Guid memberId, CancellationToken ct)` (one member as `LoadAsync` shows it).
  - `LinkMemberCommand(Guid GroupId, Guid MemberId) : IRequest<Result<GroupMemberDto>>`: transaction, `OpenOwnedAsync(Update)` (404), the member of this group (404); linked to the owner already → 200 unchanged; linked to another user → 400 `memberId: ["validation.memberLinked"]`; the owner linked to another member → 400 `memberId: ["validation.alreadyLinked"]`; `LinkTo(owner)`; `PUT /v1/groups/{id:guid}/members/{memberId:guid}/link` → 200.
  - `UnlinkMemberCommand(Guid GroupId, Guid MemberId) : IRequest<Result>`: owner, member (404); `Unlink()` (also when not linked); `DELETE …/members/{memberId:guid}/link` → 204.
  - `InviteMemberRequest(JsonElement? Email)`, `InviteMemberCommand(Guid GroupId, Guid MemberId, JsonElement? Email) : IRequest<Result<GroupMemberDto>>`; validator: `email` required → `validation.invalidEmail` (not a string, or `EmailAddress.IsValid` false after trimming); handler: transaction, owner (404), member (404), then one 400 with every failing reference: `memberId: ["validation.memberLinked"]` when linked, `email: ["validation.inviteSelf"]` when it equals the owner's email case-insensitively, `email: ["validation.alreadyInvited"]` when another member of this group holds that invitation; `Invite(trimmed lower-case, now)` (re-inviting the member replaces it); `PUT …/members/{memberId:guid}/invite` → 200.
  - `CancelInviteCommand(Guid GroupId, Guid MemberId) : IRequest<Result>`: owner, member (404); `CancelInvite()`; `DELETE …/members/{memberId:guid}/invite` → 204.
- [ ] **Step 1: Failing tests**
  - `InviteMemberValidatorTests`: `email` missing → required, `" "` → required, `42` → invalidEmail, `"no-at-sign"` → invalidEmail, `" Max@Example.com "` valid.
  - `MemberLinksTests` (owner A, group Anna, Olya, Max; user B):
    - `Should_LinkOwner_ToMember`: `PUT …/Anna/link` → 200 `{ linked: true, linkedName: <A's name> }`; again → 200; `GET` group → `myMemberId` Anna; `PUT …/Olya/link` → 400 `memberId: ["validation.alreadyLinked"]`.
    - `Should_RefuseLink_When_MemberLinkedToSomeoneElse`: B linked to Olya (TestData) → A `PUT …/Olya/link` → `memberId: ["validation.memberLinked"]`.
    - `Should_UnlinkMember`: B linked to Olya → `DELETE …/Olya/link` 204 → B's `GET` 404; `DELETE` again 204.
    - `Should_InviteMember`: `PUT …/Max/invite { email: " Max@Example.com " }` → 200 `invitedEmail "max@example.com"`, `InvitedAt` set; again with `"m@x.pl"` → replaced; `DELETE …/Max/invite` 204 → `invitedEmail null`.
    - `Should_RefuseInvites`: A's own email in upper case → `email: ["validation.inviteSelf"]`; Olya invited as `o@x.pl`, then Max with `O@X.pl` → `email: ["validation.alreadyInvited"]`; B linked to Olya, invite Olya → `memberId: ["validation.memberLinked"]`; both of the last two in one request (Olya linked, email taken by Max) → one 400 with `memberId` and `email`.
    - `Should_ClearInvite_When_MemberLinked`: Anna invited, then A links Anna → `invitedEmail null`.
    - `Should_Return404_When_NotOwner`: B (linked to Olya) on all four routes → 404; a member of another group → 404; an invalid body to an unknown group → 400.
    - `Should_RemoveLinkedMember_When_Unused`: Max invited and Olya linked, both unused → `DELETE …/members/{id}` 204 for each.
  - `OpenApiTests.Should_DescribeGroups` (extended): `/v1/groups/{id}/members/{memberId}/link` (put, delete), `/v1/groups/{id}/members/{memberId}/invite` (put, delete).
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite.
- [ ] **Step 3: Commit** — `feat: link group members and invite them by email`

### Task 4: Invitations, leaving and linking through the share link

**Files:** new `Common/Groups/GroupInviteEmails.cs`, new `Features/GroupInvites/{GroupInvitesEndpoints.cs, ListGroupInvites/*, AcceptGroupInvite/*, DeclineGroupInvite/*}`, new `Features/Groups/{LeaveGroup,LinkThroughShare}/*`, `Features/Groups/{GroupsEndpoints,SharedGroupsEndpoints}.cs`, `Program.cs`; tests new `tests/Mita.Api.IntegrationTests/Features/Groups/GroupInvitesTests.cs`, `OpenApiTests.cs`.

**Interfaces:**
- Consumes: Tasks 2-3.
- Produces:
  - `GroupInviteEmails.CurrentAsync(this AppDbContext db, Guid userId, CancellationToken ct) : Task<string>` (the user's email trimmed and lower-cased).
  - `sealed record GroupInviteDto(Guid MemberId, Guid GroupId, string GroupName, string MemberName, string OwnerName, DateTimeOffset InvitedAt)`; `ListGroupInvitesQuery(int Page, int PageSize) : IRequest<Result<PageResponse<GroupInviteDto>>>` (validator: paging); members whose `InvitedEmail` equals the user's current email and `UserId` is null, excluding groups where the user is already linked, newest first (`InvitedAt`, then `MemberId`, descending); `GET /v1/group-invites?page=1&pageSize=100` (`MapV1().WithTags("Group invites")`, user policy); `Program.cs` maps it after groups.
  - `AcceptGroupInviteCommand(Guid MemberId) : IRequest<Result<GroupSummaryDto>>`: transaction; lock the member's group `FOR UPDATE`; the member must hold the user's current email and be unlinked (404 otherwise); the user linked to another member of that group → 400 `memberId: ["validation.alreadyLinked"]`; `LinkTo(user)`; `POST /v1/group-invites/{memberId:guid}/accept` → 200 with the group's summary as the user sees it.
  - `DeclineGroupInviteCommand(Guid MemberId) : IRequest<Result>`: same lookup (404); `CancelInvite()`; `POST /v1/group-invites/{memberId:guid}/decline` → 204.
  - `LeaveGroupCommand(Guid GroupId) : IRequest<Result>`: transaction, `OpenAsync(GroupScope.User(id), Update)` (404); `MyMemberId` null → 404; `Unlink()`; `DELETE /v1/groups/{id:guid}/membership` → 204 (the owner may use it to unlink themselves).
  - `LinkThroughShareCommand(GroupScope Scope) : IRequest<Result<GroupSummaryDto>>` (a link scope with the member header): transaction, `OpenForWriteAsync(scope, Update)` (404, 403 `group.memberRequired`); the member linked to the user → 200 unchanged; linked to another user → 400 `memberId: ["validation.memberLinked"]`; the user linked to another member → 400 `memberId: ["validation.alreadyLinked"]`; `LinkTo(user)`; `PUT /v1/shared/groups/{token}/membership` mapped in `SharedGroupsEndpoints` with `.RequireAuthorization(AuthExtensions.UserPolicy)` added (anonymous with a good token → 401, a bad token → 404 whoever calls). Order: 429 → 404 token → 401 → 403 XSRF → 404 group → 403 member → 400.
- [ ] **Step 1: Failing tests** (`GroupInvitesTests`; owner A with group "Trip" Anna, Olya, Max; users B and C)
  - `Should_ListInvites_ForMatchingEmail`: Olya invited with B's email in upper case → B's `GET /v1/group-invites` → one item with properties exactly `memberId, groupId, groupName, memberName, ownerName, invitedAt` (`"Trip"`, `"Olya"`, A's name); C's list empty; after B is linked to Max (TestData) the invitation is no longer listed.
  - `Should_AcceptInvite_OnlyForMatchingEmail`: C accepts B's invitation → 404; B accepts → 200 summary `{ name: "Trip", role: "member" }`; Olya linked to B, `invitedEmail null`; B's `GET /v1/groups` lists "Trip"; accepting again → 404; an invitation cancelled by A before B accepts → 404.
  - `Should_DeclineInvite`: B declines → 204, A sees `invitedEmail null`, B's list empty; decline again → 404.
  - `Should_RefuseAccept_When_AlreadyLinkedInGroup`: B linked to Max, Olya invited with B's email (TestData) → accept → 400 `memberId: ["validation.alreadyLinked"]`.
  - `Should_FollowEmailChange`: invitation for `old@example.com` while B's email is `old@example.com`, B changes their email through `PUT /v1/me` → the invitation is not listed and accept answers 404.
  - `Should_LeaveGroup`: B linked to Olya → `DELETE /v1/groups/{id}/membership` 204 → B's `GET` 404, Olya unlinked; again → 404; A linked to Anna leaves → 204 and keeps owner access with `myMemberId null`.
  - `Should_LinkThroughShareLink`: link on; B signed in with XSRF sends `PUT /v1/shared/groups/{token}/membership` with `X-Group-Member` Olya → 200 `{ role: "member" }`; again → 200; with Max → `memberId: ["validation.alreadyLinked"]`; C with Olya → `memberId: ["validation.memberLinked"]`; anonymous → 401; unknown token → 404; no header → 403 `group.memberRequired`; signed in without `X-XSRF-TOKEN` → 403 `auth.xsrf`.
  - `Should_LinkOnce_When_RequestsRace`: B sends the share-link link for Olya and for Max at the same moment → one 200, one 400 `alreadyLinked`, exactly one member linked to B; Olya invited with B's email while B links Max through the link and accepts at the same moment → one succeeds, the other answers 400 `alreadyLinked`.
  - `OpenApiTests`: `Should_DescribeGroupInvites` (`/v1/group-invites` get, `/v1/group-invites/{memberId}/accept` and `/decline` post, tag `Group invites`); `Should_DescribeGroups` gains `/v1/groups/{id}/membership` (delete); `Should_DescribeSharedGroups` gains `/v1/shared/groups/{token}/membership` (put).
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite.
- [ ] **Step 3: Commit** — `feat: accept group invitations and link through the share link`

### Task 5: Group balances on the Debts page and the API contract

**Files:** new `Common/Groups/GroupMemberFigures.cs`, `Features/Groups/GetGroupBalances/GetGroupBalancesHandler.cs`, `Common/Debts/{DebtSummaries,DebtSummaryDto}.cs`, new `Features/Debts/ListGroupDebts/{ListGroupDebtsQuery,ListGroupDebtsEndpoint,ListGroupDebtsHandler,ListGroupDebtsValidator,GroupDebtDto}.cs`, `Features/Debts/DebtsEndpoints.cs`, `docs/api-contract.md`; tests new `tests/Mita.Api.IntegrationTests/Features/Debts/GroupDebtsTests.cs`, `Features/Overview/OverviewTests.cs`, `OpenApiTests.cs`.

**Interfaces:**
- Consumes: Task 2 (`GroupMember.UserId`), `RateTable.PlnValuesAsync`, `RateTable.Rate`, `MinorUnits.Convert`.
- Produces:
  - `sealed record GroupMemberFigures(Guid MemberId, long Paid, long Share, long Sent, long Received)` with `long Balance => Paid - Share + Sent - Received`; `static Task<IReadOnlyDictionary<Guid, GroupMemberFigures>> FiguresAsync(this AppDbContext db, IReadOnlyCollection<Guid> memberIds, CancellationToken ct)` — four grouped sums over live entries filtered by member ids (any group), zeros for members without entries. `GetGroupBalancesHandler` uses it instead of its own sums (answers unchanged).
  - `sealed record LinkedGroupBalance(Guid GroupId, string Name, Currency Currency, Guid MemberId, long Balance)`; `static Task<IReadOnlyList<LinkedGroupBalance>> LinkedBalancesAsync(this AppDbContext db, Guid userId, CancellationToken ct)` in `GroupMemberFigures.cs`, in `GET /v1/groups` order.
  - `sealed record DebtSummaryDto(long OwedToMe, long OwedByMe, int Open, int Overdue, int Groups)`; `SummarizeAsync` adds the linked balances by the Decisions rule (`null` when a needed rate was never stored).
  - `sealed record GroupDebtDto(Guid GroupId, string Name, string Currency, long Balance)`; `ListGroupDebtsQuery(int Page, int PageSize) : IRequest<Result<PageResponse<GroupDebtDto>>>` (validator: paging, default and maximum 100); `GET /v1/debts/groups?page=1&pageSize=100`.
  - Contract: endpoint rows for the eleven new routes; types `Group.role`/`myMemberId`, `GroupMember.linked`/`linkedName`/`invitedEmail`, `GroupSummary.role`, `GroupInvite`, `GroupInviteInput`, `GroupDebt`, `DebtSummary.groups`; a subsection "Linked members and invitations" in "Expense groups" stating every Decisions row the API shows (links, invitations and their matching, roles, who manages what, owner-only routes, actors of linked users, personal data, locks, order of checks of the new routes); "Groups and members" and "Share link" mention linked users and `PUT …/membership`; "Debts" gains "Groups on the Debts page" (lines and the summary rule) and `Overview.debts` says it includes groups; validation table rows for the four new codes, `invalidEmail` and `required` mention `email`; 401 row for `PUT /v1/shared/groups/{token}/membership`; 503 rows for `GET /v1/debts` and `GET /v1/overview` mention a group currency. The sentence "No endpoint creates a transaction or a debt from a group" stays.
- [ ] **Step 1: Failing tests** (`GroupDebtsTests`, `WithIsolatedData`, EUR 4.30 dated today)
  - `Should_ListGroupBalances`: B is linked to Olya in A's EUR "Trip" where Olya paid 9000 for Anna, Olya and Max (+6000), to Kasia in C's PLN "Home" where C's member paid 30000 for C and Kasia (−15000), and to a member of "Done" whose entries net to 0 → `GET /v1/debts/groups` as B → `[Done 0, Home -15000 PLN, Trip 6000 EUR]` by creation (newest first), properties exactly `groupId, name, currency, balance`; a deleted expense does not count.
  - `Should_IncludeGroupBalances_InSummary`: the same data plus B's private lent debt of 10000 PLN → `GET /v1/debts` summary `owedToMe 35800` (10000 + 6000 × 4.30), `owedByMe 15000`, `open 1`, `groups 2`; `GET /v1/overview` `debts` equal.
  - `Should_IgnoreGroups_WhereUserIsNotLinked`: A owns "Trip" without a link → A's group lines `[]`, summary zeros with `groups 0`; after A links Anna → one line.
  - `Should_Return503_When_GroupCurrencyHasNoRate`: B linked in a KWD group with a non-zero balance and no KWD rate → `GET /v1/debts` and `GET /v1/overview` 503; `GET /v1/debts/groups` 200 (no conversion).
  - `OverviewTests.Should_ReturnEmptyOverview_When_UserHasNoData` (adjusted): `debts.groups 0`.
  - `OpenApiTests.Should_DescribeDebts` (extended): `/v1/debts/groups` (get).
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then the full suite (the balances tests of 3a pass unchanged).
- [ ] **Step 3: Commit** — `feat: group balances on the debts page`
- [ ] **Step 4: Contract** as above; `be-run.sh 'dotnet build -warnaserror && dotnet test'` green. **Commit** — `docs: api contract for linked group members`

### Task 6: Frontend contract and the mock backend

Frontend repo, branch `feature/group-accounting` from `origin/develop`.

**Files:** `app/shared/api/contract.ts`, `mocks/{db,groups,debts}.ts`, new `mocks/handlers/groupLinks.ts`, `mocks/handlers/{groups,debts,index}.ts`, `mocks/seed/groups.ts`, `i18n/locales/*.json` (the four validation messages); tests new `tests/unit/mocks/groupLinks.test.ts`, `tests/unit/mocks/{seedGroups,debts,db,groups,groupLink}.test.ts`.

**Interfaces:**
- Consumes: the Task 5 contract.
- Produces:
  - `contract.ts`: `GroupRole = 'owner' | 'member' | 'link'`; `GroupDto.role`, `GroupDto.myMemberId: string | null`; `GroupMemberDto.linked: boolean`, `linkedName: string | null`, `invitedEmail: string | null`; `GroupSummaryDto.role: 'owner' | 'member'`; `GroupInviteDto = { memberId; groupId; groupName; memberName; ownerName; invitedAt }`; `GroupInviteInput = { email: string }`; `GroupDebtDto = { groupId; name; currency: Currency; balance: number }`; `DebtSummary.groups: number`.
  - `db.ts`: `GroupMemberRow` gains `userId: string | null`, `invitedEmail: string | null`, `invitedAt: string | null`; `version: 6`.
  - `groups.ts`: scope kind `owner` → `user` (`{ kind: 'user'; userId; groupId }`, opens owned or linked groups; the actor of a linked non-owner is its member); `groupView(state, row, viewer: { role: GroupRole; myMemberId: string | null })`; `groupSummaries` lists owned and linked groups with `role`; `linkedBalances(state, userId): { group: GroupRow; balance: number }[]` (live entries, `GET /v1/groups` order).
  - `handlers/groupLinks.ts` `registerGroupLinkHandlers`: the four owner routes, the three invitation routes, `DELETE /v1/groups/:id/membership`, `PUT /v1/shared/groups/:token/membership` (401 without a session); the backend's rules, codes and field names; emails compared lower-cased.
  - `debts.ts`: `summarizeDebts` adds the linked balances (each group converted with `rateOn` at today, missing → `ratesUnavailable()`) and `groups`; `handlers/debts.ts` adds `GET /v1/debts/groups` (`paginate` 100/100).
  - Seed (`demoGroups`): group `grp_krakow` "Kraków" (PLN, owner `usr_oleh`, created after Albania) with members `gmb_oleh` (Oleh, linked to `usr_oleh`), `gmb_k_anna` (Anna, invited `anna@demo.mita.app`), `gmb_taras` (Taras), expense `gex_pierogi` "Pierogi" 30000 PLN by Oleh, equal between the three, cafes, today − 5, event by `usr_oleh`; no share link. Every other member row gets `userId: null`, `invitedEmail: null`, `invitedAt: null`.
- [ ] **Step 1: Failing unit tests**
  - `groupLinks.test.ts`: the `LinkedMembersTests`, `MemberLinksTests`, `GroupInvitesTests` and `GroupDebtsTests` rows that the mock can express (owner-only details, `role`/`myMemberId` per caller, linked actor in history, owner-only routes 404 for a linked user, link/unlink/invite/cancel with the four codes, invitations by email case-insensitively, accept/decline/leave, share-link membership 401/403/400/200, `GET /v1/debts/groups` and the summary with `groups`).
  - `seedGroups.test.ts`: Anna's invitations `[Kraków as Anna by Oleh Kravets]`; Oleh's groups `["Kraków"]` (role owner, total 30000); after Anna accepts, her group lines `[Kraków -10000 PLN]` and her summary `owedByMe` grows by 10000.
  - `db.test.ts`: a stored state with `version: 5` is reseeded.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass,** then lint, typecheck and the mock E2E suite (`bun run test:e2e`; existing specs pass unchanged).
- [ ] **Step 3: Commit** — `feat: linked group members in the mock backend`

### Task 7: Link and invite members from the members dialog

**Files:** `app/features/groups/utils/scope.ts`, `app/features/groups/api/groupsApi.ts`, `app/features/groups/composables/{useGroupMutations,useGroupCurrencies}.ts`, new `app/features/groups/schemas/inviteSchema.ts`, new `app/features/groups/components/MemberAccountControls.vue`, `components/MembersDialog.vue`, `app/pages/tools/groups/[id].vue`, `i18n/locales/*.json`; tests `tests/unit/features/groups/scope.test.ts`, new `tests/unit/features/groups/inviteSchema.test.ts`, new `tests/e2e/groupLinks.spec.ts`.

**Interfaces:**
- Consumes: Task 6.
- Produces:
  - `scope.ts`: `GroupScope = { kind: 'user'; groupId } | { kind: 'link'; token; memberId }`, `userScope(groupId)` replacing `ownerScope`; `canManage(group: Pick<GroupDto, 'role'>): boolean` (`role === 'owner'`); `scopePath`, `scopeKey`, `ratesPath`, `memberHeaders` keep their rules (`user` where `owner` was). `useGroupCurrencies` pins the base currency for the `user` kind.
  - `groupsApi`: `linkMe(id, memberId) → GroupMemberDto`, `unlinkMember(id, memberId)`, `invite(id, memberId, body: GroupInviteInput) → GroupMemberDto`, `cancelInvite(id, memberId)`; `useGroupMutations` gains `linkMe`, `unlinkMember`, `invite`, `cancelInvite` (invalidate `'groups'`).
  - `inviteSchema.ts`: `inviteRules(t)`: email required, a valid address (the shared email rule of the auth forms), ≤ 254.
  - `MemberAccountControls` (`group`, `member`): a status line "You" (`member.id === group.myMemberId`), "Linked to {name}" (`linkedName`), "Invited: {email}" (`invitedEmail`), or nothing; actions: "This is me" (`aria-label` "This is me: {name}", shown while `myMemberId` is null and the member is not linked; toast "Linked to you"), "Invite {name}" (unlinked members; reveals an email `InputText` labelled "Email of {name}" with "Send invitation", errors under it; toast "Invitation sent"), "Cancel the invitation of {name}" (toast "Invitation cancelled"), "Unlink {name}" (linked members; confirm "Unlink {name}?" / "They no longer see the group in their app. Their entries stay."; toast "Member unlinked"); 44px targets.
  - `MembersDialog`: each row renders `MemberAccountControls` below its name; the dialog shows the hint "Let them know: the invitation waits for them under Tools → Groups in their dock.mita app." under the list. Page actions Members, Share and Edit group show when `canManage(group)`.
- [ ] **Step 1: Failing unit tests**: `scope.test.ts` (`user` kind paths and keys, `canManage` for the three roles); `inviteSchema.test.ts` (blank → required, `"no-at-sign"` → invalid, 255 characters → too long, `"olya@example.com"` valid).
- [ ] **Step 2: Failing E2E** (`groupLinks.spec.ts`, both projects): "links you to a member" (Albania → Members → "This is me: Anna" → Anna's row shows "You", toast "Linked to you", the other rows no longer offer "This is me"); "invites a member and cancels" (Invite Olya → "olya@example.com" → Send invitation → "Invited: olya@example.com" → Cancel the invitation of Olya → gone); "refuses your own email" (Invite Max → "anna@demo.mita.app" → "This is your own email. Use “This is me” instead."); "unlinks a member" (This is me on Anna, Unlink Anna → confirm → "This is me" offered again).
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite; all pass.
- [ ] **Step 4: Commit** — `feat: link and invite group members`

### Task 8: Invitations and shared groups for the invitee

**Files:** `app/features/groups/api/{groupsApi,keys}.ts`, new `app/features/groups/composables/useGroupInvites.ts`, `composables/useGroupMutations.ts`, new `app/features/groups/components/GroupInvites.vue`, `components/{GroupCard,BalancesCard}.vue`, `app/pages/tools/groups/{index,[id]}.vue`, `i18n/locales/*.json`; tests `tests/e2e/groupLinks.spec.ts`.

**Interfaces:**
- Consumes: Task 7.
- Produces:
  - `groupsApi`: `invites() → Page<GroupInviteDto>` (`pageSize: 100`), `acceptInvite(memberId) → GroupSummaryDto`, `declineInvite(memberId)`, `leave(id)`; `groupKeys.invites = ['groups', 'invites']`; `useGroupInvites() → { items, isPending, isError, refetch }`; `useGroupMutations` gains `acceptInvite`, `declineInvite`, `leave` (`leave` removes the group's scoped queries and invalidates the list, as `remove` does).
  - `GroupInvites` (no props): a `section` "Invitations" (`aria-labelledby`) shown above the group list when there are invitations; each item "{owner} invites you to “{group}” as {member}." with "Accept" and "Decline" (`aria-label` "Accept the invitation to {group}" / "Decline the invitation to {group}"); toasts "You joined “{group}”" and "Invitation declined"; focus moves to the next item or to the page heading.
  - `GroupCard`: a "Shared with you" tag for `role === 'member'`.
  - Group page: for `role === 'member'` the header action "Leave group" (desktop button, mobile icon button; confirm "Leave “{name}”?" / "The group disappears from your app. Its entries stay in the group."; toast "You left the group"; navigates to `/tools/groups`). `BalancesCard` names the row of `myMemberId` "{name} (you)".
- [ ] **Step 1: Failing E2E** (`groupLinks.spec.ts`): "accepts an invitation" (`/tools/groups` → "Oleh Kravets invites you to “Kraków” as Anna." → Accept → toast "You joined “Kraków”" → the card "Kraków" with "Shared with you" → open it → no "Members", "Share" or "Edit group"; balances "Anna (you)" "-zł 100.00"; Add expense works and the row shows "Added by Anna"); "declines an invitation" (Decline → "Invitation declined", no Invitations section, no Kraków card); "leaves a group" (accept, open Kraków, Leave group → confirm → `/tools/groups` without Kraków, toast "You left the group").
- [ ] **Step 2: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite; all pass.
- [ ] **Step 3: Commit** — `feat: group invitations and shared groups`

### Task 9: Link the member you picked to your account

**Files:** `app/features/groups/api/groupsApi.ts`, `composables/useGroupMutations.ts`, new `app/features/groups/components/LinkAccountOffer.vue`, `app/pages/g/[token].vue`, `i18n/locales/*.json`; tests `tests/e2e/groupLink.spec.ts`.

**Interfaces:**
- Consumes: Tasks 6-8; `useSession().status`.
- Produces:
  - `groupsApi.linkThroughShare(token, memberId) → GroupSummaryDto` (`PUT /v1/shared/groups/<token>/membership` with `memberHeaders`); `useGroupMutations.linkThroughShare` (invalidates `'groups'`).
  - `LinkAccountOffer` (`group`, `member`): shown on the link page under the `LinkBar` when the session is authenticated and the picked member is not linked: "Is this you? Link {name} to your dock.mita account to see the group in your app." with "Link to my account"; success replaces it with "{name} is linked to your account." and a `NuxtLinkLocale` "Open in my groups" to `/tools/groups/<group id>`; `alreadyLinked` and `memberLinked` show their messages in place; nothing for anonymous visitors (the anonymous page still calls only the link routes and the session probe).
- [ ] **Step 1: Failing E2E** (`groupLink.spec.ts`): signed-in context: Anna opens the Albania link, picks Olya → "Link to my account" → "Olya is linked to your account." → "Open in my groups" → `/tools/groups/grp_albania`, balances "Olya (you)"; an anonymous context sees no offer.
- [ ] **Step 2: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite; all pass.
- [ ] **Step 3: Commit** — `feat: link a share-link member to your account`

### Task 10: Group balances on the Debts page and the Overview

**Files:** `app/features/debts/api/{debtsApi,keys}.ts`, new `app/features/debts/composables/useGroupDebts.ts`, new `app/features/debts/components/GroupDebtList.vue`, `app/pages/tools/debts.vue`, `app/features/overview/utils/overviewView.ts`, `app/shared/api/invalidation.ts`, `i18n/locales/*.json`; tests `tests/unit/features/overview/overviewView.test.ts`, `tests/unit/shared/invalidation.test.ts`, `tests/e2e/debts.spec.ts`.

**Interfaces:**
- Consumes: Task 6; `MoneyText`.
- Produces:
  - `debtsApi.groups() → Page<GroupDebtDto>` (`pageSize: 100`); `debtKeys.groups = ['debts', 'groups']`; `useGroupDebts() → { items, isPending, isError, refetch }`.
  - `GroupDebtList` (no props): a `section` "Groups" (`aria-labelledby`) after "Personal debts", shown when there are lines; each line a `NuxtLinkLocale` (≥ 44px) to `/tools/groups/<id>` with the group name, the signed balance in the group currency (`MoneyText signed`) and "You get back" (> 0), "You owe" (< 0) or "Settled"; its own skeleton and compact `ErrorState`.
  - `overviewView.ts`: `hasOpenDebts(debts)` is `debts.open > 0 || debts.groups > 0`.
  - `invalidation.ts`: the `groups` change also invalidates `['debts']` and `['overview']`.
- [ ] **Step 1: Failing unit tests**: `overviewView.test.ts` (`{ open: 0, groups: 1 }` → true, `{ open: 0, groups: 0 }` → false); `invalidation.test.ts` (`'groups'` row).
- [ ] **Step 2: Failing E2E** (`debts.spec.ts`): "shows group balances" (Albania → Members → This is me on Anna → Tools → Debts → section "Groups" with "Albania 2026" "+€42.32" "You get back"); "counts accepted groups in I owe" (accept Kraków → Debts → "Kraków" "-zł 100.00" "You owe", summary "I owe" "zł 1,600.00").
- [ ] **Step 3: Run, see them fail, implement, run** unit, lint, typecheck and the mock E2E suite; all pass.
- [ ] **Step 4: Commit** — `feat: group balances on the debts page`

### Task 11: Real-backend spec, smoke and docs

Frontend repo, same branch; the backend checkout on `feature/group-accounting` (Tasks 1-5).

**Files:** new `tests/e2e-real/group-links.real.spec.ts`; the project guide ("Real-backend suite": a `group-links` line; the auth-call count per run rises by four); outside the repo `dock.mita-work/scripts/ga-real-smoke.sh` (Decisions "Real smoke").
- [ ] **Step 1: Write `group-links.real.spec.ts`** (helpers in the style of `groups.real.spec.ts`; two browser contexts): A registers; B's email is generated; A creates the EUR group "Gdańsk" with Member 1 = A's name, "Bartek" and "Ola"; Members → "This is me" on A's member; Invite Bartek with B's email → "Invited: <email>"; B registers in the second context, opens Tools → Groups → Accept → the card "Gdańsk" "Shared with you"; B opens it, adds "Pierogi" 30 € paid by Bartek split equally → "Added by Bartek"; A (reload) sees "Added by Bartek"; A's Debts page: "Gdańsk" "-€10.00" "You owe" and "I owe" "zł 43.00"; B's Debts page: "Gdańsk" "+€20.00" and "Owed to me" "zł 86.00"; B leaves the group → no Gdańsk card; A deletes the group; both users are deleted.
- [ ] **Step 2: Run** `wsl.exe -d Debian -- bash /home/jaloqa/dock.mita-work/scripts/ga-real-smoke.sh`; every real spec passes, the X-Forwarded-For check passes, both smoke projects are torn down, `pgdb`, `dockmitabackend` and `dock-mita-frontend-web-1` untouched.
- [ ] **Step 3: Run** `bun run lint && bun run typecheck && bun test tests/unit`; all green.
- [ ] **Step 4: Commit** — `test: real-backend spec for linked group members`

### Task 12: Pull requests

- [ ] Backend: rebase `feature/group-accounting` on `origin/develop`, fold fix-ups into their task commits (tree unchanged), `dotnet build -warnaserror` and `dotnet test --logger junit`, push with `--force-with-lease`, open a PR to `develop` with a plain description, wait for `check-source-branch`, `gh pr merge --rebase`.
- [ ] Frontend: after the backend PR is merged, the same flow with lint, typecheck, unit, the mock E2E suite and the real smoke green.
- [ ] Both checkouts back on `develop`, pulled; local `feature/group-accounting` branches deleted.

## Deferred (not in this plan)

Plan 4b: the "To confirm" list; confirmation into personal records (the user's share as a transaction, the rest and repayments as account legs that are neither income nor expense); changes and deletions of confirmed entries; late linking (past entries appear in "To confirm", which needs nothing from this plan beyond the link); settlements of specific shares and repayments that cover the oldest open shares; category, tag and description suggestions (standard keys, group category names). Also: an automatic "This is me" at group creation, transferring ownership, email delivery of invitations, a notification badge for invitations, and the 3a/3b deferred lists.
