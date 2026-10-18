# Authentication and Users Implementation Plan

**Goal:** Real users on the backend. Registration, login, logout and refresh with MS Identity, a JWT in httpOnly cookies plus refresh-token rotation and XSRF protection, and the `/v1/me` and `/v1/settings` endpoints exactly as the frontend contract and mocks define them.

**Architecture:**
- **Identity:** MS Identity (`IdentityUser<Guid>` stores) lives in `Mita.Infrastructure` on the existing `AppDbContext`. Domain entities `User` and `UserSettings` live in `Mita.Domain`.
- **Auth slices:** `Features/Auth/{Register,Login,Refresh,Logout}` issue short-lived access JWTs and opaque rotating refresh tokens, both as httpOnly cookies, plus a readable XSRF cookie.
- **Validation:** the existing JWT bearer validation also reads the access token from the cookie. A double-submit XSRF filter protects unsafe methods when a request is authenticated by cookie.
- **User slices:** `Features/Users/{GetMe,UpdateMe,DeleteMe}` and `Features/Settings/{GetSettings,UpdateSettings}` run through the MediatR pipeline from the foundation.

**Tech Stack:** .NET 10, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (Identity core stores only, no UI or cookie scheme), EF Core migrations (Npgsql), JWT bearer (HS256 from the foundation), ASP.NET Core rate limiting, FluentValidation, MediatR 12.5.0, HybridCache, xUnit v3, Shouldly, Moq, Bogus, Testcontainers.

**Spec:** This plan argues from these documents:
- The backend `CLAUDE.md`, `docs/STACK.md` and the foundation plan `docs/superpowers/plans/2026-10-18-backend-foundation.md`, which define the interfaces this plan consumes.
- The frontend contract in the frontend repo:
  - `docs/superpowers/specs/2026-10-15-frontend-mocks-design.md`;
  - `app/shared/api/contract.ts` (`UserDto`, `UpdateMeRequest`, `SettingsDto`, `NotificationPrefs`);
  - `mocks/handlers/users.ts` (validation rules and codes);
  - `app/shared/api/transports/http.ts` (sends `credentials: 'include'` and `Accept-Language`).
- The user's decision (2026-10-18): JWT in httpOnly cookie, rotating refresh token, XSRF.

## Decisions

| Topic | Decision |
|---|---|
| Session transport | Access JWT in cookie `mita_at` (HttpOnly, Secure outside Development, SameSite=Lax, Path=/, 15 min). Refresh token in cookie `mita_rt` (HttpOnly, Secure, SameSite=Lax, Path=`/v1/auth`, 30 days). The `Authorization: Bearer` header stays valid for Scalar and tests |
| XSRF | Double submit. Login, register and refresh also set `XSRF-TOKEN` (readable by JS, SameSite=Lax, random 32 bytes base64url). For POST, PUT, PATCH and DELETE authenticated by the `mita_at` cookie, header `X-XSRF-TOKEN` must equal the cookie, otherwise 403 ProblemDetails `{ title: "Forbidden", code: "auth.xsrf" }`. Bearer-authenticated and anonymous requests skip the check |
| Refresh tokens | Opaque 32 random bytes. Only the SHA-256 hash is stored, in table `refresh_tokens` (userId, family id, hash, expires, revokedAt, replacedBy). Rotation on every refresh. Reuse of a revoked token revokes the whole family (theft detection) and returns 401 |
| Registration | Open: `{ name, email, password }`. No email confirmation yet, because there is no email infrastructure; it is recorded for a later plan. On success it creates the user, settings with defaults, logs in (cookies) and returns 201 `UserDto` |
| Settings defaults | `baseCurrency: "PLN"`, `locale` from `Accept-Language` when it is one of `en`/`pl`/`ru`/`uk`, else `"en"`, `monthStartDay: 1`, `theme: "system"`, `notifications: { budgetLimit: true, dailyReminder: true, weeklyReport: false, goals: true }` (same as the frontend `DEFAULT_SETTINGS`) |
| Password policy | At least 10 characters, at most 128, no composition rules (NIST 800-63B). Identity's digit/upper/lower/non-alphanumeric requirements are switched off |
| Lockout | 5 failed logins lock the account for 15 minutes. A locked account and wrong credentials both return the same 401 `{ title: "Unauthorized", code: "auth.invalidCredentials" }`, so accounts cannot be probed |
| Rate limiting | Fixed window per client IP on `/v1/auth/*`: 10 requests per minute; above that, 429 ProblemDetails `{ title: "Too many requests" }` |
| CORS | Allowed origins from configuration `Cors:AllowedOrigins` (dev: `http://localhost:3000`), with credentials, headers `Content-Type`, `X-XSRF-TOKEN`, `Accept-Language`, `X-Correlation-Id` |
| Delete account | `DELETE /v1/me` with body `{ password }`: verifies the password, then deletes the user, settings and refresh tokens (cascade) and clears the cookies; 204. Feature tables will cascade from the user later |
| Not in this plan | Email confirmation and password reset (need email), external logins, the frontend login screen (a separate frontend plan), starter categories for new users (categories plan) |

## Global Constraints

- All foundation constraints apply: warnings are errors; central packages; `Api → Infrastructure → Domain`; records, `sealed`, no comments, English; `CancellationToken` everywhere; every endpoint is in the v1 group with explicit authorization; ProblemDetails for every error; correlation id.
- Contract with the frontend, byte for byte:
  - `UserDto { id: string (Guid), name, email, createdAt: ISO-8601 }`;
  - `SettingsDto { baseCurrency: "PLN"|"EUR"|"USD"|"UAH", locale: "en"|"pl"|"ru"|"uk", monthStartDay: 1..28 | "lastWorkingDay", theme: "system"|"light"|"dark", notifications: { budgetLimit, dailyReminder, weeklyReport, goals: bool } }`;
  - JSON camelCase.
- Validation codes, exactly as in `mocks/handlers/users.ts`:
  - a missing field gives `validation.required`;
  - a wrong type or value gives `validation.invalidValue`;
  - name trimmed longer than 40 characters gives `validation.nameTooLong`;
  - an email longer than 254 characters or not matching `^[^\s@]+@[^\s@]+\.[^\s@]+$` gives `validation.invalidEmail`;
  - an email used by another user (case-insensitive) gives `validation.emailTaken`;
  - a password shorter than 10 or longer than 128 characters gives `validation.passwordTooShort` or `validation.passwordTooLong`.
- `monthStartDay` is serialized as a number or the string `"lastWorkingDay"`, through one custom JSON converter used by every endpoint.
- Secrets never leave the server:
  - refresh tokens are stored hashed;
  - responses never contain tokens in the body;
  - logs never contain passwords, tokens or cookies (Serilog destructuring masks `password`, `token` and `Cookie` headers).
- Commit messages: Conventional Commits; never mention Claude, AI or assistants; no `Co-Authored-By`.

## Review Focus

- A stolen refresh token used after the real client has already rotated it must log out every session of that family and must never yield a new pair. Pinned in Task 4.
- A cross-site form POST must fail with 403 and change nothing, even with valid cookies. Such a POST carries the cookies but not the `X-XSRF-TOKEN` header. Pinned in Task 5.
- Two registrations racing with the same email in different letter case must yield one user and one `validation.emailTaken`, never two accounts or a 500. Pinned in Task 3 through a unique normalized-email index.
- Changing the email to one another user already has, in different case, must give `validation.emailTaken`. Pinned in Task 6.
- After logout or account deletion, the old access cookie (still inside its 15 minutes) must no longer reach `/v1/me` successfully. Pinned in Task 7 through a per-user security stamp checked on every authenticated request (stamp cached per user via HybridCache, invalidated on logout, password or email change and delete).

---

## File map

```
src/Mita.Domain/Users/User.cs, UserSettings.cs, Currency.cs, Locale.cs, ThemePreference.cs, MonthStart.cs, NotificationPrefs.cs
src/Mita.Infrastructure/Identity/AppUser.cs (IdentityUser<Guid>), IdentitySetup.cs
src/Mita.Infrastructure/Persistence/Configurations/UserSettingsConfiguration.cs, RefreshTokenConfiguration.cs
src/Mita.Infrastructure/Persistence/Migrations/<timestamp>_Users.cs
src/Mita.Infrastructure/Authentication/RefreshToken.cs, TokenService.cs (ITokenService), SecurityStampCache.cs
src/Mita.Api/Authentication/AuthCookies.cs, XsrfFilter.cs, SecurityStampValidation.cs
src/Mita.Api/Common/Json/MonthStartJsonConverter.cs
src/Mita.Api/Features/Auth/{Register,Login,Refresh,Logout}/...  AuthEndpoints.cs
src/Mita.Api/Features/Users/{GetMe,UpdateMe,DeleteMe}/...        UsersEndpoints.cs
src/Mita.Api/Features/Settings/{GetSettings,UpdateSettings}/...  SettingsEndpoints.cs
tests/Mita.Api.UnitTests/Features/..., tests/Mita.Api.IntegrationTests/Features/..., Infrastructure/AuthClient.cs
```

---

### Task 1: Domain model, Identity stores and the first migration

**Files:** Domain types above; `AppUser`, `IdentitySetup.AddMitaIdentity(IServiceCollection)`, `AppDbContext` becomes `IdentityUserContext<AppUser, Guid>`, configurations, the migration; tests `tests/Mita.Api.UnitTests/Users/UserSettingsTests.cs`, `tests/Mita.Api.IntegrationTests/Persistence/MigrationTests.cs`.

**Interfaces:**
- Produces:
  - `enum Currency { PLN, EUR, USD, UAH }`, `enum Locale { En, Pl, Ru, Uk }`, `enum ThemePreference { System, Light, Dark }`.
  - `readonly record struct MonthStart` with `static MonthStart Day(int day)` (1..28, otherwise `ArgumentOutOfRangeException`), `static MonthStart LastWorkingDay`, `bool IsLastWorkingDay` and `int? Value`.
  - `sealed record NotificationPrefs(bool BudgetLimit, bool DailyReminder, bool WeeklyReport, bool Goals)` with `static NotificationPrefs Default`.
  - `sealed class UserSettings` (`UserId` is the key, the 1:1 FK to the user cascades on delete) with `static UserSettings CreateDefault(Guid userId, Locale locale)`.
  - `AppUser : IdentityUser<Guid>, IAuditable` with `string Name` (≤ 40) and `CreatedAt`/`UpdatedAt`.
  - Unique index on `NormalizedEmail`.
  - `IdentitySetup` configures `PasswordOptions` (`RequiredLength = 10`, everything else false), `LockoutOptions` (5 attempts, 15 min, also for new users), `UserOptions.RequireUniqueEmail = true`, and `AddIdentityCore<AppUser>().AddEntityFrameworkStores<AppDbContext>()` without cookies or UI.
- [ ] **Step 1: Failing tests**
  - `Should_UseFrontendDefaults_When_CreatingDefaultSettings`: PLN, the given locale, day 1, system theme, `{ budgetLimit: true, dailyReminder: true, weeklyReport: false, goals: true }`.
  - `Should_RejectMonthStart_When_OutOfRange`: 0 and 29 throw.
  - `Should_ApplyMigrations_When_DatabaseIsEmpty`: the fixture database migrates, and the tables `AspNetUsers`, `user_settings` and `refresh_tokens` exist.
  - `Should_RejectSecondUser_When_NormalizedEmailRepeats`: a raw insert of the same normalized email violates the unique index.
- [ ] **Step 2: Run, see them fail, implement, add the migration `Users`, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: users, settings and identity stores with the first migration`

### Task 2: Token service and auth cookies

**Files:** `TokenService`, `RefreshToken`, `AuthCookies`; tests `tests/Mita.Api.UnitTests/Authentication/TokenServiceTests.cs`.

**Interfaces:**
- Consumes: `JwtOptions` (foundation), `TimeProvider`.
- Produces:
  - `interface ITokenService` with:
    - `Task<IssuedTokens> IssueAsync(AppUser user, Guid? familyId, CancellationToken ct)`;
    - `Task<RefreshOutcome> RefreshAsync(string refreshToken, CancellationToken ct)`;
    - `Task RevokeFamilyAsync(string refreshToken, CancellationToken ct)`;
    - `Task RevokeAllAsync(Guid userId, CancellationToken ct)`.
  - `sealed record IssuedTokens(string AccessToken, DateTimeOffset AccessExpires, string RefreshToken, DateTimeOffset RefreshExpires)`.
  - `RefreshOutcome` is one of `Rotated(IssuedTokens, AppUser)`, `Invalid` or `ReuseDetected`.
  - Access JWT claims: `sub` (user id), `email`, `name`, `stamp` (security stamp), `jti`; lifetime 15 min.
  - Refresh: 32 random bytes, base64url; the store keeps the SHA-256 hex; lifetime 30 days.
  - `static class AuthCookies` with `Write(HttpResponse, IssuedTokens, string xsrfToken, bool secure)` and `Clear(HttpResponse)`, using names and attributes exactly as in Decisions.
- [ ] **Step 1: Failing tests**
  - `Should_IssueAccessTokenWithSubStampAndFifteenMinutes`
  - `Should_StoreOnlyHash_When_IssuingRefreshToken`: the raw token does not appear in the store.
  - `Should_RotateAndRevokePrevious_When_Refreshing`
  - `Should_RevokeWholeFamily_When_RevokedTokenIsReused`: the second use of an old token gives `ReuseDetected`, and the newer token in the same family is revoked too.
  - `Should_ReturnInvalid_When_TokenExpiredOrUnknown`
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: token service with rotating refresh tokens and auth cookies`

### Task 3: Register and login

**Files:** `Features/Auth/Register/*`, `Features/Auth/Login/*`, `AuthEndpoints.cs`, rate limiter registration (`Extensions/RateLimitingExtensions.cs`), CORS (`Extensions/CorsExtensions.cs`, option `Cors:AllowedOrigins`); tests `tests/Mita.Api.IntegrationTests/Features/Auth/RegisterTests.cs`, `LoginTests.cs`, `Infrastructure/AuthClient.cs`.

**Interfaces:**
- Produces:
  - `POST /v1/auth/register` (anonymous) with `{ name, email, password }`: returns 201 `UserDto` and sets the cookies.
  - `POST /v1/auth/login` (anonymous) with `{ email, password }`: returns 200 `UserDto` and sets the cookies.
  - Wrong credentials and a locked account both give 401 `auth.invalidCredentials`.
  - Validation codes as in Global Constraints.
  - The policy `auth` limits `/v1/auth/*` to `RateLimiting:AuthPermitLimit` requests per minute per IP (options, validated ≥ 1; default 10). `MitaApiFactory` sets it to 1000 so the suite is not throttled; only `Should_Return429_When_TooManyAuthRequests` uses a factory with 10.
  - `AuthClient` (test helper) wraps an `HttpClient` with a cookie container. It has `RegisterAsync`, `LoginAsync`, `Xsrf` (from the cookie) and `WithXsrf(HttpRequestMessage)`.
- [ ] **Step 1: Failing tests**
  - `Should_CreateUserSettingsAndCookies_When_Registering`: 201; the body matches `UserDto`; cookies `mita_at`, `mita_rt` and `XSRF-TOKEN` are present with HttpOnly/Path as specified; settings default to locale `pl` when `Accept-Language: pl-PL`.
  - `Should_ReturnFieldErrors_When_RegisterInvalid`: name missing gives `name: [validation.required]`; a 41-character name gives `validation.nameTooLong`; `a@b` gives `validation.invalidEmail`; a 9-character password gives `validation.passwordTooShort`.
  - `Should_ReturnEmailTaken_When_SameEmailDifferentCase`
  - `Should_CreateOneUser_When_TwoRegistrationsRace`: 10 parallel registrations with case variants of one email give one 201 and the rest 400 `emailTaken`; there are no 500s.
  - `Should_Login_When_CredentialsValid`
  - `Should_Return401SameBody_When_PasswordWrongOrUserUnknown`
  - `Should_LockAfterFiveFailures`: the sixth attempt with the right password still gets 401.
  - `Should_Return429_When_TooManyAuthRequests`: the 11th request within a minute is rejected.
  - `Should_NotLogPassword`: captured log events contain no password value.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: registration and login with cookies, lockout and rate limiting`

### Task 4: Refresh and logout

**Files:** `Features/Auth/Refresh/*`, `Features/Auth/Logout/*`; tests `RefreshTests.cs`, `LogoutTests.cs`.

**Interfaces:**
- Consumes: `ITokenService`, `AuthCookies` (Task 2).
- Produces:
  - `POST /v1/auth/refresh` (anonymous; reads `mita_rt`): rotates and sets new cookies, returns 204. An invalid token gives 401 `auth.sessionExpired` and clears the cookies; reuse gives 401 `auth.sessionExpired`, revokes the family and clears the cookies.
  - `POST /v1/auth/logout` (anonymous-tolerant): revokes the refresh family if a cookie is present, bumps the security stamp if authenticated, clears the cookies, returns 204.
- [ ] **Step 1: Failing tests**
  - `Should_RotateCookies_When_Refreshing`
  - `Should_RevokeFamilyAndReturn401_When_OldRefreshReused`: client A refreshes; replaying A's previous cookie gives 401; A's current cookie now also gives 401.
  - `Should_ClearCookies_When_LoggingOut`
  - `Should_RejectRefresh_When_LoggedOut`
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: refresh token rotation with reuse detection and logout`

### Task 5: Cookie authentication and XSRF protection

**Files:** `Extensions/AuthExtensions.cs` (JWT bearer `OnMessageReceived` reads `mita_at` when no `Authorization` header), `Authentication/XsrfFilter.cs` (endpoint filter on the v1 group); tests `XsrfTests.cs`.

**Interfaces:**
- Consumes: foundation JWT validation and `User` policy.
- Produces:
  - Requests authenticated by cookie with an unsafe method go through the XSRF check from Decisions. A failure gives 403 `auth.xsrf` before any handler runs.
  - Bearer requests are not checked.
  - The auth endpoints themselves (`/v1/auth/*`) are exempt.
- [ ] **Step 1: Failing tests** (using the Testing-only endpoint `PUT /v1/test/echo-auth`, policy `User`, returning `{ userId }`):
  - `Should_Accept_When_CookieAndMatchingHeader`
  - `Should_Return403_When_CookieWithoutHeader`
  - `Should_Return403_When_HeaderMismatch`
  - `Should_Accept_When_BearerWithoutXsrf`
  - `Should_AllowGet_When_CookieWithoutHeader`
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: authenticate by cookie with double-submit xsrf protection`

### Task 6: `/v1/me` — get, update, delete

**Files:** `Features/Users/GetMe/*`, `UpdateMe/*`, `DeleteMe/*`, `UsersEndpoints.cs`; tests `tests/Mita.Api.UnitTests/Features/Users/UpdateMeValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Users/MeTests.cs`.

**Interfaces:**
- Consumes: `ICurrentUser`, `AuthClient`.
- Produces:
  - `GET /v1/me` returns `UserDto`; it is an `ICachedQuery` with key `me` and tag `me`.
  - `PUT /v1/me` takes `{ name, email }` and returns `UserDto`. It trims the name, updates `Email`/`UserName`/the normalized fields through `UserManager`, bumps the security stamp when the email changes, and invalidates the tag `me`.
  - `DELETE /v1/me` takes `{ password }`. A wrong password gives 400 `password: [validation.invalidPassword]`. Otherwise it deletes the user (cascade), revokes all tokens, clears the cookies and returns 204.
- [ ] **Step 1: Failing tests**
  - Validator: every row of the mock's rules; whitespace-only name gives `validation.required`; a 254-character valid email is OK and 255 gives `invalidEmail`.
  - `Should_ReturnCurrentUser`
  - `Should_UpdateNameAndEmail`, where the GET after PUT returns the new data (cache invalidated).
  - `Should_ReturnEmailTaken_When_OtherUserHasEmailInDifferentCase`
  - `Should_AllowSameEmail_When_ItIsOwnEmailInDifferentCase`
  - `Should_DeleteAccount_When_PasswordCorrect`: then login with the old credentials gives 401.
  - `Should_RejectDelete_When_PasswordWrong`
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: current user profile endpoints`

### Task 7: Security stamp check on every request

**Files:** `Authentication/SecurityStampValidation.cs` (JWT `OnTokenValidated`), `Infrastructure/Authentication/SecurityStampCache.cs`; tests `SessionInvalidationTests.cs`.

**Interfaces:**
- Produces:
  - `OnTokenValidated` compares the token's `stamp` claim with the user's current stamp, read through HybridCache key `stamp:{userId}` (5 min). A mismatch or a deleted user fails authentication with 401 `auth.sessionExpired`.
  - Logout, email change, account delete and (later) password change call `SecurityStampCache.InvalidateAsync(userId)` after updating the stamp.
- [ ] **Step 1: Failing tests**
  - `Should_Reject_OldAccessCookie_AfterLogout`: copy `mita_at` before logout, replay it, get 401.
  - `Should_Reject_OldAccessCookie_AfterAccountDelete`
  - `Should_Reject_OldAccessCookie_AfterEmailChange`: the response of the email change sets fresh cookies, so the current session keeps working.
  - `Should_StillWork_When_RedisDown`: the stamp is read from the database when the cache fails.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: reject tokens after logout, email change or account deletion`

### Task 8: `/v1/settings` — get and update

**Files:** `Features/Settings/GetSettings/*`, `UpdateSettings/*`, `SettingsEndpoints.cs`, `Common/Json/MonthStartJsonConverter.cs` (registered globally); tests `tests/Mita.Api.UnitTests/Common/MonthStartJsonConverterTests.cs`, `Features/Settings/UpdateSettingsValidatorTests.cs`, `tests/Mita.Api.IntegrationTests/Features/Settings/SettingsTests.cs`.

**Interfaces:**
- Produces:
  - `GET /v1/settings` returns `SettingsDto`; it is an `ICachedQuery` with key and tag `settings`.
  - `PUT /v1/settings` takes a full `SettingsDto` and returns `SettingsDto`, invalidating the tag `settings`. Validation follows the mock: every field is required (`validation.required`); an unknown currency, locale or theme, a `monthStartDay` outside 1..28 and not `"lastWorkingDay"`, or `notifications` not an object of four booleans gives `validation.invalidValue`; unknown extra properties are ignored.
- [ ] **Step 1: Failing tests**
  - Converter: `1 ↔ 1`, `"lastWorkingDay" ↔ LastWorkingDay`, `"x"`, `0`, `29` and `1.5` are rejected.
  - Validator rows for each field.
  - `Should_ReturnDefaults_ForNewUser`
  - `Should_PersistUpdate`
  - `Should_IsolateUsers`: user B never sees user A's settings, including through the cache.
  - `Should_Return400WithFieldErrors_When_BodyInvalid`: for example `baseCurrency: "GBP"` and `monthStartDay: 31` in one request.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: user settings endpoints matching the frontend contract`

### Task 9: OpenAPI, docs and the Docker smoke

**Files:** OpenAPI document (cookie security scheme `mita_at` plus bearer; tags Auth/Users/Settings), `README.md` (local run: `cp .env.example .env`, `docker compose up -d --build`, how to register via Scalar), `.env.example` (`Cors__AllowedOrigins__0`), `docs/api-contract.md` (endpoint table and error codes, for the frontend switch-over); test `OpenApiTests` extended.
- [ ] **Step 1: Failing test** — `Should_DescribeAuthUsersAndSettingsEndpoints`: the document contains `/v1/auth/login`, `/v1/me` and `/v1/settings` and both security schemes.
- [ ] **Step 2: Implement, run, see it pass;** Docker smoke:
  1. `docker compose up -d --build`;
  2. register with curl, using a cookie jar, and get 201;
  3. `GET /v1/me` with the jar gives 200;
  4. `PUT /v1/settings` without `X-XSRF-TOKEN` gives 403, and with it gives 200;
  5. logout gives 204, after which `GET /v1/me` gives 401;
  6. `docker compose down -v`.
- [ ] **Step 3: Commit** — `docs: api contract, openapi security schemes and local run guide`

### Task 10: Pull request

- [ ] Rebase on `origin/develop` and fold fix commits into their task commits. Run `dotnet build -warnaserror` and `dotnet test --logger junit` (Docker running); all must be green. Push, open a PR to `develop` (plain description), wait for `check-source-branch`, then `gh pr merge --rebase`.
