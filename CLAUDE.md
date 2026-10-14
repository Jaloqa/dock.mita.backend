# CLAUDE.md

Backend of dock.mita. Guidance for working in this repository.

## Stack

- .NET 10, C#, nullable reference types enabled
- ASP.NET Core Minimal APIs
- Mediator (martinothamar/Mediator, source-generated) for commands, queries and notifications
- EF Core with PostgreSQL (Npgsql)
- FluentValidation
- Scalar for API documentation (OpenAPI via `Microsoft.AspNetCore.OpenApi`)
- Serilog for logging, OpenTelemetry for traces and metrics
- Testing: xUnit, Testcontainers (PostgreSQL), Shouldly, NSubstitute
- Docker and docker compose for local development and deployment
- Central package management (`Directory.Packages.props`), `Directory.Build.props` for shared settings

## Architecture: Vertical Slice

Code is organised by feature, not by technical layer. Each use case is a slice that owns everything it needs.

```
src/
  Mita.Api/                  # host: HTTP, features, pipeline
    Program.cs
    Features/
      <Feature>/
        <UseCase>/
          <UseCase>Endpoint.cs
          <UseCase>Command.cs | <UseCase>Query.cs
          <UseCase>Handler.cs
          <UseCase>Validator.cs
          <UseCase>Response.cs
        <Feature>Endpoints.cs    # Map<Feature>Endpoints extension
    Middleware/                  # exception handling, correlation id, request logging
    Behaviors/                   # Mediator pipeline: validation, logging, transaction
    Extensions/                  # service and endpoint registration (AddX / MapX)
    Configuration/               # options classes and their validators
    Common/                      # shared API contracts: Result, pagination, ProblemDetails mapping
  Mita.Domain/               # entities, value objects, enums, domain events, no dependencies
  Mita.Infrastructure/       # everything that talks to the outside world
    Persistence/
      AppDbContext.cs
      Configurations/            # IEntityTypeConfiguration<T>
      Migrations/
      Interceptors/              # auditing, soft delete
    Authentication/
    Email/
    Storage/
    Caching/
    DependencyInjection.cs       # AddInfrastructure extension
tests/
  Mita.Api.UnitTests/
  Mita.Api.IntegrationTests/
docker/                          # Dockerfile, compose files
```

Dependencies flow one way: `Api` -> `Infrastructure` -> `Domain`. `Domain` references nothing. Business logic lives in handlers and domain entities, not in Infrastructure.

Rules:

- A slice must not reference another slice's internals. Share through `Common/` or through the domain entities only.
- No generic repositories and no service layer wrappers. Handlers use `AppDbContext` directly.
- Infrastructure holds technical concerns only (database, auth, email, storage, caching). Add a folder there per external dependency.
- Do not extract shared abstractions until the same code appears in three slices.
- Endpoints are thin: map the request to a command or query, send it through `IMediator`, map the result to an HTTP response.
- Each feature exposes a `Map<Feature>Endpoints(this IEndpointRouteBuilder)` extension and is registered in `Program.cs`.
- Validation runs in a Mediator pipeline behavior using FluentValidation, never inside handlers.
- Errors are returned as RFC 9457 `ProblemDetails`. Use a result type for expected failures and exceptions only for unexpected ones.
- Queries use `AsNoTracking` and project to response DTOs with `Select`. Never expose entities from the API.
- Every query that returns a collection is paginated.
- Pass `CancellationToken` through every async call.
- Database changes go through EF Core migrations. Never edit an applied migration.

## Code style

- All code, identifiers, commit messages and comments are in English.
- Do not write comments. Add one only for a genuinely non-obvious or controversial decision, and explain why, not what.
- Prefer clear naming over explanation.
- File-scoped namespaces, primary constructors where they fit, records for commands, queries and responses.
- `sealed` by default for classes.
- Async methods are suffixed with `Async` only when a synchronous counterpart exists.
- Respect `.editorconfig`. Build must pass with warnings as errors.

## Git workflow

- Branches: `prod` (production), `preprod`, `develop`. Work happens in `feature/*`, `bugfix/*`, `hotfix/*` branches.
- Flow: `feature/*` -> `develop` -> `preprod` -> `prod`. Hotfixes go to `prod` and are back-merged into `develop`.
- Everything reaches the long-lived branches through pull requests.
- Use Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `chore:`, `docs:`.
- Never mention Claude, AI or any assistant in commit messages, PR titles or descriptions, code comments, or any other artifact. No `Co-Authored-By` trailers, no "Generated with" lines.

## Commands

```
dotnet build
dotnet test
dotnet run --project src/DockMita.Api
dotnet ef migrations add <Name> --project src/DockMita.Infrastructure --startup-project src/DockMita.Api
dotnet ef database update --project src/DockMita.Infrastructure --startup-project src/DockMita.Api
docker compose up -d
```

## Testing

- Unit-test validators and non-trivial handler logic.
- Integration-test each endpoint against a real PostgreSQL started by Testcontainers, using `WebApplicationFactory`. Do not use the EF in-memory provider.
- Test names describe behaviour: `Should_ReturnNotFound_When_OrderDoesNotExist`.

## Configuration and secrets

- Configuration through `appsettings.json`, environment variables and the Options pattern with validation on start.
- Never commit secrets. Use user secrets locally and environment variables in deployed environments.
