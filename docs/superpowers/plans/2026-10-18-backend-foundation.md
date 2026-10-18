# Backend Foundation Implementation Plan

**Goal:** A runnable .NET 10 backend skeleton with the agreed Vertical Slice structure, request pipeline, error contract, API versioning, OpenAPI, persistence base, Redis caching, authentication skeleton, test infrastructure and Docker, so feature slices can be added one by one against the frontend's `/v1` contract.

**Architecture:** Three projects with one-way dependencies `Mita.Api → Mita.Infrastructure → Mita.Domain`. Use cases live as slices under `Mita.Api/Features/<Feature>/<UseCase>/`, run through MediatR with a FluentValidation behavior, and return a `Result` that endpoints map to HTTP or RFC 9457 ProblemDetails. PostgreSQL through EF Core, integration tests on a real database via Testcontainers.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, MediatR 12.5.0, FluentValidation, Asp.Versioning.Http, Microsoft.AspNetCore.OpenApi + Scalar.AspNetCore, EF Core + Npgsql, Redis through `Microsoft.Extensions.Caching.Hybrid` (HybridCache) with `Microsoft.Extensions.Caching.StackExchangeRedis` as the distributed layer, JWT bearer, Serilog (+ Elastic APM agent, disabled without a server URL), xUnit v3, Shouldly, Moq, Bogus, Testcontainers.PostgreSql, Testcontainers.Redis, JunitXml.TestLogger, Docker.

**Spec:** This plan argues from three documents, which executors read alongside it:
- `CLAUDE.md` of this repo: architecture, rules, code style.
- `docs/STACK.md`: the agreed stack.
- The frontend design spec in the frontend repo, `docs/superpowers/specs/2026-10-15-frontend-mocks-design.md`. It defines the API contract the backend must serve: `/v1` paths, ProblemDetails with field errors whose messages are i18n codes such as `validation.required`, camelCase field names, the pagination shape `{ items, page, pageSize, total }`, and amounts in minor units.

## Decisions

| Topic | Decision |
|---|---|
| Project names | `Mita.Api`, `Mita.Domain`, `Mita.Infrastructure`, `Mita.Api.UnitTests`, `Mita.Api.IntegrationTests` (as in the CLAUDE.md structure); the CLAUDE.md commands section is corrected from `DockMita.*` to `Mita.*` |
| Solution | `Mita.slnx` at the repo root |
| Mediator | MediatR 12.5.0 — the last Apache-2.0 release before the commercial licence (user decision); version pinned, never upgraded to 13+ |
| Testcontainers | Used for PostgreSQL integration tests (user decision) |
| API docs | OpenAPI document from `Microsoft.AspNetCore.OpenApi` at `/openapi/v1.json`; Scalar UI at `/scalar` in Development only |
| Versioning | URL segment: every API route is under `/v{version:apiVersion}` with v1 as the only version; health and OpenAPI routes are unversioned |
| Errors | `Result`/`Error` in `Common/`; expected failures never throw; unhandled exceptions become 500 ProblemDetails without stack traces; field errors use camelCase field names and i18n codes as messages |
| Auth in this plan | JWT bearer validation, authorization policy `User` required by default on the v1 group, `ICurrentUser` from the `sub` claim. Issuing tokens (MS Identity, login/register) is the next plan |
| Persistence in this plan | `AppDbContext`, auditing interceptor (`CreatedAt`/`UpdatedAt` from `TimeProvider`), DB readiness check. First tables and migrations arrive with the first feature plan |
| Caching | Redis as a cache only, never as a source of truth. `HybridCache` provides an in-process L1 and Redis as L2 (stampede protection, tag invalidation). Queries opt in through `ICachedQuery`; commands invalidate through `IInvalidatesCache`; both are applied by MediatR behaviors, so handlers never touch the cache. Keys and tags are always scoped to the current user. If Redis is unavailable, requests still work from the database and readiness reports `Degraded`, not `Unhealthy` |
| Not in this plan | RustFS, Polly, Dapper, email: added by the plan that first needs them |

## Global Constraints

- `TreatWarningsAsErrors`, `Nullable` enable, `ImplicitUsings` enable for every project, set in `Directory.Build.props`.
- Package versions are set only in `Directory.Packages.props` (central package management). `MediatR` stays at exactly `12.5.0`.
- Dependencies flow one way: `Mita.Api → Mita.Infrastructure → Mita.Domain`. `Mita.Domain` has no project or package references.
- Code style:
  - file-scoped namespaces;
  - records for requests and responses;
  - classes `sealed` by default;
  - English only;
  - no comments except for a controversial decision;
  - every async method takes and forwards a `CancellationToken`.
- Every endpoint belongs to the v1 version group and declares authorization explicitly (`RequireAuthorization("User")` or `AllowAnonymous()`).
- Errors follow RFC 9457 ProblemDetails:
  - validation errors are `400`, with `errors: { camelCaseField: ["validation.code"] }` and title `Validation failed`;
  - not found is `404` with title `Not Found`;
  - unhandled errors are `500` with title `Server error` and no exception details.
- Lists return `{ items, page, pageSize, total }`. `page` starts at 1 and `pageSize` defaults to 20 with a maximum of 100.
- Caching is applied only in MediatR behaviors:
  - only successful results of queries implementing `ICachedQuery` are cached;
  - every cache key is `u:{userId}:{query key}`, and every entry carries the tag `user:{userId}` plus the query's own tags, also prefixed `u:{userId}:`;
  - a successful command implementing `IInvalidatesCache` removes its tags for the current user;
  - the default expiration is 5 minutes, with 1 minute in the local L1 cache.
- Every response carries an `X-Correlation-Id` header (echoed from the request if present, else a new GUID), and the same id is in every log entry of that request.
- No secrets in the repo; JWT signing key and connection string come from configuration/environment; options are validated on start.
- Commit messages: Conventional Commits; never mention Claude, AI or assistants; no `Co-Authored-By`.

## Review Focus

- A request body that is not valid JSON, or has the wrong JSON types, must return a 400 ProblemDetails, not a 500. Pinned in Task 4.
- Two users calling the same cached query must never see each other's data, even with identical parameters. Pinned in Task 9.
- When Redis is down or unreachable at startup, the app still starts, cached queries still answer from the database, and `/health/ready` reports `Degraded` with status 200. Pinned in Task 9.
- If the database is down, `/health/ready` reports 503 while `/health/live` keeps reporting 200. The app must not crash on start. Pinned in Task 7.
- An expired token, a token with a wrong signature, or a token without a `sub` claim gets 401. It never reaches a handler with an empty user id. Pinned in Task 8.

---

## File map

```
global.json                         SDK 10.0.x, rollForward latestFeature
Directory.Build.props               shared MSBuild settings
Directory.Packages.props            central package versions
.editorconfig                       C# style, analyzers as errors
Mita.slnx
src/Mita.Domain/Mita.Domain.csproj
src/Mita.Domain/Common/IAuditable.cs
src/Mita.Infrastructure/Mita.Infrastructure.csproj
src/Mita.Infrastructure/DependencyInjection.cs          AddInfrastructure(IServiceCollection, IConfiguration)
src/Mita.Infrastructure/Persistence/AppDbContext.cs
src/Mita.Infrastructure/Persistence/Interceptors/AuditingInterceptor.cs
src/Mita.Infrastructure/Authentication/JwtOptions.cs
src/Mita.Infrastructure/Caching/RedisOptions.cs, RedisHealthCheck.cs
src/Mita.Api/Common/Caching/ICachedQuery.cs, IInvalidatesCache.cs, CacheKeys.cs
src/Mita.Api/Behaviors/CachingBehavior.cs, CacheInvalidationBehavior.cs
src/Mita.Api/Mita.Api.csproj
src/Mita.Api/Program.cs
src/Mita.Api/Common/Error.cs, Result.cs, ResultHttpExtensions.cs
src/Mita.Api/Common/Paging.cs                            PageRequest, PageResponse<T>
src/Mita.Api/Behaviors/ValidationBehavior.cs, LoggingBehavior.cs
src/Mita.Api/Middleware/CorrelationIdMiddleware.cs, UnhandledExceptionHandler.cs
src/Mita.Api/Extensions/ApiVersioningExtensions.cs, OpenApiExtensions.cs, HealthExtensions.cs, AuthExtensions.cs, LoggingExtensions.cs
src/Mita.Api/Configuration/                              options validators
src/Mita.Api/Authentication/ICurrentUser.cs, CurrentUser.cs
src/Mita.Api/Features/System/Ping/PingEndpoint.cs        GET /v1/system/ping (auth smoke slice)
tests/Mita.Api.UnitTests/...
tests/Mita.Api.IntegrationTests/Infrastructure/MitaApiFactory.cs, PostgresFixture.cs, RedisFixture.cs, TestTokens.cs
docker/Dockerfile, compose.yaml (root), .dockerignore
```

All commands run in WSL through `/home/jaloqa/dock.mita-work/be-run.sh '<command>'` (sets `DOTNET_ROOT`/`PATH`, `cd ~/dock.mita.backend`).

---

### Task 1: Toolchain, solution and dependency rules

**Files:**
- Create: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `Mita.slnx`, the five `.csproj` files, `src/Mita.Domain/Common/IAuditable.cs`
- Modify: `.gitignore` (add standard .NET entries: `bin/`, `obj/`, `*.user`, `TestResults/`, `.vs/`), `CLAUDE.md` (Commands: `Mita.*` names; `dotnet test --logger junit`), `docs/STACK.md` (MediatR "12.5.0, last Apache-2.0 release — do not upgrade"; Testcontainers confirmed)
- Test: `tests/Mita.Api.UnitTests/Architecture/DependencyRulesTests.cs`

**Interfaces:**
- Produces: projects `Mita.Domain` (no refs), `Mita.Infrastructure` (→ Domain), `Mita.Api` (→ Infrastructure; Web SDK), `Mita.Api.UnitTests` (→ Api), `Mita.Api.IntegrationTests` (→ Api); `public interface IAuditable { DateTimeOffset CreatedAt { get; set; } DateTimeOffset UpdatedAt { get; set; } }` in namespace `Mita.Domain.Common`.

- [ ] **Step 1: Install the .NET 10 SDK for the user**

Run: `curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"` then `be-run.sh 'dotnet --list-sdks'`
Expected: one `10.0.x` SDK listed.

- [ ] **Step 2: Create solution, projects and shared props**

`Directory.Build.props`: `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild true`, `ManagePackageVersionsCentrally` in the packages file. Test projects: xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Shouldly, Moq, Bogus, JunitXml.TestLogger; integration tests also Microsoft.AspNetCore.Mvc.Testing and Testcontainers.PostgreSql.

- [ ] **Step 3: Write the failing dependency-rule test**

```csharp
public sealed class DependencyRulesTests
{
    [Fact]
    public void Should_HaveNoProjectReferences_When_InspectingDomain() =>
        typeof(Mita.Domain.Common.IAuditable).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name).ShouldNotContain(name => name!.StartsWith("Mita."));

    [Fact]
    public void Should_NotReferenceApi_When_InspectingInfrastructure() =>
        typeof(Mita.Infrastructure.DependencyInjection).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name).ShouldNotContain("Mita.Api");
}
```

- [ ] **Step 4: Run and see it fail, add `Mita.Infrastructure.DependencyInjection` (static class, empty `AddInfrastructure(this IServiceCollection services, IConfiguration configuration)` returning `services`), run again**

Run: `be-run.sh 'dotnet build -warnaserror && dotnet test'`
Expected: build succeeds with 0 warnings; 2 tests pass.

- [ ] **Step 5: Commit** — `chore: .NET 10 solution with vertical slice projects and dependency rules`

---

### Task 2: API host, logging and health

**Files:**
- Create: `src/Mita.Api/Program.cs`, `Extensions/LoggingExtensions.cs`, `Extensions/HealthExtensions.cs`, `appsettings.json`, `appsettings.Development.json`, `tests/Mita.Api.IntegrationTests/Infrastructure/MitaApiFactory.cs`, `tests/Mita.Api.IntegrationTests/HealthTests.cs`
- Test: `HealthTests.cs`

**Interfaces:**
- Produces:
  - `public partial class Program;` so tests can reference the entry point.
  - `MitaApiFactory : WebApplicationFactory<Program>` with environment `Testing`.
  - Health endpoints: `GET /health/live` (anonymous; checks tagged `live` only) and `GET /health/ready` (anonymous; checks tagged `ready`). Both return 200 with body `Healthy`, or 503 when a check fails.
  - `AddMitaLogging(this WebApplicationBuilder)`: Serilog reads from configuration, writes JSON (ECS format) to the console, and enriches with `CorrelationId` from the log context. The Elastic APM agent is registered only when `ElasticApm:ServerUrl` is set.

- [ ] **Step 1: Failing test** — `Should_ReturnHealthy_When_LiveEndpointCalled`: `GET /health/live` → 200, body `"Healthy"`.
- [ ] **Step 2: Run, see it fail; implement the host** (Minimal API, health checks, Serilog request logging); **run, see it pass.**
- [ ] **Step 3: Commit** — `feat: api host with structured logging and liveness check`

---

### Task 3: Correlation id and unhandled errors as ProblemDetails

**Files:**
- Create: `Middleware/CorrelationIdMiddleware.cs`, `Middleware/UnhandledExceptionHandler.cs` (`IExceptionHandler`), registration in `Program.cs`; tests `tests/Mita.Api.IntegrationTests/ErrorHandlingTests.cs`, `CorrelationIdTests.cs`
- The test-only endpoint `GET /test/throw` is mapped only when the environment is `Testing`.

**Interfaces:**
- Produces:
  - Header name constant `CorrelationIdMiddleware.HeaderName = "X-Correlation-Id"`.
  - An accepted inbound id matches `^[A-Za-z0-9-]{1,64}$`; anything else is replaced with `Guid.NewGuid().ToString()`.
  - Unhandled exception: 500 `application/problem+json` with `{ title: "Server error", status: 500, correlationId }` and no `detail` or stack trace. The exception is logged at Error level.

- [ ] **Step 1: Failing tests**
  - `Should_EchoCorrelationId_When_ClientSendsValidOne`
  - `Should_GenerateCorrelationId_When_HeaderMissing`: response header is a GUID.
  - `Should_ReplaceCorrelationId_When_ClientSendsInvalidOne`: send 200 chars, then `"a\nb"`; the response header is a GUID, not the input.
  - `Should_ReturnProblemDetails_When_HandlerThrows`: status 500, content type `application/problem+json`, `title == "Server error"`, the body does not contain `"exception"` or `"stackTrace"`, `correlationId` equals the response header.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: correlation ids and problem details for unhandled errors`

---

### Task 4: Result, errors and the validation pipeline

**Files:**
- Create: `Common/Error.cs`, `Common/Result.cs`, `Common/ResultHttpExtensions.cs`, `Behaviors/ValidationBehavior.cs`, `Behaviors/LoggingBehavior.cs`, MediatR + FluentValidation registration (`Extensions/MediatorExtensions.cs`); tests `tests/Mita.Api.UnitTests/Common/ResultHttpExtensionsTests.cs`, `Behaviors/ValidationBehaviorTests.cs`, `tests/Mita.Api.IntegrationTests/RequestBindingTests.cs`

**Interfaces:**
- Produces:
  - `public enum ErrorType { Validation, NotFound, Conflict, Unauthorized, Forbidden }`
  - `public sealed record Error(ErrorType Type, string Code, IReadOnlyDictionary<string, string[]>? Fields = null)`, with factories `Error.Validation(IReadOnlyDictionary<string,string[]> fields)`, `Error.NotFound()`, `Error.Conflict(string code)`.
  - `public sealed class Result<T>` with `IsSuccess`, `Value`, `Error`, implicit conversions from `T` and from `Error`, and `Result` (non-generic) with `Result.Success()`.
  - `IResult ToHttp<T>(this Result<T> result, Func<T, IResult> onSuccess)` maps:
    - Validation → 400 `{ title: "Validation failed", errors }`;
    - NotFound → 404 `{ title: "Not Found" }`;
    - Conflict → 409 `{ title: "Conflict", code }`;
    - Unauthorized → 401;
    - Forbidden → 403;
    - always `application/problem+json`, with `correlationId`.
  - `ValidationBehavior<TRequest, TResponse>` where `TResponse` is a `Result`/`Result<T>`: it runs every `IValidator<TRequest>`. On failure it returns `Error.Validation`, with field names in camelCase (first character lowercased, nested paths kept as `items[0].name`) and messages equal to the validator's `ErrorCode` (validators set codes like `validation.required`). It never throws for validation.
  - `LoggingBehavior`: logs the request name and elapsed milliseconds at Information, and failures at Warning with the error code.
- Request binding: when the body is malformed JSON or has the wrong JSON types, the response is 400 ProblemDetails with `title "Validation failed"` and the field `body: ["validation.invalidValue"]`. Configure this through the Minimal API binding failure handling (`BadHttpRequestException` → 400 in the exception handler).

- [ ] **Step 1: Failing unit tests**
  - `Should_MapValidationErrorTo400WithCamelCaseFields`
  - `Should_MapNotFoundTo404`
  - `Should_ReturnValidationError_When_ValidatorFails`, using a sample request `SampleCommand(string Name)` with validator `RuleFor(x => x.Name).NotEmpty().WithErrorCode("validation.required")` → `Fields["name"] == ["validation.required"]` and the handler is not called (Moq verify).
  - `Should_CallHandler_When_ValidationPasses`
- [ ] **Step 2: Failing integration test** `Should_Return400_When_BodyIsMalformedJson` against a Testing-only `POST /test/echo` taking `record EchoBody(int Value)`. Two cases: body `{`, and body `{"value":"x"}`.
- [ ] **Step 3: Implement, run all tests, see them pass.**
- [ ] **Step 4: Commit** — `feat: result type, problem details mapping and validation pipeline`

---

### Task 5: Pagination contract

**Files:**
- Create: `Common/Paging.cs`; tests `tests/Mita.Api.UnitTests/Common/PagingTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record PageRequest(int Page = 1, int PageSize = 20)` with `public const int MaxPageSize = 100;` and `int Skip => (Page - 1) * PageSize`.
  - `public sealed class PageRequestValidator : AbstractValidator<PageRequest>`: `Page >= 1` and `1 <= PageSize <= 100`, both with code `validation.invalidValue`.
  - `public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)`, which serialises as `{ items, page, pageSize, total }`.
  - `static Task<PageResponse<T>> ToPageAsync<T>(this IQueryable<T> query, PageRequest request, CancellationToken ct)`, with `Total` counted before `Skip`/`Take`.
- [ ] **Step 1: Failing tests** — `Should_RejectPage_When_ZeroOrNegative` (0, -1 → `validation.invalidValue` on `page`), `Should_RejectPageSize_When_AboveMax` (1000), `Should_SerialiseWithFrontendFieldNames` (System.Text.Json web defaults → `{"items":[],"page":1,"pageSize":20,"total":0}`), `Should_SkipPreviousPages` (page 3, size 20 → Skip 40). Non-numeric `page` in a query string is covered by the binding rule from Task 4 (add case `GET /test/page?page=x` → 400 `page: ["validation.invalidValue"]` to `RequestBindingTests`).
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: pagination contract shared with the frontend`

---

### Task 6: API versioning and OpenAPI with Scalar

**Files:**
- Create: `Extensions/ApiVersioningExtensions.cs`, `Extensions/OpenApiExtensions.cs`; tests `tests/Mita.Api.IntegrationTests/OpenApiTests.cs`

**Interfaces:**
- Produces:
  - `RouteGroupBuilder MapV1(this IEndpointRouteBuilder app)`: returns the `/v{version:apiVersion}` group with version set v1 and `RequireAuthorization("User")` applied (the policy comes in Task 8; until then register a placeholder policy named `User` requiring an authenticated user).
  - The OpenAPI document `v1` is served at `/openapi/v1.json` in every environment and is anonymous.
  - Scalar is served at `/scalar` only in Development, and an `ApiVersion`-aware document transformer sets `info.title = "Mita API"` and `version = "v1"`.
- [ ] **Step 1: Failing tests** — `Should_ServeOpenApiDocument` (200, JSON with `info.title == "Mita API"`), `Should_NotServeScalar_OutsideDevelopment` (`/scalar` → 404 in Testing).
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: api versioning and openapi document`

---

### Task 7: Persistence base and readiness against a real PostgreSQL

**Files:**
- Create: `src/Mita.Infrastructure/Persistence/AppDbContext.cs`, `Persistence/Interceptors/AuditingInterceptor.cs`, `Configuration/DatabaseOptions.cs` (+ validator), wiring in `DependencyInjection.AddInfrastructure`; `tests/Mita.Api.IntegrationTests/Infrastructure/PostgresFixture.cs`, `ReadinessTests.cs`, `AuditingTests.cs` (with a test-only `AuditedThing : IAuditable` entity in a test-only `DbContext` that registers the same interceptor)

**Interfaces:**
- Consumes: `IAuditable` (Task 1), `MitaApiFactory` (Task 2).
- Produces:
  - `public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)` applies configurations from its assembly.
  - `DatabaseOptions { string ConnectionString }` is bound from `ConnectionStrings:Mita` and validated non-empty on start.
  - The `ready` health check `postgres` uses `AddDbContextCheck<AppDbContext>` tagged `ready`.
  - `AuditingInterceptor(TimeProvider clock)`: on `SavingChanges` it sets `CreatedAt` and `UpdatedAt` for Added entities, and only `UpdatedAt` for Modified ones.
  - `PostgresFixture` (xUnit assembly fixture): starts `postgres:17-alpine` via `Testcontainers.PostgreSql`, and `MitaApiFactory` receives its connection string through configuration.
- [ ] **Step 1: Failing tests**
  - `Should_ReportReady_When_DatabaseIsUp`: `/health/ready` returns 200 with the fixture.
  - `Should_ReportNotReadyButAlive_When_DatabaseIsDown`: a factory pointed at `Host=127.0.0.1;Port=1;...` gives `/health/ready` → 503 and `/health/live` → 200, and the app starts.
  - `Should_StampCreatedAndUpdated_When_Saving` (integration, on the fixture database, with a stub `TimeProvider` returning fixed instants): insert sets `CreatedAt == UpdatedAt == t1`; after advancing the clock to `t2` and modifying, `CreatedAt == t1` and `UpdatedAt == t2`.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass** (Docker must be running).
- [ ] **Step 3: Commit** — `feat: postgres persistence base with auditing and readiness check`

---

### Task 8: Authentication skeleton and current user

**Files:**
- Create: `src/Mita.Infrastructure/Authentication/JwtOptions.cs` (+ validator), `src/Mita.Api/Extensions/AuthExtensions.cs`, `Authentication/ICurrentUser.cs`, `Authentication/CurrentUser.cs`, `Features/System/Ping/PingEndpoint.cs` (+ `SystemEndpoints.cs` with `MapSystemEndpoints`); `tests/Mita.Api.IntegrationTests/Infrastructure/TestTokens.cs`, `AuthTests.cs`

**Interfaces:**
- Produces:
  - `JwtOptions { string Issuer; string Audience; string SigningKey }`, bound from `Jwt`; the signing key must be at least 32 bytes (validated on start).
  - JWT bearer with issuer, audience, lifetime and signing key validation, `ClockSkew` 30 s.
  - The authorization policy `User` requires an authenticated user with a non-empty `sub` claim; the fallback policy is none (health and OpenAPI stay anonymous).
  - `public interface ICurrentUser { Guid Id { get; } }`, scoped, read from `sub`; a missing or invalid `sub` throws `UnauthorizedAccessException`. This cannot happen behind the `User` policy.
  - `GET /v1/system/ping` (policy `User`) returns `{ userId }`, the smoke slice proving versioning, auth and ICurrentUser.
  - `TestTokens.Create(Guid userId, DateTimeOffset? expires = null, string? signingKey = null, bool includeSub = true)` builds tokens for tests.
- [ ] **Step 1: Failing tests**
  - `Should_Return401_When_NoToken`
  - `Should_Return401_When_TokenExpired`
  - `Should_Return401_When_SignatureInvalid`
  - `Should_Return401_When_SubClaimMissing`
  - `Should_ReturnUserId_When_TokenValid`: `GET /v1/system/ping` gives `userId` equal to the token's `sub`.
  - Every 401 is `application/problem+json`, and the user id never appears empty.
- [ ] **Step 2: Run, see them fail, implement, run, see them pass.**
- [ ] **Step 3: Commit** — `feat: jwt authentication skeleton with current user`

---

### Task 9: Redis caching through the request pipeline

**Files:**
- Create: `src/Mita.Infrastructure/Caching/RedisOptions.cs` (+ validator), `Caching/RedisHealthCheck.cs`, registration in `DependencyInjection.AddInfrastructure`; `src/Mita.Api/Common/Caching/ICachedQuery.cs`, `IInvalidatesCache.cs`, `CacheKeys.cs`; `src/Mita.Api/Behaviors/CachingBehavior.cs`, `CacheInvalidationBehavior.cs`.
- Test: `tests/Mita.Api.UnitTests/Behaviors/CachingBehaviorTests.cs`, `tests/Mita.Api.IntegrationTests/Infrastructure/RedisFixture.cs`, `tests/Mita.Api.IntegrationTests/CachingTests.cs`.
- Testing-only endpoints:
  - `GET /v1/test/counter?name=` sends a cached query `CounterQuery(string Name)`. Its handler increments and returns a per-user in-memory counter, so a cache hit is visible as an unchanged number.
  - `POST /v1/test/counter/reset` sends `ResetCounterCommand`, which invalidates the tag `counter`.

**Interfaces:**
- Consumes: `ICurrentUser` (Task 8), `Result<T>` (Task 4), `MitaApiFactory` (Task 2).
- Produces:
  - **`RedisOptions { string ConnectionString }`:** bound from `ConnectionStrings:Redis`, validated non-empty. A single `IConnectionMultiplexer` is created with `AbortOnConnectFail = false`, so startup never fails because of Redis, and the same multiplexer is used by `AddStackExchangeRedisCache` (`InstanceName = "mita:"`).
  - **`AddHybridCache`:** default `Expiration = 5 min`, `LocalCacheExpiration = 1 min`.
  - **`RedisHealthCheck`:** pings the multiplexer, tagged `ready`, registered with `failureStatus: HealthStatus.Degraded`.
  - **`public interface ICachedQuery`:** members `string CacheKey { get; }`, `IReadOnlyCollection<string> CacheTags { get; }` and `TimeSpan? CacheExpiration { get; }`.
  - **`public interface IInvalidatesCache`:** member `IReadOnlyCollection<string> CacheTagsToInvalidate { get; }`.
  - **`CacheKeys`:** `static string Key(Guid userId, string key) => $"u:{userId}:{key}"`, `static string Tag(Guid userId, string tag) => $"u:{userId}:{tag}"` and `static string UserTag(Guid userId) => $"user:{userId}"`.
  - **`CachingBehavior<TRequest, TResponse>`:** for `ICachedQuery` requests only, wraps the handler in `HybridCache.GetOrCreateAsync` with the user-scoped key and tags. Failed results are returned but never stored. If the cache throws, the behavior logs a Warning and calls the handler directly.
  - **`CacheInvalidationBehavior<TRequest, TResponse>`:** for `IInvalidatesCache` requests, after a successful result calls `RemoveByTagAsync` for each user-scoped tag. Cache failures are logged and never fail the command.
  - **Pipeline order:** Logging → Validation → CacheInvalidation → Caching → handler.
  - **`RedisFixture`:** an xUnit assembly fixture that starts `redis:8.6.2-alpine` via `Testcontainers.Redis`; the factory gets `ConnectionStrings:Redis` from it.

- [ ] **Step 1: Failing unit tests** (real `HybridCache` from `services.AddHybridCache()` without an L2, handler mocked with Moq):
  - `Should_CallHandlerOnce_When_SameUserAsksTwice`
  - `Should_CallHandlerPerUser_When_DifferentUsersAskSameQuery`: two `ICurrentUser` ids, the same `CacheKey`, two handler calls, and each user gets their own value.
  - `Should_NotCache_When_ResultFailed`: first call returns `Error.NotFound()`, so the second call reaches the handler again.
  - `Should_CallHandlerAgain_When_TagInvalidated`
  - `Should_CallHandler_When_CacheThrows`: a mocked `HybridCache` that throws still returns the handler's result.
- [ ] **Step 2: Failing integration tests** (`CachingTests`, Postgres and Redis fixtures):
  - `Should_ServeFromCache_When_CounterRequestedTwice`: two GETs return the same number.
  - `Should_RecomputeAfterReset`: reset, then GET returns the next number.
  - `Should_IsolateUsers`: two tokens get independent counters.
  - `Should_StoreEntryInRedis`: after a GET, the Redis key `mita:u:{userId}:counter:<name>` exists. Check it with the fixture's `IConnectionMultiplexer` and a key-pattern scan.
  - `Should_WorkAndReportDegraded_When_RedisDown`: a factory pointed at `127.0.0.1:1` gets 200 twice from the counter GET (L1 still caches, which is acceptable), and `/health/ready` returns 200 with body `Degraded`.
- [ ] **Step 3: Run, see them fail, implement, run, see them pass** (Docker must be running).
- [ ] **Step 4: Commit** — `feat: redis-backed hybrid cache applied by the request pipeline`

---

### Task 10: Docker image and local compose

**Files:**
- Create: `docker/Dockerfile`, `.dockerignore`, `compose.yaml`, `compose.override.yaml`, `.env.example`

**Interfaces:**
- Produces:
  - **Image stages:** `build` on `mcr.microsoft.com/dotnet/sdk:10.0` (restore, then `dotnet publish src/Mita.Api -c Release -o /out`) and `runtime` on `mcr.microsoft.com/dotnet/aspnet:10.0`. Runtime runs as non-root `app`, uses `ASPNETCORE_HTTP_PORTS=8080`, exposes 8080, has `HEALTHCHECK` on `/health/live`, and uses entrypoint `dotnet Mita.Api.dll`. This follows the team pattern, without the private NuGet secret until a private package is needed.
  - **`compose.yaml`:**
    - `api` builds from `docker/Dockerfile`, maps port `8080:8080`, reads env from `.env` and depends on `postgres` and `redis` both being healthy;
    - `postgres` uses `postgres:17-alpine`, a named volume and a `pg_isready` healthcheck;
    - `redis` uses `redis:8.6.2-alpine`, a named volume `redis_data` and a `redis-cli ping` healthcheck (team pattern).
  - **`compose.override.yaml`:** `ASPNETCORE_ENVIRONMENT=Development`.
  - **`.env.example`:** `ConnectionStrings__Mita`, `ConnectionStrings__Redis`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey` placeholders.
- [ ] **Step 1: Build and run**
  1. `docker compose up -d --build`;
  2. `curl -s localhost:8080/health/ready` gives `Healthy`;
  3. `curl -s -o /dev/null -w '%{http_code}' localhost:8080/v1/system/ping` gives `401`;
  4. `docker compose stop redis`, after which `/health/ready` gives `Degraded`;
  5. `docker compose down`.
- [ ] **Step 2: Commit** — `build: docker image and compose with postgres and redis`

---

### Task 11: Pull request

- [ ] Rebase on `origin/develop` and fold any fix commits into their task commits. Run the full verification, `dotnet build -warnaserror && dotnet test --logger junit`, which must be green with Docker running. Push, then open a PR to `develop` with a plain description ("## What changed and why" / "## How to test"). Wait for `check-source-branch`, then `gh pr merge --rebase`.
