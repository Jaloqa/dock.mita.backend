# Technology stack

Agreed stack for dock.mita (frontend, backend, infrastructure). Items marked TBD are not final yet.

## Infrastructure

| Technology | Purpose |
|---|---|
| GitOps | Git is the single source of truth for cluster state and deployments |
| Kubernetes | Container orchestration: deployment, scaling, availability |
| Argo CD | GitOps operator: syncs the cluster with Git, rolling updates, automatic rollback |
| Redis | In-memory cache |
| RustFS | S3-compatible object storage written in Rust (MinIO alternative) |
| Docker | Container images and isolated environments |
| PostHog | Product analytics: events, funnels, session replay, feature flags |
| Google Analytics | Web analytics: traffic, behaviour, conversions |

## Frontend

| Technology | Purpose |
|---|---|
| Nuxt 4.x | Vue meta-framework, base of the whole frontend |
| TanStack Query | HTTP data layer: fetching, caching, synchronisation, invalidation |
| TanStack Table | Headless tables and data grids |
| Nuxt file-based routing | Routes defined by `pages/` |
| Rspack | Rust bundler (Nuxt Rspack builder), faster builds and HMR |
| Bun | JavaScript runtime and package manager for development |
| TypeScript | Static typing |
| Biome | Linter and formatter, replaces ESLint and Prettier |
| Tailwind CSS | Utility-first CSS |
| PrimeVue | UI component library: tables, dialogs, forms, charts, calendars |
| ShadCN Vue (TBD) | Headless Radix-based components copied into the project |
| Nuxt i18n | Internationalisation, localised routes and content |
| Vuelidate | Form validation with composables |
| ENV | Configuration through Nuxt `runtimeConfig` and `.env` files |

## Backend

| Technology | Purpose |
|---|---|
| .NET 10 | Runtime (LTS), Linux containers |
| Serilog + Elastic APM | Structured logging, distributed tracing, metrics, query profiling |
| Dapper | Micro-ORM for complex queries, alongside EF Core |
| Minimal APIs | HTTP endpoints without controllers, fits Vertical Slice |
| MediatR (TBD) | Mediator pattern, CQRS, pipeline behaviors |
| FluentValidation | Validation rules in separate classes |
| Vertical Slice | Each feature owns endpoint, handler, model and validation |
| PostgreSQL | Relational database (replaces Oracle) |
| Pagination | Cursor-based or offset pagination for list endpoints |
| Polly | Resilience for outgoing HTTP: retry, circuit breaker, timeout, bulkhead |
| MS Identity + JWT | Authentication with JWT; sticky sessions for routing to cluster nodes |
| API versioning | Parallel v1/v2 without breaking clients (`Asp.Versioning`) |
| RBAC | Role-based access control |
| EF Core | ORM for CRUD and schema migrations |
| Swagger / OpenAPI | API specification generated from code and API explorer |
| ENV | Configuration through `appsettings` and environment variables, secrets outside the code |

## Testing

| Technology | Purpose |
|---|---|
| xUnit | Unit and integration tests for .NET |
| Playwright | End-to-end UI tests, cross-browser, screenshot comparison |
| Testcontainers (TBD) | Real PostgreSQL and Redis in Docker for integration tests |
| Moq | Mocks in .NET tests |
| Bogus | Realistic fake test data |
| JUnit XML | Test report format for CI |
| Shouldly | Assertions (free FluentAssertions alternative) |
