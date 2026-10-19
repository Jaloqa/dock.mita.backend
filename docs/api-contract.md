# API contract: authentication, users, settings, accounts, transfers, rates, categories, transactions, budgets and goals

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
| PUT | `/v1/settings` | required | `Settings` | 200 `Settings` | 400, 401, 403, 503 |
| GET | `/v1/accounts?page&pageSize` | required | none | 200 `Page<Account>` | 400, 401 |
| POST | `/v1/accounts` | required | `AccountInput` | 201 `Account`, `Location` header | 400, 401, 403 |
| PUT | `/v1/accounts/{id}` | required | `AccountInput` | 200 `Account` | 400, 401, 403, 404 |
| DELETE | `/v1/accounts/{id}` | required | none | 204 | 400, 401, 403, 404 |
| GET | `/v1/transfers?accountId&page&pageSize` | required | none | 200 `Page<Transfer>` | 400, 401 |
| POST | `/v1/transfers` | required | `TransferInput` | 201 `Transfer`, no `Location` header | 400, 401, 403, 503 |
| GET | `/v1/rates?date&from&to` | required | none | 200 `Rate[]` | 400, 401 |
| GET | `/v1/categories?kind&page&pageSize` | required | none | 200 `Page<Category>` | 400, 401 |
| POST | `/v1/categories` | required | `CategoryInput` | 201 `Category`, `Location` header | 400, 401, 403 |
| PUT | `/v1/categories/{id}` | required | `CategoryInput` | 200 `Category` | 400, 401, 403, 404 |
| DELETE | `/v1/categories/{id}` | required | none | 204 | 400, 401, 403, 404, 503 |
| GET | `/v1/transactions?search&categoryId&accountId&kind&from&to&sort&page&pageSize` | required | none | 200 `TransactionPage` | 400, 401 |
| GET | `/v1/transactions/{id}` | required | none | 200 `Transaction` | 401, 404 |
| POST | `/v1/transactions` | required | `TransactionInput` | 201 `Transaction`, `Location` header | 400, 401, 403, 503 |
| PUT | `/v1/transactions/{id}` | required | `TransactionInput` | 200 `Transaction` | 400, 401, 403, 404, 503 |
| DELETE | `/v1/transactions/{id}` | required | none | 204 | 401, 403, 404 |
| GET | `/v1/budgets?period&page&pageSize` | required | none | 200 `BudgetPage` | 400, 401, 503 |
| POST | `/v1/budgets` | required | `BudgetInput` | 201 `Budget`, `Location` header | 400, 401, 403, 503 |
| PUT | `/v1/budgets/{id}` | required | `BudgetInput` | 200 `Budget` | 400, 401, 403, 404, 503 |
| DELETE | `/v1/budgets/{id}` | required | none | 204 | 401, 403, 404 |
| POST | `/v1/budgets/copy` | required | `BudgetCopyInput` | 200 `BudgetCopyResult` | 400, 401, 403 |
| GET | `/v1/goals?page&pageSize` | required | none | 200 `GoalPage` | 400, 401, 503 |
| POST | `/v1/goals` | required | `GoalInput` | 201 `Goal`, `Location` header | 400, 401, 403 |
| PUT | `/v1/goals/{id}` | required | `GoalInput` | 200 `Goal` | 400, 401, 403, 404, 503 |
| DELETE | `/v1/goals/{id}` | required | none | 204 | 401, 403, 404 |
| POST | `/v1/goals/{id}/contributions` | required | `GoalContributionInput` | 200 `Goal`, no `Location` header | 400, 401, 403, 404, 409, 503 |

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
  balance: number;        // minor units, computed by the server: openingBalance plus every transfer leg plus every transaction (income adds, expense subtracts, in the account currency)
  cardLast4: string | null;
  archived: boolean;
  inUse: boolean;         // the account has at least one transfer or transaction
  linkedGoals: number;    // the number of the user's goals linked to this account
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

type CategoryKind = 'expense' | 'income';

type Category = {
  id: string;             // GUID
  name: string;
  kind: CategoryKind;
  color: 'chart-1' | 'chart-2' | 'chart-3' | 'chart-4' | 'chart-5' | 'chart-6';
  system: 'transfer' | 'other' | null;
  inUse: boolean;         // the category has at least one transaction or budget
};

type CategoryInput = {
  name: string;           // 1 to 40 characters after trimming, no control characters, unique per kind
  kind: CategoryKind;
  color: 'chart-1' | 'chart-2' | 'chart-3' | 'chart-4' | 'chart-5' | 'chart-6';
};

type Transaction = {
  id: string;             // GUID
  kind: CategoryKind;
  date: string;           // YYYY-MM-DD
  title: string;          // "" when none
  note: string | null;
  categoryId: string;
  accountId: string;
  amount: number;         // minor units of the entered currency
  currency: Currency;     // the entered currency
  accountAmount: number;  // minor units of the account currency
  rate: number;           // entered currency to account currency, 1 for the same currency
  baseAmount: number;     // minor units of the current base currency
};

type TransactionInput = {
  kind: CategoryKind;
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  title?: string;         // up to 100 characters after trimming
  note?: string | null;   // up to 200 characters after trimming
  categoryId: string;
  accountId: string;
  amount: number;         // integer minor units, 1 to 100000000000
  currency: Currency;     // any supported currency, converted into the account currency
  rate?: number;          // manual rate, 0 < rate <= 1000000; not used for the same currency, but still validated
};

type TransactionPage = Page<Transaction> & {
  sum: number;            // minor units of the base currency, over every filtered row, not only the page
  currency: Currency;     // the current base currency
};

type Budget = {
  id: string;             // GUID
  categoryId: string;
  period: string;         // YYYY-MM
  limit: number;          // minor units of the current base currency
  spent: number;          // minor units of the base currency: expense transactions of the category in the period
  remaining: number;      // limit - spent, negative when over the limit
  status: 'on_track' | 'almost_spent' | 'over_budget';
  perDay: number | null;  // floor(remaining / daysLeft), null when remaining or the days left are not positive
};

type BudgetInput = {
  categoryId: string;     // an expense category of the user, not the transfer category
  period: string;         // YYYY-MM, from 1970-01 to 2099-12
  limit: number;          // integer minor units of the base currency, 1 to 100000000000
};

type BudgetPage = Page<Budget> & {
  period: string;         // YYYY-MM, the period shown
  from: string;           // YYYY-MM-DD, first day of the period
  to: string;             // YYYY-MM-DD, last day of the period
  currency: Currency;     // the current base currency
  summary: {
    planned: number;      // sum of limit over every budget of the period, not only the page
    spent: number;
    left: number;         // planned - spent
    daysLeft: number;     // 0 after the period ended
  };
};

type BudgetCopyInput = {
  period: string;         // YYYY-MM, the target period; budgets come from the previous one
};

type BudgetCopyResult = {
  copied: number;         // at least 1
};

type Goal = {
  id: string;             // GUID
  name: string;
  target: number;         // minor units of the current base currency
  saved: number;          // minor units of the current base currency
  remaining: number;      // max(target - saved, 0)
  percent: number;        // floor(saved * 100 / target), may exceed 100
  monthly: number | null; // amount to save per month to reach the target by the deadline, null when not applicable
  status: 'active' | 'reached' | 'overdue';
  deadline: string | null; // YYYY-MM
  accountId: string | null; // the linked savings account
  currency: Currency;     // the current base currency
};

type GoalInput = {
  name: string;           // 1 to 40 characters after trimming, no control characters
  target: number;         // integer minor units of the base currency, 1 to 100000000000
  deadline?: string | null;  // YYYY-MM, from the current month to 600 months ahead
  accountId?: string | null; // a non-archived savings account of the user
};

type GoalPage = Page<Goal> & {
  currency: Currency;     // the current base currency
  summary: {
    saved: number;        // sum of saved over every goal, not only the page
    monthly: number;      // average saved per month over the last 3 months, in the base currency
    active: number;       // number of goals that are not reached
  };
};

type GoalContributionInput = {
  amount: number;         // integer minor units, 1 to 100000000000
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  fromAccountId?: string; // required when the goal is linked to an account
  rate?: number;          // manual rate, 0 < rate <= 1000000; used only for a linked goal in another currency, but still validated
};
```

Registration creates default settings and the starter categories (see Categories). The initial locale comes from the `Accept-Language` header when it matches a supported locale, and is `en` otherwise.

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
| `validation.required` | the field is missing, null or blank. Goal `deadline` and `accountId` and contribution `rate` are optional, and `fromAccountId` is required only for a linked goal |
| `validation.invalidEmail` | the email is not a valid address. On login it is returned only when the email contains a control character |
| `validation.emailTaken` | register, or `PUT /v1/me`: another account uses this email |
| `validation.nameTooLong` | the name is longer than 40 characters after trimming (users, accounts, categories and goals) |
| `validation.passwordTooShort` | register: fewer than 10 characters |
| `validation.passwordTooLong` | register: more than 128 characters |
| `validation.invalidPassword` | `DELETE /v1/me`: the password is wrong, or the account is locked after repeated failures (field `password`) |
| `validation.invalidValue` | the value is of the wrong type or outside the allowed set, for example an unknown currency, a `monthStartDay` outside 1 to 28 or a name containing a control character. On accounts and transfers it also covers an amount that is not an integer or is above 100000000000 in absolute value, a `date` that is not a real `YYYY-MM-DD`, a manual `rate` that is not a JSON number, is not greater than 0 or is above 1000000, a `note` that is not a string or is longer than 200 characters, and a converted transfer amount above 100000000000 (reported under `rate`). On categories it covers an unknown `kind` or `color`. On transactions it covers an unknown `kind` or `currency`, a `title` or `note` that is not a string, and the same rules for `amount`, `date`, `rate` and the converted amount as on transfers. On budgets it covers a `period` that is not a `YYYY-MM` key between 1970-01 and 2099-12 and a `limit` that is not an integer or is above 100000000000. On goals it covers a `name` that is not a string or contains a control character, a `target` that is not an integer or is above 100000000000, and a `deadline` that is not a `YYYY-MM` key or is more than 600 months after the current month. On contributions it covers the same rules for `amount`, `date` and `rate` as on transfers. In queries it is returned under the parameter name for an invalid `page`, `pageSize`, `accountId`, `categoryId`, `date`, `from`, `to`, `kind`, `sort` or budget `period` |
| `validation.currencyLocked` | `PUT /v1/accounts/{id}`: the currency of an account that is in use cannot change (field `currency`) |
| `validation.cardLast4` | `cardLast4` is present, not blank and not exactly 4 digits. Checked for every account type |
| `validation.accountInUse` | `DELETE /v1/accounts/{id}`: the account has transfers or transactions, so it can only be archived (field `id`) |
| `validation.dateTooFar` | transfer, transaction or goal contribution `date` is later than today plus one year |
| `validation.unknownAccount` | transfer `fromAccountId` or `toAccountId`, transaction `accountId`, goal `accountId` or contribution `fromAccountId` is not one of the user's accounts (or not a GUID) |
| `validation.archivedAccount` | transfer `fromAccountId` or `toAccountId`, transaction `accountId`, goal `accountId` or contribution `fromAccountId` is an archived account. A contribution to a goal whose linked account is archived answers it under `accountId`. On `PUT /v1/transactions/{id}` the account the transaction already has is allowed even when archived, and `PUT /v1/goals/{id}` may keep the account the goal is already linked to |
| `validation.sameAccount` | transfer `toAccountId` equals `fromAccountId`, or a contribution `fromAccountId` equals the account the goal is linked to |
| `validation.positiveAmount` | transfer `fromAmount`, transaction `amount`, budget `limit`, goal `target` or contribution `amount` is 0 or negative, or the converted amount rounds to 0 (reported under `rate`) |
| `validation.categoryNameTaken` | `POST`/`PUT /v1/categories`: another category of the same kind has this name, compared case-insensitively after trimming (field `name`) |
| `validation.categoryKindLocked` | `PUT /v1/categories/{id}`: the kind of a system category, or of a category that is in use, cannot change (field `kind`) |
| `validation.systemCategory` | `DELETE /v1/categories/{id}`: a system category cannot be deleted (field `id`) |
| `validation.unknownCategory` | transaction or budget `categoryId` is not one of the user's categories, is not a GUID, or is the `transfer` category. Also returned when the category was deleted at the same moment |
| `validation.categoryKindMismatch` | transaction `categoryId` has a different kind than the transaction `kind`. Checked only when `kind` itself is valid |
| `validation.titleTooLong` | transaction `title` is longer than 100 characters after trimming |
| `validation.noteTooLong` | transaction `note` is longer than 200 characters after trimming (transfers report this as `validation.invalidValue`) |
| `validation.expenseCategoryOnly` | `POST`/`PUT /v1/budgets`: `categoryId` is an income category (field `categoryId`) |
| `validation.budgetExists` | `POST`/`PUT /v1/budgets`: the category already has a budget in this period. Also returned when another request created it at the same moment. Checked only when `period` is valid (field `categoryId`) |
| `validation.nothingToCopy` | `POST /v1/budgets/copy`: the previous period has no budget to copy, or every category of it already has a budget in the target period (field `period`) |
| `validation.savingsAccountOnly` | `POST`/`PUT /v1/goals`: `accountId` is not a savings account (field `accountId`). Not checked when `PUT` keeps the account the goal is already linked to |
| `validation.deadlinePast` | `POST`/`PUT /v1/goals`: `deadline` is before the current month (UTC). Not checked when `PUT` keeps the stored deadline (field `deadline`) |

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
| 409 | `goal.concurrentUpdate` | `POST /v1/goals/{id}/contributions` | the goal was linked to another account (or unlinked) by another request while the contribution was processed. Nothing is written. Reload the goal and retry |
| 429 | title "Too many requests" | `/v1/auth/*` and `DELETE /v1/me` | rate limit exceeded. The `Retry-After` header holds the wait in seconds. On refresh it does not mean the session ended: wait and retry |
| 500 | title "Server error" | any | unexpected failure. Report the `correlationId` |
| 503 | `rates.unavailable` (title "Service Unavailable") | `POST /v1/transfers`, `POST`/`PUT /v1/transactions`, `PUT /v1/settings`, `GET /v1/budgets`, `POST /v1/budgets`, `PUT /v1/budgets/{id}`, `GET /v1/goals`, `PUT /v1/goals/{id}`, `POST /v1/goals/{id}/contributions`, `DELETE /v1/categories/{id}` | a conversion that needs an exchange rate that is not stored yet: a cross-currency transfer, transaction or contribution without a manual `rate` (a transaction may also need the rate between the account and base currencies, a contribution the rate between the linked account and base currencies), or a base currency change while some currency used by a transaction, budget, goal or contribution has no stored rate at all. Nothing is written. Retry later; transfers, transactions and contributions can also send a manual `rate` (only for the entered-to-account conversion). On the budget and goal reads, `POST /v1/budgets`, `PUT /v1/budgets/{id}`, `PUT /v1/goals/{id}` and `DELETE /v1/categories/{id}` (a budget merge that needs a rate) the case cannot happen once the settings check passed and is kept so that it never becomes a 500 |

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
- `balance` is `openingBalance` plus the sum of the account's transfer legs and transactions (`+accountAmount` for income, `-accountAmount` for expense), computed by the server. `inUse` is true when the account has any transfer or transaction. `linkedGoals` is the number of the user's goals linked to the account. Deleting an account that has no history but is linked to goals answers 204 and unlinks them (`accountId: null`).
- `DELETE` removes an account that has no history. An account in use answers 400 `id: ["validation.accountInUse"]`: archive it with `PUT` and `archived: true` instead. Deleting a missing id answers 404.
- `PUT` replaces the editable fields. The currency of an account in use (with a transfer or a transaction) cannot change (`validation.currencyLocked`).
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

## Categories and transactions

Every category and transaction belongs to the current user, and an id of another user answers exactly like an unknown id (404). The validation rules of the previous section apply: the first failing rule per field, all failing fields in one 400, a whitespace-only string counts as missing, and validation runs before the lookup. A `title`, `note` or category `name` that is not a string or contains a control character answers `validation.invalidValue`.

### Categories

- `GET /v1/categories`: `pageSize` defaults to 100 (maximum 100). The optional `kind` query (`expense` or `income`) filters, and an unknown value answers 400 `kind: ["validation.invalidValue"]`. Categories are ordered by kind (`expense` first), then the system ones last (`other`, then `transfer`), then by creation order. The starter categories keep the order of the list below.
- Each user gets a starter set of 18 categories, created at registration in the registration locale (`Accept-Language`, `en` by default). Users that existed before categories were introduced got the set in their settings locale. Names are stored, so a later locale change does not rename them.
  - expense: Groceries, Transport, Housing, Cafes, Health, Shopping, Leisure, Gifts, Other (system `other`), Transfers (system `transfer`);
  - income: Salary, Freelance, Gift, Interest, Refund, Sale, Bonus, Other (system `other`).
- A user has one `other` category per kind and one `transfer` category (expense). System categories can be renamed and recoloured, but their kind is locked and they cannot be deleted (`id: ["validation.systemCategory"]`). Transactions cannot use the `transfer` category (`categoryId: ["validation.unknownCategory"]`).
- `name` is 1 to 40 characters after trimming and is stored trimmed. It is unique per user and kind, case-insensitively (`validation.categoryNameTaken`). `color` is one of `chart-1` to `chart-6`.
- `inUse` is true when the category has at least one transaction or budget.
- `PUT` replaces `name`, `kind` and `color`. The kind of a category that is in use (a transaction or a budget) cannot change (`kind: ["validation.categoryKindLocked"]`), nor can the kind of a system category.
- `DELETE` of a category that is in use moves its transactions to the `other` category of the same kind, then deletes it. Its budgets move to the expense `other` category. When `other` already has a budget in the same period, that budget gets the sum of both limits (each converted into the base currency at the rate of the period start, the sum is not capped) and the moved budget disappears. Deleting a missing id answers 404.

### Transactions

- `GET /v1/transactions`: `pageSize` defaults to 20 (maximum 100). The answer is a `TransactionPage`: the usual `Page` fields plus `sum` and `currency`.
- Filters, all optional and combined with AND: `kind`, `categoryId`, `accountId`, `from` and `to` (both inclusive, `YYYY-MM-DD`) and `search`. An unknown `kind` or `sort`, a `from` or `to` that is not a real date, or a `categoryId` or `accountId` that is not a GUID answers 400 with `validation.invalidValue` under the parameter name. `from` later than `to` is not an error and gives an empty page. The server does not know financial periods: the client turns a period into `from` and `to`.
- `search` is trimmed and compared case-insensitively as literal text, so `%`, `_` and `\` are plain characters. A transaction matches when its `title`, its `note` or the name of its category contains the text. When the text also looks like an amount (digits, optionally followed by `.` or `,` and up to two digits), a transaction matches too when its entered `amount` written as `units.cc` starts with it, reading a comma as a dot. For example `12,5` matches 1250 (`12.50`) and 12500 (`125.00`) does not. An empty search is ignored.
- `sort` is one of `-date` (default), `date`, `-amount` and `amount`. `-date` is newest first: `date`, then creation time, then `id`, all descending. `date` is the same order reversed. `amount` sorts by `baseAmount` (not by the entered amount), ascending for `amount` and descending for `-amount`, and ties fall back to the `-date` order. Editing a transaction keeps its creation time.
- `sum` is the signed total in the base currency over every transaction that matches the filters, not only the page: `+baseAmount` for income and `-baseAmount` for expense. It is 0 for an empty result. `currency` is the base currency the `sum` and every `baseAmount` are in.
- `POST` and `PUT` take the account, the category and the entered `amount` and `currency`. The server stores `rate`, `accountAmount` and `baseAmount` with the transaction:
  - `rate` is 1 when `currency` equals the account currency (a sent `rate` is still validated, then ignored). Otherwise it is the manual `rate` rounded to 6 decimals when sent, and the exchange rate of the transaction `date` when not.
  - `accountAmount` is `amount` times `rate`, rounded half away from zero to a whole minor unit.
  - `baseAmount` is `amount` when `currency` is the base currency. Otherwise it is `accountAmount` converted from the account currency into the base currency at the exchange rate of the `date`, rounded the same way.
  - A converted `accountAmount` that rounds to 0 answers `rate: ["validation.positiveAmount"]`, and one above 100000000000 answers `rate: ["validation.invalidValue"]`.
  - A manual `rate` must be a JSON number with `0 < rate <= 1000000`.
  - A rate that is needed but not stored answers 503 `rates.unavailable`.
- `title` is optional, stored trimmed, and is `""` when missing or blank. `note` is stored trimmed and is `null` when missing or blank. The limits are 100 and 200 characters after trimming (`validation.titleTooLong`, `validation.noteTooLong`).
- `categoryId` must be a category of the user that is not the `transfer` category, and its kind must equal the transaction `kind` (`validation.categoryKindMismatch`). `accountId` must be an account of the user and not archived, except that `PUT` may keep the account the transaction already has even when it was archived since.
- A stored transaction never changes when exchange rates change later. Only a change of the base currency rewrites `baseAmount`: `PUT /v1/settings` with a new `baseCurrency` recomputes `baseAmount` of every transaction of the user at the exchange rate of its own date, exactly as a new transaction would be priced, and the `sum`, the `amount` sort and every `baseAmount` are then in the new currency. If a currency used by the transactions has no stored rate at all, the answer is 503 `rates.unavailable` and nothing changes.
- `GET /v1/transactions/{id}` answers 404 for an unknown or foreign id. `POST` answers 201 with `Location: /v1/transactions/{id}`, `PUT` 200 and `DELETE` 204.
- Transactions change `balance` and `inUse` of the account and `inUse` of the category. Deleting a transaction restores the balance.

## Budgets and goals

Every budget and goal belongs to the current user, and an id of another user answers exactly like an unknown id (404). The validation rules above apply: the first failing rule per field, all failing fields in one 400, a whitespace-only string counts as missing, and validation runs before the lookup, so an invalid body sent to an unknown id answers 400. Ids in paths must be GUIDs, any other value answers 404. Budgets and goals are never cached, and "today" is the UTC date of the server.

### Financial periods

- A period is a `YYYY-MM` key between `1970-01` and `2099-12`. Its first day depends on `monthStartDay` of the settings: that day of the key's month (1 to 28), or with `lastWorkingDay` the last Monday to Friday day of the previous month. It lasts until the day before the start of the next key. For example with `monthStartDay` 5, period `2026-10` runs from 2026-10-05 to 2026-11-04.
- The period of a date is found by trying the month of the date, then the previous one, then the next one. `GET /v1/budgets` without `period` shows the period of today.
- The server tells the client the range: `BudgetPage` carries `period`, `from` and `to`. Changing `monthStartDay` moves the ranges of existing budgets, and their keys stay.
- `spent` counts the expense transactions of the budget category dated inside the period, by `baseAmount`. Transfers are not counted.

### Budgets

- `GET /v1/budgets`: `pageSize` defaults to 100 (maximum 100). An invalid `period`, `page` or `pageSize` answers 400 `invalidValue` under the parameter name. Budgets are ordered by creation order (a copy keeps the order of its source). `summary` covers every budget of the period, not only the page.
- `limit` is stored in the base currency of the moment it was written. After a base currency change the response shows it converted at the rate of the period start, `round(limit * rate)`, with the rate rounded to 6 decimals and half away from zero.
- `status` is `over_budget` when `spent > limit`, `almost_spent` when `spent` is at least 85% of `limit`, otherwise `on_track`. `perDay` is `floor(remaining / daysLeft)` when both are positive, otherwise `null`.
- `POST` answers 201 with `Location: /v1/budgets/{id}`. There is no `GET /v1/budgets/{id}`. `PUT` answers 200 with the budget and replaces `categoryId`, `period` and `limit` (the limit is taken as base currency). `DELETE` answers 204.
- Checks on `categoryId`, in this order: required, then a GUID (otherwise `unknownCategory`), then the category must be one of the user's expense categories that is not `transfer` (`unknownCategory`; an income category answers `expenseCategoryOnly`), then `budgetExists` when the period is valid. One budget exists per category and period.
- `POST /v1/budgets/copy` copies every budget of the previous period whose category has no budget in the target `period`, converting the limit into the base currency at the start of the source period. It answers 200 `{ copied }`. When nothing is copied it answers 400 `period: ["validation.nothingToCopy"]`. Calling it twice never duplicates a category.
- A category with a budget is `inUse`, so its kind cannot change and an income category never ends up with a budget.

### Goals

- `GET /v1/goals`: `pageSize` defaults to 100 (maximum 100), ordered by creation order. `summary` covers every goal: `saved` is the sum, `active` counts goals that are not reached, and `monthly` is the sum of the contributions of the last 3 months (today minus 3 months, exclusive, to today) divided by 3 and rounded, in the base currency.
- `target` and `saved` are shown in the current base currency. After a base currency change they are converted from the stored currency at today's rate, with no drift after repeated changes.
- `status` is `reached` when `saved >= target`, `overdue` when the deadline month has passed, otherwise `active`. `percent` is `floor(saved * 100 / target)`. `monthly` is `null` without a deadline, when `remaining` is 0 or when the deadline has passed, otherwise `ceil(remaining / max(months until the deadline, 1))`.
- `POST` answers 201 with `Location: /v1/goals/{id}`. There is no `GET /v1/goals/{id}`. `PUT` answers 200 with the goal and replaces `name`, `target` (taken as base currency), `deadline` and `accountId`; omitted or `null` clears the deadline and the link. `DELETE` answers 204, removes the goal and its contributions and keeps the transfers they made.
- `deadline` must not be before the current month (`deadlinePast`) and at most 600 months ahead (`invalidValue`). `PUT` skips both checks when the deadline equals the stored one. `accountId` must be one of the user's accounts (`unknownAccount`), then a savings account (`savingsAccountOnly`), then not archived (`archivedAccount`). `PUT` skips the last two when the goal stays linked to the same account.
- Deleting an account that is linked to goals unlinks them instead of failing, and `linkedGoals` counts the goals linked to an account.

### Contributions

- `POST /v1/goals/{id}/contributions` answers 200 with the updated `Goal`, not with the contribution, and no `Location` header. An unknown or foreign goal answers 404 once the body validated.
- A goal without a linked account moves no money: `amount` is in the base currency and is added to `saved`. `fromAccountId` is optional there, but validated when sent.
- A goal with a linked account needs `fromAccountId` (an unarchived account of the user, other than the linked one) and creates a transfer from it to the linked account with `note` equal to the goal name. The rules of transfers apply: the same currency gives rate 1, otherwise the manual `rate` (rounded to 6 decimals), otherwise the stored rate of `date`; `rate: ["validation.positiveAmount"]` and `rate: ["validation.invalidValue"]` for a converted amount that rounds to 0 or is too large; 503 `rates.unavailable` when a rate is missing. The transfer appears in `GET /v1/transfers` and changes both balances.
- `saved` grows by the amount received by the linked account, valued in the base currency at the rate of `date`.
- If the goal is linked to another account or unlinked by another request while the contribution runs, nothing is written and the answer is 409 `goal.concurrentUpdate`.
