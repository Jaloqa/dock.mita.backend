# API contract: authentication, users, settings, accounts, transfers and rates

This document describes the HTTP contract of API version 1 for the frontend switch-over from mocks to the real backend. The OpenAPI document is served at `/openapi/v1.json`, and Scalar at `/scalar` in the Development environment.

All paths are prefixed with `/v1`. Request and response bodies are JSON with camelCase property names.

## Endpoints

| Method | Path | Auth | Request body | Success response | Error statuses |
| --- | --- | --- | --- | --- | --- |
| POST | `/v1/auth/register` | none | `{ name, email, password }` | 201 `User`, sets cookies | 400, 429 |
| POST | `/v1/auth/login` | none | `{ email, password }` | 200 `User`, sets cookies | 400, 401, 429 |
| POST | `/v1/auth/refresh` | `mita_rt` cookie | none | 204, sets cookies (see the refresh flow) | 401, 429 |
| POST | `/v1/auth/logout` | none | none | 204, clears cookies | 429 |
| GET | `/v1/me` | required | none | 200 `User` | 401 |
| PUT | `/v1/me` | required | `{ name, email }` | 200 `User` | 400, 401, 403, 409 |
| DELETE | `/v1/me` | required | `{ password }` | 204, clears cookies | 400, 401, 403, 429 |
| GET | `/v1/settings` | required | none | 200 `Settings` | 401 |
| PUT | `/v1/settings` | required | `Settings` | 200 `Settings` | 400, 401, 403 |
| GET | `/v1/accounts?page&pageSize` | required | none | 200 `Page<Account>` | 400, 401 |
| POST | `/v1/accounts` | required | `AccountInput` | 201 `Account`, `Location` header | 400, 401, 403 |
| PUT | `/v1/accounts/{id}` | required | `AccountInput` | 200 `Account` | 400, 401, 403, 404 |
| DELETE | `/v1/accounts/{id}` | required | none | 204 | 400, 401, 403, 404 |
| GET | `/v1/transfers?accountId&page&pageSize` | required | none | 200 `Page<Transfer>` | 400, 401 |
| POST | `/v1/transfers` | required | `TransferInput` | 201 `Transfer`, no `Location` header | 400, 401, 403, 503 |
| GET | `/v1/rates?date&from&to` | required | none | 200 `Rate[]` | 400, 401 |

"required" means a valid access token, sent either in the `mita_at` cookie or as an `Authorization: Bearer <token>` header. Cookie authentication is what the browser frontend uses. Requests authenticated with the Bearer header are exempt from the XSRF check.

Logout always answers 204 and clears the cookies, even when the session is already gone, so the frontend can call it unconditionally.

Changing the email through `PUT /v1/me` revokes every refresh token of the user and answers with fresh cookies for the calling browser. Other devices have to sign in again.

### Types

```ts
type User = {
  id: string;        // GUID
  name: string;      // 1 to 40 characters after trimming
  email: string;
  createdAt: string; // UTC ISO 8601
};

type Settings = {
  baseCurrency: 'PLN' | 'EUR' | 'USD' | 'UAH';
  locale: 'en' | 'pl' | 'ru' | 'uk';
  monthStartDay: number | 'lastWorkingDay'; // integer from 1 to 28
  theme: 'system' | 'light' | 'dark';
  notifications: {
    budgetLimit: boolean;
    dailyReminder: boolean;
    weeklyReport: boolean;
    goals: boolean;
  };
};

type Page<T> = {
  items: T[];
  page: number;      // 1-based
  pageSize: number;
  total: number;
};

type Currency = 'PLN' | 'EUR' | 'USD' | 'UAH';

type Account = {
  id: string;             // GUID
  name: string;
  type: 'card' | 'cash' | 'savings';
  currency: Currency;
  openingBalance: number; // minor units
  balance: number;        // minor units, computed by the server: openingBalance plus every transfer leg
  cardLast4: string | null;
  archived: boolean;
  inUse: boolean;         // the account has at least one transfer
  linkedGoals: number;    // always 0 until goals exist
};

type AccountInput = {
  name: string;           // 1 to 40 characters after trimming, no control characters
  type: 'card' | 'cash' | 'savings';
  currency: Currency;
  openingBalance: number; // integer minor units, absolute value up to 100000000000
  cardLast4?: string | null; // exactly 4 digits; stored only for type "card"
  archived?: boolean;        // omitted on update keeps the current value
};

type Transfer = {
  id: string;             // GUID
  date: string;           // YYYY-MM-DD
  fromAccountId: string;
  toAccountId: string;
  fromAmount: number;     // minor units of the source account currency
  toAmount: number;       // minor units of the target account currency
  rate: number;           // 1 for the same currency
  note: string | null;
};

type TransferInput = {
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  fromAccountId: string;
  toAccountId: string;
  fromAmount: number;     // integer minor units, 1 to 100000000000
  rate?: number;          // manual rate, 0 < rate <= 1000000; not used for the same currency, but still validated
  note?: string | null;   // up to 200 characters after trimming
};

type Rate = {
  date: string;           // YYYY-MM-DD, the requested date (today in UTC when omitted)
  from: Currency;
  to: Currency;
  value: number;          // units of "to" per 1 unit of "from"
};
```

Registration creates default settings. The initial locale comes from the `Accept-Language` header when it matches a supported locale, and is `en` otherwise.

Passwords must be 10 to 128 characters long.

## Cookies

| Cookie | Purpose | HttpOnly | Secure | SameSite | Path | Lifetime |
| --- | --- | --- | --- | --- | --- | --- |
| `mita_at` | access token (JWT) | yes | outside Development | Lax | `/` | 15 minutes |
| `mita_rt` | refresh token | yes | always | Lax | `/v1/auth` | 30 days |
| `XSRF-TOKEN` | XSRF token | no | outside Development | Lax | `/` | 30 days |

Because `mita_rt` is always Secure, a browser only stores it over HTTPS. Chrome and Firefox treat `http://localhost` as a secure context and accept Secure cookies from it, so local development over plain HTTP works in those browsers. Safari may refuse Secure cookies on `http://localhost`; there, use another browser or serve the API over HTTPS.

The cookies are written by register, login, refresh and by `PUT /v1/me` when the email changes. They are cleared by logout, by delete account and by a failed refresh.

Cross-origin calls from the frontend need `credentials: 'include'`, and the frontend origin must be listed in `Cors:AllowedOrigins`.

## XSRF protection

Every request that uses a cookie for authentication and an unsafe method (anything except GET, HEAD, OPTIONS and TRACE) must carry the XSRF token:

1. Read the value of the `XSRF-TOKEN` cookie. It is not HttpOnly, so scripts can read it.
2. Send the same value in the `X-XSRF-TOKEN` request header.
3. Send the request with `credentials: 'include'`.

If the header is missing or does not match the cookie, the answer is 403 with code `auth.xsrf`.

The `/v1/auth/*` endpoints do not need the header. A new `XSRF-TOKEN` value is issued on every register, login and refresh, so read the cookie again after those calls rather than caching it.

## Refresh flow

The access token lives for 15 minutes. The browser drops the `mita_at` cookie when it expires, so after that the request carries no access token at all and the answer is a plain 401 without a code. For this reason the frontend must not wait for a specific code. On ANY 401 from an endpoint outside `/v1/auth`:

1. Call `POST /v1/auth/refresh` once, with `credentials: 'include'` and no body.
2. If it answers 204, retry the original request with the current cookies, including a freshly read `X-XSRF-TOKEN`.
3. If it answers 401, the session is over. Clear local state and send the user to the login page.
4. If it answers 429, the session is still valid and the client is only rate limited. Wait the number of seconds in the `Retry-After` header and call refresh again. Never treat a 429 from refresh as a logout.

A 204 that carries no `Set-Cookie` header means another tab rotated the refresh token a moment ago and this call arrived within the 30 second grace period. Nothing is wrong: the other tab has already stored the new cookies, so retry the original request with the cookies the browser currently holds.

Single-flight the refresh. Keep one shared promise for the refresh call in the frontend, and make every request that fails with a 401 while it is running wait for that promise instead of starting its own. Never refresh more than once for the same failed request: if the retry answers 401 again, treat the user as logged out.

Do not refresh on 401 answers from `/v1/auth/*` endpoints. A 401 with `auth.invalidCredentials` comes from login and means a wrong email or password.

`auth.sessionExpired` is returned when:

- the access token was sent and has expired (for example as a Bearer header; a browser normally no longer sends the expired cookie, which gives the plain 401 above),
- the access token was revoked by logout, an email change or account deletion, even if it has not expired yet,
- the refresh token is missing, invalid, expired or already rotated outside the grace period (from `/v1/auth/refresh`).

When both an `Authorization` header and the `mita_at` cookie are present, the header takes precedence and the cookie is ignored.

## Errors

Every error is an RFC 9457 `application/problem+json` document:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "errors": { "email": ["validation.invalidEmail"] },
  "traceId": "00-046c478135c08ba5cdf6a20883e509bc-42e494109944b4ec-00",
  "correlationId": "3e369314-c9cd-4008-af8e-f57805893729"
}
```

- `status`, `title`, `traceId` and `correlationId` are present on every error response. `correlationId` matches the `X-Correlation-Id` response header, and `traceId` identifies the request in the logs. Quote both when reporting a problem.
- Only validation errors (400) have an `errors` object that maps a camelCase field name to an array holding exactly one code. The frontend maps these codes to translated messages. A body that cannot be parsed reports `validation.invalidValue` under the field `body`.
- Errors with a stable meaning (401, 403 and 409 listed below) carry a machine-readable `code` property. Other statuses have only `status` and `title`.

### Validation codes (400, under `errors.<field>`)

| Code | Meaning |
| --- | --- |
| `validation.required` | the field is missing, null or blank |
| `validation.invalidEmail` | the email is not a valid address. On login it is returned only when the email contains a control character |
| `validation.emailTaken` | register, or `PUT /v1/me`: another account uses this email |
| `validation.nameTooLong` | the name is longer than 40 characters after trimming |
| `validation.passwordTooShort` | register: fewer than 10 characters |
| `validation.passwordTooLong` | register: more than 128 characters |
| `validation.invalidPassword` | `DELETE /v1/me`: the password is wrong, or the account is locked after repeated failures (field `password`) |
| `validation.invalidValue` | the value is of the wrong type or outside the allowed set, for example an unknown currency, a `monthStartDay` outside 1 to 28 or a name containing a control character. On accounts and transfers it also covers an amount that is not an integer or is above 100000000000 in absolute value, a `date` that is not a real `YYYY-MM-DD`, a manual `rate` that is not a JSON number, is not greater than 0 or is above 1000000, a `note` that is not a string or is longer than 200 characters, and a converted transfer amount above 100000000000 (reported under `rate`). In queries it is returned under the parameter name for an invalid `page`, `pageSize`, `accountId`, `date`, `from` or `to` |
| `validation.currencyLocked` | `PUT /v1/accounts/{id}`: the currency of an account that is in use cannot change (field `currency`) |
| `validation.cardLast4` | `cardLast4` is present, not blank and not exactly 4 digits. Checked for every account type |
| `validation.accountInUse` | `DELETE /v1/accounts/{id}`: the account has transfers, so it can only be archived (field `id`) |
| `validation.dateTooFar` | transfer `date` is later than today plus one year |
| `validation.unknownAccount` | transfer `fromAccountId` or `toAccountId` is not one of the user's accounts |
| `validation.archivedAccount` | transfer `fromAccountId` or `toAccountId` is an archived account |
| `validation.sameAccount` | transfer `toAccountId` equals `fromAccountId` |
| `validation.positiveAmount` | transfer `fromAmount` is 0 or negative, or the converted amount rounds to 0 (reported under `rate`) |

### Other error codes

| Status | Code or title | Where | Meaning |
| --- | --- | --- | --- |
| 401 | `auth.invalidCredentials` | login | wrong email or password, unknown account, or a locked account. The cases are deliberately indistinguishable |
| 401 | `auth.sessionExpired` | any protected endpoint, and refresh | the access token expired or was revoked, or the refresh token is missing, invalid or expired. See the refresh flow |
| 401 | title "Unauthorized", no code | any protected endpoint | no credentials (including an expired `mita_at` cookie the browser dropped), or an invalid token. Treat it like any 401 and try the refresh flow |
| 403 | `auth.xsrf` | unsafe cookie-authenticated requests | the `X-XSRF-TOKEN` header is missing or does not match the cookie |
| 403 | title "Forbidden", no code | protected endpoints | authenticated but not allowed |
| 404 | title "Not Found" | any | the resource or route does not exist |
| 409 | `user.concurrentUpdate` | `PUT /v1/me` | the account was changed by another request at the same time. Reload and retry |
| 429 | title "Too many requests" | `/v1/auth/*` and `DELETE /v1/me` | rate limit exceeded. The `Retry-After` header holds the wait in seconds. On refresh it does not mean the session ended: wait and retry |
| 500 | title "Server error" | any | unexpected failure. Report the `correlationId` |
| 503 | `rates.unavailable` (title "Service Unavailable") | `POST /v1/transfers` | a cross-currency transfer without a manual `rate` while no exchange rates are stored yet. Retry later or send a manual `rate` |

### Rate limiting and lockout

Requests are counted per client IP address per minute, in two separate buckets:

- `POST /v1/auth/register`, `POST /v1/auth/login` and `DELETE /v1/me` share a tight limit against password guessing (`RateLimiting:AuthPermitLimit`, 10 by default).
- `POST /v1/auth/refresh` and `POST /v1/auth/logout` have their own, larger limit (`RateLimiting:SessionPermitLimit`, 60 by default), so failed logins never block a refresh.

A 429 from refresh means "wait for `Retry-After` seconds and retry", never "logged out".

After 5 failed password attempts the account is locked for 15 minutes. Failures are counted together on login and on `DELETE /v1/me`. A locked account answers like a wrong password.

## Accounts, transfers and rates

All amounts are integers in minor units (grosze, cents). The absolute value of an amount is at most 100000000000. JSON numbers with a zero fraction, such as `5.0`, count as integers. Every account and transfer belongs to the current user, and an id of another user answers exactly like an unknown id (404).

Validation reports the first failing rule for each field and returns all failing fields in one 400. A body field of the wrong JSON type gives a field error, for example `openingBalance: ["validation.invalidValue"]`, not an error under `body`. A whitespace-only string counts as missing (`validation.required`).

Validation runs before the lookup. An invalid body sent to an unknown account id answers 400, and only a valid body gives 404 for an unknown or foreign id.

### Accounts

- `GET /v1/accounts`: `pageSize` defaults to 100 (maximum 100). Accounts are ordered by creation time.
- `balance` is `openingBalance` plus the sum of the account's transfer legs, computed by the server. `inUse` is true when the account has any transfer. `linkedGoals` is always 0 for now.
- `DELETE` removes an account that has no history. An account in use answers 400 `id: ["validation.accountInUse"]`: archive it with `PUT` and `archived: true` instead. Deleting a missing id answers 404.
- `PUT` replaces the editable fields. The currency of an account in use cannot change (`validation.currencyLocked`).
- `cardLast4` is validated for every type but stored only for `card`; other types answer `null`.

### Transfers

- `GET /v1/transfers`: `pageSize` defaults to 20 (maximum 100). `accountId` matches transfers on either side. A value that is not a GUID answers 400 `accountId: ["validation.invalidValue"]`. Transfers are ordered newest first (`date`, then creation time).
- Transfers cannot be edited or deleted.
- For accounts in the same currency the `rate` is 1 and `toAmount` equals `fromAmount`. Otherwise the server uses the manual `rate` when sent, and the exchange rate of the transfer `date` when not. `toAmount` is `fromAmount` times the rate, rounded to a whole minor unit (half away from zero). A manual rate is rounded to 6 decimals first.
- A manual `rate` must be a JSON number with `0 < rate <= 1000000`. A converted amount that rounds to 0 answers `rate: ["validation.positiveAmount"]`. A converted amount above 100000000000 answers `rate: ["validation.invalidValue"]`.
- A transfer stores its `rate` and `toAmount`. They never change when exchange rates change later.
- Without a manual rate and without any stored exchange rates the answer is 503 with code `rates.unavailable`.

### Rates

- Rates come from the National Bank of Poland (NBP, table A mid rates) for PLN, EUR, USD and UAH. A background job in the API loads them on start and every 6 hours, and requests never call NBP. The first run backfills about 183 days. If NBP is unreachable, the API keeps serving the stored rates.
- `GET /v1/rates` returns a plain array, not a `Page`. This is the one exception to "every collection is paginated": there are at most 12 pairs.
- `date`, `from` and `to` are all optional. An omitted `date` means today's UTC date, and every `Rate` echoes the requested or resolved date in `date`. Without `from` and `to` every currency pair is returned, never a pair with `from == to`.
- For a date, the rate of each currency is the latest stored one on or before that date. For weekends, holidays and future dates that is the latest earlier rate, and for a date before the history start it is the earliest stored rate. Cross rates go through PLN and are rounded to 6 decimals.
- Pairs whose currencies have no stored rate are omitted, so the array can be empty right after the first start.
- The response is cached for up to an hour and is the same for every user.
