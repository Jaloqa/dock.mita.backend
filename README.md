# dock.mita.backend

Backend of dock.mita: ASP.NET Core Minimal APIs on .NET 10 with PostgreSQL and Redis. See [docs/STACK.md](docs/STACK.md) for the stack.

## Run locally

Requirements: Docker with Compose.

```
cp .env.example .env
docker compose up -d --build
```

The API listens on `http://localhost:8080`. `compose.override.yaml` is picked up automatically and sets `ASPNETCORE_ENVIRONMENT=Development`, so the database migrations are applied on start and Scalar is served.

Stop and remove the containers and volumes with `docker compose down -v`.

## Register and call the API through Scalar

1. Open `http://localhost:8080/scalar`. Scalar is served only in the Development environment, which the compose setup uses.
2. Send `POST /v1/auth/register` with a body like `{ "name": "Ada", "email": "ada@example.com", "password": "a-long-password" }`. The password needs at least 10 characters. The answer is 201 and the session cookies are stored by the browser.
3. Send `GET /v1/me` to check the session.
4. For `PUT`, `PATCH`, `POST` and `DELETE` calls outside the `/v1/auth` group, add the header `X-XSRF-TOKEN` with the value of the `XSRF-TOKEN` cookie.

Scalar sends requests from its own page, so cookies work without extra setup. Alternatively, copy the access token and use it as a Bearer token; no XSRF header is needed in that case.

The frontend integration rules (cookies, XSRF, refresh flow, error codes) are in [docs/api-contract.md](docs/api-contract.md).

## Configuration

Configuration comes from `appsettings.json`, environment variables and user secrets. In environment variables, replace `:` with `__` and use an index for array items. Options are validated on start, so a bad value stops the application with a clear message.

| Key | Purpose | Default |
| --- | --- | --- |
| `ConnectionStrings:Mita` | PostgreSQL connection string | none, required |
| `ConnectionStrings:Redis` | Redis connection string | none, required |
| `Jwt:Issuer`, `Jwt:Audience` | token issuer and audience | none, required |
| `Jwt:SigningKey` | HMAC signing key, at least 32 bytes. Use a secret in deployed environments | none, required |
| `Database:ApplyMigrationsOnStartup` | apply EF Core migrations when the app starts | `false`; `true` in Development and in compose |
| `Cors:AllowedOrigins` | allowed frontend origins, for example `Cors__AllowedOrigins__0=http://localhost:3000`. Origins have no trailing slash or path | empty, which allows no cross-origin calls |
| `RateLimiting:AuthPermitLimit` | requests per minute and client IP for register, login and `DELETE /v1/me` | `10` |
| `RateLimiting:SessionPermitLimit` | requests per minute and client IP for `/v1/auth/refresh` and `/v1/auth/logout`, counted separately from the limit above. A 429 on refresh means "wait for `Retry-After` and retry", not "logged out" | `60` |
| `Rates:SyncEnabled` | load exchange rates from NBP in the background. Set `Rates__SyncEnabled=false` for offline work: no request leaves the app, and cross-currency transfers then need a manual `rate` | `true` |
| `Rates:SyncInterval` | time between syncs, at least one minute | `06:00:00` |
| `Rates:HistoryDays` | days of history loaded on the first sync, 1 to 3650 | `183` |
| `Rates:NbpBaseUrl` | NBP API base URL, absolute https | `https://api.nbp.pl/api/` |
| `ForwardedHeaders:KnownProxies` | IP addresses of reverse proxies whose `X-Forwarded-For` and `X-Forwarded-Proto` are trusted | empty, which keeps the ASP.NET Core loopback-only default |
| `ForwardedHeaders:KnownNetworks` | CIDR networks of trusted reverse proxies | empty, as above |

Behind an ingress or any other reverse proxy, set `ForwardedHeaders__KnownNetworks__0` (for example `10.0.0.0/8`, the pod network of the cluster) or `ForwardedHeaders__KnownProxies__0`. Without it the forwarded headers are ignored: every request appears to come from the proxy address, so the rate limit counts all clients together, and the scheme stays `http`, which breaks the Secure cookie handling.

## Commands

```
dotnet build
dotnet test
dotnet run --project src/Mita.Api
```

Integration tests start PostgreSQL and Redis with Testcontainers, so Docker must be running.
