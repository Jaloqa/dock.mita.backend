# API contract: authentication, users, settings, accounts, transfers, rates, categories, transactions, tags, budgets, goals, debts, groups, overview and reports

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
| GET | `/v1/transactions?search&categoryId&accountId&tagIds&kind&from&to&sort&page&pageSize` | required | none | 200 `TransactionPage` | 400, 401 |
| GET | `/v1/transactions/{id}` | required | none | 200 `Transaction` | 401, 404 |
| POST | `/v1/transactions` | required | `TransactionInput` | 201 `Transaction`, `Location` header | 400, 401, 403, 503 |
| PUT | `/v1/transactions/{id}` | required | `TransactionInput` | 200 `Transaction` | 400, 401, 403, 404, 503 |
| DELETE | `/v1/transactions/{id}` | required | none | 204 | 401, 403, 404 |
| GET | `/v1/tags?search&page&pageSize` | required | none | 200 `Page<Tag>` | 400, 401 |
| PUT | `/v1/tags/{id}` | required | `TagInput` | 200 `Tag` | 400, 401, 403, 404 |
| DELETE | `/v1/tags/{id}` | required | none | 204 | 401, 403, 404 |
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
| GET | `/v1/debts?status&page&pageSize` | required | none | 200 `DebtPage` | 400, 401, 503 |
| POST | `/v1/debts` | required | `DebtInput` | 201 `Debt`, `Location` header | 400, 401, 403, 503 |
| PUT | `/v1/debts/{id}` | required | `DebtInput` | 200 `Debt` | 400, 401, 403, 404, 503 |
| DELETE | `/v1/debts/{id}` | required | none | 204 | 401, 403, 404 |
| GET | `/v1/debts/{id}/repayments?page&pageSize` | required | none | 200 `Page<DebtRepayment>` | 400, 401, 404 |
| POST | `/v1/debts/{id}/repayments` | required | `DebtRepaymentInput` | 200 `Debt`, no `Location` header | 400, 401, 403, 404, 503 |
| DELETE | `/v1/debts/{id}/repayments/{repaymentId}` | required | none | 204 | 401, 403, 404 |
| GET | `/v1/groups?page&pageSize` | required | none | 200 `Page<GroupSummary>` | 400, 401 |
| POST | `/v1/groups` | required | `GroupInput` | 201 `Group`, `Location` header | 400, 401, 403 |
| GET | `/v1/groups/{id}` | required | none | 200 `Group` | 401, 404 |
| PUT | `/v1/groups/{id}` | required | `GroupUpdateInput` | 200 `Group` | 400, 401, 403, 404 |
| DELETE | `/v1/groups/{id}` | required | none | 204 | 401, 403, 404 |
| POST | `/v1/groups/{id}/share` | required | none | 200 `Group` | 401, 403, 404 |
| DELETE | `/v1/groups/{id}/share` | required | none | 204 | 401, 403, 404 |
| POST | `/v1/groups/{id}/members` | required | `GroupNameInput` | 201 `GroupMember`, no `Location` header | 400, 401, 403, 404 |
| PUT | `/v1/groups/{id}/members/{memberId}` | required | `GroupNameInput` | 200 `GroupMember` | 400, 401, 403, 404 |
| DELETE | `/v1/groups/{id}/members/{memberId}` | required | none | 204 | 400, 401, 403, 404 |
| POST | `/v1/groups/{id}/categories` | required | `GroupNameInput` | 201 `GroupCategory`, no `Location` header | 400, 401, 403, 404 |
| GET | `/v1/groups/{id}/expenses?deleted&page&pageSize` | required | none | 200 `Page<GroupExpense>` | 400, 401, 404 |
| POST | `/v1/groups/{id}/expenses` | required | `GroupExpenseInput` | 201 `GroupExpense`, no `Location` header | 400, 401, 403, 404, 503 |
| PUT | `/v1/groups/{id}/expenses/{expenseId}` | required | `GroupExpenseInput` | 200 `GroupExpense` | 400, 401, 403, 404, 503 |
| DELETE | `/v1/groups/{id}/expenses/{expenseId}` | required | none | 204 | 401, 403, 404 |
| POST | `/v1/groups/{id}/expenses/{expenseId}/restore` | required | none | 200 `GroupExpense` | 401, 403, 404 |
| GET | `/v1/groups/{id}/expenses/{expenseId}/history?page&pageSize` | required | none | 200 `Page<GroupEntryEvent<GroupExpenseState>>` | 400, 401, 404 |
| GET | `/v1/groups/{id}/repayments?deleted&page&pageSize` | required | none | 200 `Page<GroupRepayment>` | 400, 401, 404 |
| POST | `/v1/groups/{id}/repayments` | required | `GroupRepaymentInput` | 201 `GroupRepayment`, no `Location` header | 400, 401, 403, 404, 503 |
| PUT | `/v1/groups/{id}/repayments/{repaymentId}` | required | `GroupRepaymentInput` | 200 `GroupRepayment` | 400, 401, 403, 404, 503 |
| DELETE | `/v1/groups/{id}/repayments/{repaymentId}` | required | none | 204 | 401, 403, 404 |
| POST | `/v1/groups/{id}/repayments/{repaymentId}/restore` | required | none | 200 `GroupRepayment` | 401, 403, 404 |
| GET | `/v1/groups/{id}/repayments/{repaymentId}/history?page&pageSize` | required | none | 200 `Page<GroupEntryEvent<GroupRepaymentState>>` | 400, 401, 404 |
| GET | `/v1/groups/{id}/balances?currency` | required | none | 200 `GroupBalances` | 400, 401, 404, 503 |
| GET | `/v1/overview?period` | required | none | 200 `Overview` | 400, 401, 503 |
| GET | `/v1/reports?range` | required | none | 200 `Report` | 400, 401 |

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
  baseCurrency: 'PLN' | 'EUR' | 'USD' | 'UAH'; // only these four, although Currency has 148 codes
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

// 148 ISO 4217 codes: PLN, EUR, USD and UAH, then every other currency of NBP tables A and B alphabetically (XDR is not one of them).
// The rules for amounts, rates and the base currency are in "Accounts, transfers and rates".
type Currency =
  | 'PLN' | 'EUR' | 'USD' | 'UAH'
  | 'AED' | 'AFN' | 'ALL' | 'AMD' | 'AOA' | 'ARS' | 'AUD' | 'AWG' | 'AZN' | 'BAM' | 'BBD' | 'BDT'
  | 'BHD' | 'BIF' | 'BND' | 'BOB' | 'BRL' | 'BSD' | 'BWP' | 'BYN' | 'BZD' | 'CAD' | 'CDF' | 'CHF'
  | 'CLP' | 'CNY' | 'COP' | 'CRC' | 'CUP' | 'CVE' | 'CZK' | 'DJF' | 'DKK' | 'DOP' | 'DZD' | 'EGP'
  | 'ERN' | 'ETB' | 'FJD' | 'GBP' | 'GEL' | 'GHS' | 'GIP' | 'GMD' | 'GNF' | 'GTQ' | 'GYD' | 'HKD'
  | 'HNL' | 'HTG' | 'HUF' | 'IDR' | 'ILS' | 'INR' | 'IQD' | 'IRR' | 'ISK' | 'JMD' | 'JOD' | 'JPY'
  | 'KES' | 'KGS' | 'KHR' | 'KMF' | 'KRW' | 'KWD' | 'KZT' | 'LAK' | 'LBP' | 'LKR' | 'LRD' | 'LSL'
  | 'LYD' | 'MAD' | 'MDL' | 'MGA' | 'MKD' | 'MMK' | 'MNT' | 'MOP' | 'MRU' | 'MUR' | 'MVR' | 'MWK'
  | 'MXN' | 'MYR' | 'MZN' | 'NAD' | 'NGN' | 'NIO' | 'NOK' | 'NPR' | 'NZD' | 'OMR' | 'PAB' | 'PEN'
  | 'PGK' | 'PHP' | 'PKR' | 'PYG' | 'QAR' | 'RON' | 'RSD' | 'RUB' | 'RWF' | 'SAR' | 'SBD' | 'SCR'
  | 'SDG' | 'SEK' | 'SGD' | 'SLE' | 'SOS' | 'SRD' | 'SSP' | 'STN' | 'SVC' | 'SYP' | 'SZL' | 'THB'
  | 'TJS' | 'TMT' | 'TND' | 'TOP' | 'TRY' | 'TTD' | 'TWD' | 'TZS' | 'UGX' | 'UYU' | 'UZS' | 'VES'
  | 'VND' | 'VUV' | 'WST' | 'XAF' | 'XCD' | 'XCG' | 'XOF' | 'XPF' | 'YER' | 'ZAR' | 'ZMW' | 'ZWG';

type Account = {
  id: string;             // GUID
  name: string;
  type: 'card' | 'cash' | 'savings';
  currency: Currency;
  openingBalance: number; // minor units of the account currency
  balance: number;        // minor units, computed by the server: openingBalance plus every transfer leg plus every transaction (income adds, expense subtracts, in the account currency) plus every debt leg (a lent debt subtracts, a borrowed one adds) plus every repayment leg (a repayment of a lent debt adds, of a borrowed debt subtracts)
  cardLast4: string | null;
  archived: boolean;
  inUse: boolean;         // the account has at least one transfer, transaction, debt or repayment
  linkedGoals: number;    // the number of the user's goals linked to this account
};

type AccountInput = {
  name: string;           // 1 to 40 characters after trimming, no control characters
  type: 'card' | 'cash' | 'savings';
  currency: Currency;
  openingBalance: number; // integer minor units of the account currency, absolute value up to 100000000000
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
  rate: number;           // major units of the target currency per major unit of the source currency, 1 for the same currency
  note: string | null;
};

type TransferInput = {
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  fromAccountId: string;
  toAccountId: string;
  fromAmount: number;     // integer minor units of the source account currency, 1 to 100000000000
  rate?: number;          // manual rate per major unit, 0 < rate <= 1000000000; not used for the same currency, but still validated
  note?: string | null;   // up to 200 characters after trimming
};

type Rate = {
  date: string;           // YYYY-MM-DD, the requested date (today in UTC when omitted)
  from: Currency;
  to: Currency;
  value: number;          // major units of "to" per 1 major unit of "from", for example 0.024764 for JPY to PLN
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
  rate: number;           // entered currency to account currency per major unit, 1 for the same currency
  baseAmount: number;     // minor units of the current base currency
  tags: TransactionTag[]; // ordered by lower-case name, then id; [] when none
};

type TransactionTag = {
  id: string;             // GUID
  name: string;
};

type TransactionInput = {
  kind: CategoryKind;
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  title?: string;         // up to 100 characters after trimming
  note?: string | null;   // up to 200 characters after trimming
  categoryId: string;
  accountId: string;
  amount: number;         // integer minor units of `currency`, 1 to 100000000000
  currency: Currency;     // any supported currency, converted into the account currency
  rate?: number;          // manual rate per major unit, 0 < rate <= 1000000000; not used for the same currency, but still validated
  tags?: string[];        // tag names, each 1 to 30 characters after trimming with no control characters, at most 10 distinct; missing or null means no tags
};

type Tag = {
  id: string;             // GUID
  name: string;
  usage: number;          // the number of the user's transactions with the tag
};

type TagInput = {
  name: string;           // 1 to 30 characters after trimming, no control characters, unique per user
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
  amount: number;         // integer minor units, 1 to 100000000000: of the base currency for a goal without an account, of the source account currency otherwise
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  fromAccountId?: string; // required when the goal is linked to an account
  rate?: number;          // manual rate per major unit, 0 < rate <= 1000000000; used only for a linked goal in another currency, but still validated
};

type DebtDirection = 'lent' | 'borrowed'; // lent: money left the user ("owed to me"); borrowed: money came to the user ("I owe")

type DebtStatus = 'open' | 'settled';

type Debt = {
  id: string;             // GUID
  direction: DebtDirection;
  counterparty: string;   // 1 to 40 characters, stored trimmed
  amount: number;         // minor units of `currency`
  currency: Currency;     // the debt currency
  repaid: number;         // minor units of `currency`: the sum of debtAmount over the repayments
  remaining: number;      // amount - repaid
  status: DebtStatus;     // 'open' while remaining > 0, 'settled' at 0
  overdue: boolean;       // open, with a dueDate before today (UTC)
  date: string;           // YYYY-MM-DD
  dueDate: string | null; // YYYY-MM-DD
  accountId: string | null;     // the account the money moved through
  accountAmount: number | null; // minor units of the account currency, null without an account
  rate: number | null;    // debt currency to account currency per major unit, 1 for the same currency, null without an account
  note: string | null;
  repayments: number;     // the number of repayments
};

type DebtInput = {
  direction: DebtDirection;
  counterparty: string;   // 1 to 40 characters after trimming, no control characters
  amount: number;         // integer minor units of `currency`, 1 to 100000000000
  currency: Currency;     // any supported currency, converted into the account currency when an account is given
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  dueDate?: string | null; // YYYY-MM-DD, not before `date`
  accountId?: string | null; // a non-archived account of the user; omitted or null moves no money
  rate?: number;          // manual rate per major unit, 0 < rate <= 1000000000; used only with an account in another currency, but always validated
  note?: string | null;   // up to 200 characters after trimming
};

type DebtSummary = {
  owedToMe: number;       // minor units of the base currency: the remaining amount of every open lent debt
  owedByMe: number;       // minor units of the base currency: the remaining amount of every open borrowed debt
  open: number;           // the number of open debts
  overdue: number;        // the number of overdue debts
};

type DebtPage = Page<Debt> & {
  currency: Currency;     // the current base currency
  summary: DebtSummary;   // over every open debt of the user, whatever the status filter and the page
};

type DebtRepayment = {
  id: string;             // GUID
  date: string;           // YYYY-MM-DD
  amount: number;         // minor units of `currency`
  currency: Currency;     // the repayment currency
  rate: number;           // repayment currency to debt currency per major unit, 1 for the same currency
  debtAmount: number;     // minor units of the debt currency: what the repayment takes off the debt
  accountId: string | null;     // the account that received (lent) or paid (borrowed) the money
  accountAmount: number | null; // minor units of the account currency, null without an account
  note: string | null;
};

type DebtRepaymentInput = {
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  amount: number;         // integer minor units of `currency`, 1 to 100000000000
  currency: Currency;     // any supported currency, converted into the debt currency
  rate?: number;          // manual rate per major unit, 0 < rate <= 1000000000; used only in another currency than the debt, but always validated
  accountId?: string | null; // a non-archived account of the user; omitted or null moves no money
  note?: string | null;   // up to 200 characters after trimming
};

type GroupSummary = {
  id: string;             // GUID
  name: string;
  currency: Currency;     // the main currency of the group
  members: number;        // the number of members
  total: number;          // minor units of the main currency: the sum of groupAmount over the live expenses
};

type Group = {
  id: string;             // GUID
  name: string;           // 1 to 40 characters, stored trimmed
  currency: Currency;     // the main currency: every entry is converted into it
  currencyLocked: boolean; // the group has an expense or a repayment, deleted ones included, so the currency cannot change
  total: number;          // minor units of the main currency: the sum of groupAmount over the live expenses
  members: GroupMember[]; // in member order, at most 50
  categories: GroupCategory[]; // the 9 standard categories in this order (groceries, transport, housing, cafes, health, shopping, leisure, gifts, other), then the group's own in creation order, at most 100 of them
  shareToken: string | null; // the token of the share link while the link is on (43 characters of A-Z, a-z, 0-9, - and _), null while it is off
};

type GroupMember = {
  id: string;             // GUID
  name: string;           // 1 to 40 characters, stored trimmed, unique per group case-insensitively
  inUse: boolean;         // a history event of the group names the member, as its actor or in its state (payer, share, either side of a repayment), deleted entries and earlier states included: it can be renamed but not removed
};

type GroupCategory = {
  id: string;             // a standard key (groceries, transport, housing, cafes, health, shopping, leisure, gifts, other) or the GUID of a category of the group
  standard: boolean;      // true for the 9 standard keys
  name: string | null;    // null for a standard category (the client translates the key), the stored name for a category of the group
};

type GroupInput = {
  name: string;           // 1 to 40 characters after trimming, no control characters
  currency: Currency;     // any supported currency, the main currency of the group
  members: string[];      // 1 to 50 names, each 1 to 40 characters after trimming with no control characters, unique case-insensitively; the order of the array is the member order
};

type GroupUpdateInput = {
  name: string;           // as in GroupInput
  currency: Currency;     // changes only while the group has no expense and no repayment, deleted ones included
};

type GroupNameInput = {
  name: string;           // 1 to 40 characters after trimming, no control characters. A member name is unique per group case-insensitively. A category name is unique per group case-insensitively and differs from the name of every standard category in en, pl, ru and uk
};

type GroupActor = {
  kind: 'owner' | 'member'; // always 'owner' in this release
  memberId: string | null;  // null for the owner
  name: string;             // the current name of the owner (the user) or of the member
};

type GroupShare = {
  memberId: string;       // GUID
  amount: number;         // minor units of the expense currency, always 1 or more; the shares of an expense add up to its amount
  groupAmount: number;    // minor units of the main currency, 0 or more; the shares of an expense add up to its groupAmount
};

type GroupSplitInput =
  | { mode: 'equal'; memberIds: string[] }  // 1 to 50 distinct members of the group; a member whose equal share rounds to 0 gets no share
  | { mode: 'exact'; shares: { memberId: string; amount: number }[] }; // 1 to 50 distinct members, each amount an integer from 1 to the expense amount, adding up to it

type GroupExpense = {
  id: string;             // GUID
  payerId: string;        // a member of the group
  amount: number;         // minor units of `currency`
  currency: Currency;     // the expense currency
  date: string;           // YYYY-MM-DD
  categoryId: string;     // a standard key or the GUID of a category of the group
  tags: string[];         // trimmed, in the spelling entered first, [] when none
  description: string | null; // stored trimmed, null when blank
  rate: number;           // expense currency to main currency per major unit, 1 for the same currency
  rateManual: boolean;    // true when `rate` is a manual rate that was sent with the entry
  groupAmount: number;    // minor units of the main currency: `amount` converted with `rate`
  split: 'equal' | 'exact'; // how the expense was split
  shares: GroupShare[];   // in member order, one per member that shares
  deleted: boolean;
  createdBy: GroupActor;
  createdAt: string;      // UTC ISO 8601 with milliseconds
  updatedBy: GroupActor | null; // the actor of the newest change after creation (edit, delete or restore), null when never changed
  updatedAt: string | null; // UTC ISO 8601 with milliseconds
};

type GroupExpenseInput = {
  payerId: string;        // a member of the group
  amount: number;         // integer minor units of `currency`, 1 to 100000000000
  currency: Currency;     // any supported currency, converted into the main currency
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  categoryId?: string | null; // a standard key (lower case, exactly as listed) or the GUID of a category of the group; missing, null or blank means "other"
  tags?: string[];        // tag names, each 1 to 30 characters after trimming with no control characters, at most 10 distinct case-insensitively; missing or null means no tags
  description?: string | null; // up to 100 characters after trimming, no control characters
  rate?: number;          // manual rate per major unit, 0 < rate <= 1000000000; a blank string counts as missing; not used for the main currency, but still validated
  split?: GroupSplitInput | null; // missing or null: equal between every current member of the group
};

type GroupExpenseState = {
  // the expense after a change: the fields of GroupExpense without id, deleted, createdBy, createdAt, updatedBy and updatedAt
  payerId: string;
  amount: number;
  currency: Currency;
  date: string;           // YYYY-MM-DD
  categoryId: string;
  tags: string[];
  description: string | null;
  rate: number;
  rateManual: boolean;
  groupAmount: number;
  split: 'equal' | 'exact';
  shares: GroupShare[];   // in member order
};

type GroupRepayment = {
  id: string;             // GUID
  fromMemberId: string;   // the member who paid
  toMemberId: string;     // the member who received, a different one
  amount: number;         // minor units of `currency`
  currency: Currency;     // the repayment currency
  date: string;           // YYYY-MM-DD
  rate: number;           // repayment currency to main currency per major unit, 1 for the same currency
  rateManual: boolean;    // true when `rate` is a manual rate that was sent with the entry
  groupAmount: number;    // minor units of the main currency: `amount` converted with `rate`
  note: string | null;    // stored trimmed, null when blank
  deleted: boolean;
  createdBy: GroupActor;
  createdAt: string;      // UTC ISO 8601 with milliseconds
  updatedBy: GroupActor | null; // the actor of the newest change after creation (edit, delete or restore), null when never changed
  updatedAt: string | null; // UTC ISO 8601 with milliseconds
};

type GroupRepaymentInput = {
  fromMemberId: string;   // a member of the group
  toMemberId: string;     // another member of the group
  amount: number;         // integer minor units of `currency`, 1 to 100000000000
  currency: Currency;     // any supported currency, converted into the main currency
  date: string;           // YYYY-MM-DD, at most one year after today (UTC)
  rate?: number;          // manual rate per major unit, 0 < rate <= 1000000000; a blank string counts as missing; not used for the main currency, but still validated
  note?: string | null;   // up to 200 characters after trimming, no control characters
};

type GroupRepaymentState = {
  // the repayment after a change: the fields of GroupRepayment without id, deleted, createdBy, createdAt, updatedBy and updatedAt
  fromMemberId: string;
  toMemberId: string;
  amount: number;
  currency: Currency;
  date: string;           // YYYY-MM-DD
  rate: number;
  rateManual: boolean;
  groupAmount: number;
  note: string | null;
};

type GroupEntryEvent<T> = {
  id: string;             // GUID
  action: 'created' | 'updated' | 'deleted' | 'restored';
  at: string;             // UTC ISO 8601 with milliseconds
  actor: GroupActor;
  state: T;               // the entry as it was right after the change
};

type GroupMemberBalance = {
  memberId: string;       // GUID
  paid: number;           // minor units: the groupAmount of the live expenses the member paid
  share: number;          // the member's groupAmount shares of the live expenses
  sent: number;           // the groupAmount of the live repayments the member sent
  received: number;       // the groupAmount of the live repayments the member received
  balance: number;        // paid - share + sent - received, positive when the others owe the member
};

type GroupTransfer = {
  fromMemberId: string;   // the member who pays
  toMemberId: string;     // the member who receives
  amount: number;         // minor units, above 0
};

type GroupBalances = {
  currency: Currency;     // the currency of every figure: the `currency` query, the main currency without it
  groupCurrency: Currency; // the main currency of the group
  total: number;          // the sum of groupAmount over the live expenses
  members: GroupMemberBalance[]; // every member, in member order
  transfers: GroupTransfer[];    // the suggested transfers that settle every balance, [] when all are 0
};

type Overview = {
  range: 'week' | 'month' | 'year';
  from: string;           // YYYY-MM-DD, first day of the range
  to: string;             // YYYY-MM-DD, last day of the range
  previousFrom: string;   // YYYY-MM-DD, first day of the previous range
  previousTo: string;     // YYYY-MM-DD, last day of the previous range
  currency: Currency;     // the current base currency, every amount is in it
  balance: {
    amount: number;       // minor units: every account except archived accounts at 0, converted into the base currency at today's rate
    accounts: number;     // the number of those accounts
  };
  debts: DebtSummary;     // every amount in the base currency, converted at today's rate
  income: OverviewTotal;
  expense: OverviewTotal;
  averageExpense: number | null; // round(expense.amount / expense.count), null without expense transactions
  transactionCount: number;      // income.count + expense.count
  spending: SpendingSlice[];     // at most 5 slices plus the bucket
  month: OverviewMonth;
  topBudgets: OverviewBudget[];  // at most 5
  recent: OverviewTransaction[]; // at most 6
  goals: OverviewGoal[];         // at most 3
  goalCount: number;             // every goal of the user
  hasTransactions: boolean;      // the user has any transaction, on any date
};

type OverviewTotal = {
  amount: number;         // minor units of the base currency
  count: number;
  change: number | null;  // percent against the previous range, one decimal, null when the previous amount is 0
};

type SpendingSlice = {
  categoryId: string | null; // null for the bucket of the remaining categories
  name: string | null;       // null for the bucket
  amount: number;            // minor units of the base currency
  percent: number;           // one decimal, the percents of all slices add up to 100
};

type OverviewMonth = {
  period: string;         // YYYY-MM, the period of today
  from: string;           // YYYY-MM-DD
  to: string;             // YYYY-MM-DD
  planned: number;        // sum of limit over every budget of the period
  spent: number;
  left: number;           // planned - spent
  budgets: number;        // the number of budgets of the period
};

type OverviewBudget = {
  id: string;             // GUID
  categoryId: string;
  name: string;           // the category name
  limit: number;          // minor units of the base currency
  spent: number;
  remaining: number;      // limit - spent, negative when over the limit
  status: 'on_track' | 'almost_spent' | 'over_budget';
};

type OverviewTransaction = {
  id: string;             // GUID
  kind: CategoryKind;
  date: string;           // YYYY-MM-DD
  title: string;          // "" when none
  categoryId: string;     // GUID
  categoryName: string;
  categoryColor: string;  // chart-1 to chart-6
  accountName: string;
  amount: number;         // minor units of the base currency (baseAmount)
};

type OverviewGoal = {
  id: string;             // GUID
  name: string;
  target: number;         // minor units of the base currency
  saved: number;
  percent: number;        // floor(saved * 100 / target), may exceed 100
  status: 'active' | 'reached' | 'overdue';
  deadline: string | null; // YYYY-MM
};

type Report = {
  range: '3m' | '6m' | '12m';
  from: string;           // YYYY-MM-DD, first day of the first period
  to: string;             // YYYY-MM-DD, last day of the current period
  currency: Currency;     // the current base currency, every amount is in it
  periods: ReportPeriod[]; // 3, 6 or 12 periods, oldest first, the last one is the period of today
  totals: {
    income: number;       // minor units, sum over the periods
    expense: number;
  };
  averageIncome: number;  // round(totals.income / number of periods)
  averageExpense: number;
  savingsRate: number | null; // percent, one decimal: (income - expense) / income, null when the income is 0
  structure: ReportStructure;
  hasTransactions: boolean;   // the user has any transaction, on any date
};

type ReportPeriod = {
  period: string;         // YYYY-MM
  from: string;           // YYYY-MM-DD
  to: string;             // YYYY-MM-DD
  income: number;         // minor units of the base currency
  expense: number;
};

type ReportStructure = {
  period: string;         // YYYY-MM, the period of today
  from: string;           // YYYY-MM-DD
  to: string;             // YYYY-MM-DD
  total: number;          // sum of the item amounts
  items: ReportStructureItem[]; // every expense category with spending, largest first
};

type ReportStructureItem = {
  categoryId: string;
  name: string;
  color: 'chart-1' | 'chart-2' | 'chart-3' | 'chart-4' | 'chart-5' | 'chart-6';
  amount: number;         // minor units of the base currency
  percent: number;        // one decimal, the percents of all items add up to 100
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
| `validation.required` | the field is missing, null or blank, including the `name` of `PUT /v1/tags/{id}`. Goal `deadline` and `accountId` and contribution `rate` are optional, and `fromAccountId` is required only for a linked goal. A debt needs `direction`, `counterparty`, `amount`, `currency` and `date`, and a repayment needs `date`, `amount` and `currency`: the other fields of both are optional. A group needs `name`, `currency` and `members` (at least one name), a group update `name` and `currency`, and a group member or group category `name`. A group expense needs `payerId`, `amount`, `currency` and `date`, and a group repayment `fromMemberId`, `toMemberId`, `amount`, `currency` and `date`: the other fields of both are optional. `GET /v1/rates` needs at least one of `from` and `to`: when both are missing, `to` is reported (field `to`) |
| `validation.invalidEmail` | the email is not a valid address. On login it is returned only when the email contains a control character |
| `validation.emailTaken` | register, or `PUT /v1/me`: another account uses this email |
| `validation.nameTooLong` | the name is longer than 40 characters after trimming (users, accounts, categories and goals), or the debt `counterparty` is, or a group `name`, a group member name (in `members` or as `name`) or a group category `name` is |
| `validation.passwordTooShort` | register: fewer than 10 characters |
| `validation.passwordTooLong` | register: more than 128 characters |
| `validation.invalidPassword` | `DELETE /v1/me`: the password is wrong, or the account is locked after repeated failures (field `password`) |
| `validation.invalidValue` | the value is of the wrong type or outside the allowed set, for example an unknown currency (anything but exactly three upper-case letters naming one of the 148 supported codes, so `jpy`, ` JPY` and `XDR` are refused), a `baseCurrency` other than PLN, EUR, USD or UAH, a `monthStartDay` outside 1 to 28 or a name containing a control character. On accounts and transfers it also covers an amount that is not an integer or is above 100000000000 in absolute value (minor units of its own currency), a `date` that is not a real `YYYY-MM-DD`, a manual `rate` that is not a JSON number, is not greater than 0 or is above 1000000000, a `note` that is not a string or is longer than 200 characters, and a converted transfer amount above 100000000000 minor units of the target account currency (reported under `rate`, also when the product is too large to represent). On categories it covers an unknown `kind` or `color`. On transactions it covers an unknown `kind` or `currency`, a `title` or `note` that is not a string, and the same rules for `amount`, `date`, `rate` and the converted amount as on transfers, and for `tags` a value that is not an array, an element that is not a string, and an element that is blank or contains a control character (checked before `tagTooLong` and `tooManyTags`). On tags it covers a `name` that is not a string or contains a control character. On budgets it covers a `period` that is not a `YYYY-MM` key between 1970-01 and 2099-12 and a `limit` that is not an integer or is above 100000000000. On goals it covers a `name` that is not a string or contains a control character, a `target` that is not an integer or is above 100000000000, and a `deadline` that is not a `YYYY-MM` key or is more than 600 months after the current month. On contributions it covers the same rules for `amount`, `date` and `rate` as on transfers. On debts it covers an unknown `direction` or `currency`, a `counterparty` or `note` that is not a string or contains a control character, a `dueDate` that is not a real `YYYY-MM-DD`, and the same rules for `amount`, `date`, `rate` and the converted `accountAmount` as on transactions. On repayments it covers an unknown `currency`, a `note` that is not a string or contains a control character, and the same rules for `amount`, `date`, `rate` and the converted `debtAmount` and `accountAmount` as on transfers. On groups it covers a `name` (of a group, a group member or a group category) that is not a string or contains a control character, an unknown `currency`, and `members` that is not an array or holds an element that is not a string, is blank or contains a control character (checked before `nameTooLong`, `tooManyMembers` and `memberNameTaken`). On group expenses and group repayments it covers an unknown `currency`, a `description` or `note` that is not a string or contains a control character, the same rules for `amount`, `date` and `rate` as on transfers (a blank `rate` string counts as missing), for `tags` the rules of transactions, and a converted `groupAmount` above 100000000000 minor units of the main currency (reported under `rate`). A `split` that is not an object (a blank string, a number or an array included), has a `mode` other than `equal` or `exact`, has no `memberIds` (equal) or `shares` (exact) array of 1 to 50 elements, repeats a member, or holds an exact share that is not an object or has no integer `amount` from 1 to 100000000000 is `split: ["validation.invalidValue"]`; this shape check wins over `validation.unknownMember` and `validation.splitMismatch`. In queries it is returned under the parameter name for an invalid `page`, `pageSize`, `accountId`, `categoryId`, `tagIds`, `date`, `from`, `to`, `kind`, `sort`, debt `status` (anything but `open` or `settled`, case-sensitive), budget `period`, overview `period` (anything but `week`, `month` or `year`, case-sensitive), report `range` (anything but `3m`, `6m` or `12m`), the `deleted` flag of the group expense and repayment lists (anything but `true` or `false`, case-sensitive; missing means `false`) or the `currency` of `GET /v1/groups/{id}/balances` (the rule of every currency above) |
| `validation.currencyLocked` | `PUT /v1/accounts/{id}`: the currency of an account that is in use (it has a transfer, a transaction, a debt or a repayment) cannot change (field `currency`) |
| `validation.cardLast4` | `cardLast4` is present, not blank and not exactly 4 digits. Checked for every account type |
| `validation.accountInUse` | `DELETE /v1/accounts/{id}`: the account has transfers, transactions, debts or repayments, so it can only be archived (field `id`) |
| `validation.dateTooFar` | transfer, transaction, goal contribution, debt, repayment, group expense or group repayment `date` is later than today plus one year |
| `validation.unknownAccount` | transfer `fromAccountId` or `toAccountId`, transaction `accountId`, goal `accountId`, contribution `fromAccountId`, debt `accountId` or repayment `accountId` is not one of the user's accounts (or not a GUID) |
| `validation.archivedAccount` | transfer `fromAccountId` or `toAccountId`, transaction `accountId`, goal `accountId`, contribution `fromAccountId`, debt `accountId` or repayment `accountId` is an archived account. A contribution to a goal whose linked account is archived answers it under `accountId`. On `PUT /v1/transactions/{id}` the account the transaction already has is allowed even when archived, `PUT /v1/goals/{id}` may keep the account the goal is already linked to, and `PUT /v1/debts/{id}` may keep the account the debt already has |
| `validation.sameAccount` | transfer `toAccountId` equals `fromAccountId`, or a contribution `fromAccountId` equals the account the goal is linked to |
| `validation.positiveAmount` | transfer `fromAmount`, transaction `amount`, budget `limit`, goal `target`, contribution `amount`, debt `amount`, repayment `amount`, group expense `amount` or group repayment `amount` is 0 or negative, or the converted amount rounds to 0 (reported under `rate`: the `accountAmount` of a debt, the `debtAmount` or `accountAmount` of a repayment, the `groupAmount` of a group expense or group repayment) |
| `validation.categoryNameTaken` | `POST`/`PUT /v1/categories`: another category of the same kind has this name, compared case-insensitively after trimming (field `name`). `POST /v1/groups/{id}/categories`: another category of the group has this name, or it is the name of a standard category in any of en, pl, ru and uk, compared the same way (field `name`). Checked before `tooManyCategories` |
| `validation.categoryKindLocked` | `PUT /v1/categories/{id}`: the kind of a system category, or of a category that is in use, cannot change (field `kind`) |
| `validation.systemCategory` | `DELETE /v1/categories/{id}`: a system category cannot be deleted (field `id`) |
| `validation.unknownCategory` | transaction or budget `categoryId` is not one of the user's categories, is not a GUID, or is the `transfer` category. Also returned when the category was deleted at the same moment. Group expense `categoryId` is neither one of the 9 standard keys (lower case, case-sensitive: `Groceries` and `transfer` are refused) nor a GUID (checked with the shape of the body), or is a GUID that is not a category of this group (checked after the group lookup, together with the member references) |
| `validation.categoryKindMismatch` | transaction `categoryId` has a different kind than the transaction `kind`. Checked only when `kind` itself is valid |
| `validation.titleTooLong` | transaction `title`, or group expense `description`, is longer than 100 characters after trimming |
| `validation.noteTooLong` | transaction, debt, repayment or group repayment `note` is longer than 200 characters after trimming (transfers report this as `validation.invalidValue`) |
| `validation.tagTooLong` | a transaction or group expense tag, or the `name` of `PUT /v1/tags/{id}`, is longer than 30 characters after trimming (field `tags` or `name`) |
| `validation.tooManyTags` | transaction or group expense `tags` holds more than 10 distinct names, compared case-insensitively (field `tags`) |
| `validation.tagNameTaken` | `PUT /v1/tags/{id}`: another tag of the user has this name, compared case-insensitively after trimming (field `name`) |
| `validation.expenseCategoryOnly` | `POST`/`PUT /v1/budgets`: `categoryId` is an income category (field `categoryId`) |
| `validation.budgetExists` | `POST`/`PUT /v1/budgets`: the category already has a budget in this period. Also returned when another request created it at the same moment. Checked only when `period` is valid (field `categoryId`) |
| `validation.nothingToCopy` | `POST /v1/budgets/copy`: the previous period has no budget to copy, or every category of it already has a budget in the target period (field `period`) |
| `validation.savingsAccountOnly` | `POST`/`PUT /v1/goals`: `accountId` is not a savings account (field `accountId`). Not checked when `PUT` keeps the account the goal is already linked to |
| `validation.deadlinePast` | `POST`/`PUT /v1/goals`: `deadline` is before the current month (UTC). Not checked when `PUT` keeps the stored deadline (field `deadline`) |
| `validation.dueBeforeDate` | `POST`/`PUT /v1/debts`: `dueDate` is before `date`. Checked only when `date` is valid (field `dueDate`) |
| `validation.exceedsRemaining` | `POST /v1/debts/{id}/repayments`: the repayment, converted into the debt currency (`debtAmount`), is above the remaining amount of the debt. Also returned for a settled debt (field `amount`) |
| `validation.belowRepaid` | `PUT /v1/debts/{id}`: `amount` is below the amount already repaid (field `amount`) |
| `validation.debtCurrencyLocked` | `PUT /v1/debts/{id}`: the currency of a debt that has a repayment cannot change (field `currency`) |
| `validation.memberNameTaken` | `POST /v1/groups`: two names in `members` are equal, compared case-insensitively after trimming (field `members`). `POST /v1/groups/{id}/members` and `PUT /v1/groups/{id}/members/{memberId}`: another member of the group has this name, compared the same way (field `name`). It is the last rule of `members` on `POST /v1/groups` (after `tooManyMembers`) and is checked before `tooManyMembers` on `POST /v1/groups/{id}/members` |
| `validation.tooManyMembers` | `POST /v1/groups`: `members` holds more than 50 names (field `members`). `POST /v1/groups/{id}/members`: the group already has 50 members (field `name`), checked only after `memberNameTaken`. On `POST /v1/groups` it comes after `nameTooLong` and before `memberNameTaken` |
| `validation.memberInUse` | `DELETE /v1/groups/{id}/members/{memberId}`: a history event of the group names the member, as its actor or in its state (payer, share, either side of a repayment), deleted entries and earlier states included (field `memberId`) |
| `validation.unknownMember` | a member reference of a group expense or group repayment is not a GUID, or is not a member of this group: `payerId`, a member of `split` (field `split`), `fromMemberId` or `toMemberId`. A value that is not a GUID, including an exact share without `memberId`, is reported with the shape of the body (but a `split` that also fails a shape check, such as a repeated member, answers `invalidValue` alone), an unknown GUID after the group lookup, together with the other references |
| `validation.sameMember` | group repayment `toMemberId` equals `fromMemberId` (field `toMemberId`). Checked only when both are GUIDs |
| `validation.splitMismatch` | group expense `split` is `exact` and the share amounts do not add up to the expense `amount`, including a share above the amount (field `split`). Checked only when `amount` is a valid positive amount and the shape of `split` is valid |
| `validation.groupCurrencyLocked` | `PUT /v1/groups/{id}`: the main currency of a group that has an expense or a repayment, deleted ones included, cannot change (field `currency`). Sending the same currency is fine |
| `validation.tooManyCategories` | `POST /v1/groups/{id}/categories`: the group already has 100 categories of its own (field `name`), checked only after `categoryNameTaken` |

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
| 503 | `rates.unavailable` (title "Service Unavailable") | `POST /v1/transfers`, `POST`/`PUT /v1/transactions`, `PUT /v1/settings`, `GET /v1/budgets`, `POST /v1/budgets`, `PUT /v1/budgets/{id}`, `GET /v1/goals`, `PUT /v1/goals/{id}`, `POST /v1/goals/{id}/contributions`, `GET /v1/debts`, `POST /v1/debts`, `PUT /v1/debts/{id}`, `POST /v1/debts/{id}/repayments`, `POST /v1/groups/{id}/expenses`, `PUT /v1/groups/{id}/expenses/{expenseId}`, `POST /v1/groups/{id}/repayments`, `PUT /v1/groups/{id}/repayments/{repaymentId}`, `GET /v1/groups/{id}/balances` with a `currency` other than the main currency of the group, `DELETE /v1/categories/{id}`, `GET /v1/overview` | a conversion that needs an exchange rate that is not stored yet, or whose newest stored rate is older than 30 days (see Rates): a cross-currency transfer, transaction, contribution, debt with an account, repayment, group expense or group repayment without a manual `rate` (a transaction may also need the rate between the account and base currencies, a contribution the rate between the linked account and base currencies, a repayment into or out of an account in a third currency the rate between the repayment and account currencies), or a base currency change while some currency used by a transaction, budget, goal or contribution has no stored rate at all. Nothing is written. Retry later; transfers, transactions, contributions, debts, repayments, group expenses and group repayments can also send a manual `rate` (only for the entered-to-account conversion of a transaction, a contribution or a debt, for the repayment-to-debt conversion of a repayment and for the conversion into the main currency of a group entry, never for the account-to-base one or for a repayment's account in a third currency). A stale rate is refused only on the automatic conversions of transfers, transactions, contributions, debts, repayments and group entries. The base currency change and every read value stored data with the latest stored rate whatever its age. On the budget and goal reads, `POST /v1/budgets`, `PUT /v1/budgets/{id}`, `PUT /v1/goals/{id}` and `DELETE /v1/categories/{id}` (a budget merge that needs a rate) the case cannot happen once the settings check passed and is kept so that it never becomes a 500. On `GET /v1/overview` it is returned when the balance, a budget, a goal or an open debt needs a rate that is not stored. On `GET /v1/debts` it is returned when an open debt needs a rate that was never stored, whatever the `status` filter, because every page carries the summary (see Debts). On a group expense or group repayment write it is returned when the entry currency differs from the main currency of the group and no stored rate of the entry `date` is usable for the entry currency or the main currency, after the member and category references passed (see Expense groups). On `GET /v1/groups/{id}/balances?currency=X` it is returned when X or the main currency of the group has no stored rate at all, also for a group without entries: a read uses the latest stored rate whatever its age |

### Rate limiting and lockout

Requests are counted per client IP address per minute, in two separate buckets:

- `POST /v1/auth/register`, `POST /v1/auth/login` and `DELETE /v1/me` share a tight limit against password guessing (`RateLimiting:AuthPermitLimit`, 10 by default).
- `POST /v1/auth/refresh` and `POST /v1/auth/logout` have their own, larger limit (`RateLimiting:SessionPermitLimit`, 60 by default), so failed logins never block a refresh.

A 429 from refresh means "wait for `Retry-After` seconds and retry", never "logged out".

After 5 failed password attempts the account is locked for 15 minutes. Failures are counted together on login and on `DELETE /v1/me`. A locked account answers like a wrong password.

## Accounts, transfers and rates

All amounts are integers in the minor units of their currency, as ISO 4217 defines them: grosze and cents for most currencies, whole yen for JPY, thousandths of a dinar for KWD. A currency has 0, 2 or 3 fraction digits:

- 0 digits (16 currencies): `BIF`, `CLP`, `DJF`, `GNF`, `ISK`, `JPY`, `KMF`, `KRW`, `PYG`, `RWF`, `UGX`, `VND`, `VUV`, `XAF`, `XOF`, `XPF`;
- 3 digits (7 currencies): `BHD`, `IQD`, `JOD`, `KWD`, `LYD`, `OMR`, `TND`;
- 2 digits: every other currency, including PLN, EUR, USD and UAH.

The absolute value of an amount is at most 100000000000 minor units of its own currency: 1000000000.00 in a 2-digit currency, 100000000.000 in KWD and 100000000000 in JPY. JSON numbers with a zero fraction, such as `5.0`, count as integers. Every account and transfer belongs to the current user, and an id of another user answers exactly like an unknown id (404).

Validation reports the first failing rule for each field and returns all failing fields in one 400. A body field of the wrong JSON type gives a field error, for example `openingBalance: ["validation.invalidValue"]`, not an error under `body`. A whitespace-only string counts as missing (`validation.required`).

Validation runs before the lookup. An invalid body sent to an unknown account id answers 400, and only a valid body gives 404 for an unknown or foreign id.

Currencies and conversion:

- `Currency` is one of 148 codes: PLN, EUR, USD, UAH and the 144 other currencies of NBP tables A and B, listed in the type above. XDR (the IMF unit of account) is not one of them. A code is exactly three upper-case ASCII letters, and anything else is `validation.invalidValue`. Accounts and the entered currency of a transaction accept every code.
- The base currency stays one of PLN, EUR, USD and UAH. `PUT /v1/settings` with any other code, supported or not, answers `baseCurrency: ["validation.invalidValue"]`.
- A rate is always per major unit, as NBP publishes it: 1 JPY = 0.024764 PLN is the rate `0.024764`. Minor units are converted with `round(minor * rate * 10^(exponent(to) - exponent(from)))`, where the exponent is the number of fraction digits above and `round` is half away from zero to a whole minor unit of the target currency. So 1500 JPY at 0.024764 into PLN is 3715 grosze (37.15 PLN), and 1250 KWD minor units (1.250 KWD) at 12.6221 into PLN are 1578 grosze.
- The rate rounding rule: rates are rounded to 6 decimals, half away from zero. A rate below 0.1 keeps one more decimal for every zero after the decimal point (6 significant digits, at most 12 decimals): `0.024764` stays, `0.00000212` stays and `1.2345678` becomes `1.234568`. The rule applies to stored NBP values, to cross rates and to manual rates, so a tiny rate such as IRR or VND against PLN never collapses to 0.
- A manual `rate` is a JSON number with `0 < rate <= 1000000000` (KWD to IRR is about 6000000). It is rounded by the rule above before it is used and stored.
- The amount a write produces in the target currency (`toAmount` of a transfer or contribution, `accountAmount` of a transaction or a debt, `debtAmount` and `accountAmount` of a repayment) is held to the same limit, in minor units of that currency. A larger product, or one too large to represent at all, answers `rate: ["validation.invalidValue"]` and never a 500.

### Accounts

- `GET /v1/accounts`: `pageSize` defaults to 100 (maximum 100). Accounts are ordered by creation time.
- `balance` is `openingBalance` plus the sum of the account's transfer legs, transactions (`+accountAmount` for income, `-accountAmount` for expense), debt legs (`-accountAmount` for a lent debt, `+accountAmount` for a borrowed one) and repayment legs (`+accountAmount` for a repayment of a lent debt, `-accountAmount` for a repayment of a borrowed one), computed by the server. `inUse` is true when the account has any transfer, transaction, debt or repayment. `linkedGoals` is the number of the user's goals linked to the account. Deleting an account that has no history but is linked to goals answers 204 and unlinks them (`accountId: null`).
- `DELETE` removes an account that has no history. An account in use answers 400 `id: ["validation.accountInUse"]`: archive it with `PUT` and `archived: true` instead. Deleting a missing id answers 404.
- `PUT` replaces the editable fields. The currency of an account in use (with a transfer, a transaction, a debt or a repayment) cannot change (`validation.currencyLocked`).
- `cardLast4` is validated for every type but stored only for `card`; other types answer `null`.

### Transfers

- `GET /v1/transfers`: `pageSize` defaults to 20 (maximum 100). `accountId` matches transfers on either side. A value that is not a GUID answers 400 `accountId: ["validation.invalidValue"]`. Transfers are ordered newest first (`date`, then creation time).
- Transfers cannot be edited or deleted.
- For accounts in the same currency the `rate` is 1 and `toAmount` equals `fromAmount`. Otherwise the server uses the manual `rate` when sent, and the exchange rate of the transfer `date` when not. `toAmount` is `fromAmount` converted with the formula above, rounded to a whole minor unit of the target currency (half away from zero). A manual rate is rounded by the rate rounding rule first.
- A manual `rate` must be a JSON number with `0 < rate <= 1000000000`. A converted amount that rounds to 0 answers `rate: ["validation.positiveAmount"]`. A converted amount above 100000000000 minor units of the target currency answers `rate: ["validation.invalidValue"]`.
- A transfer stores its `rate` and `toAmount`. They never change when exchange rates change later.
- Without a manual rate, a transfer between two currencies needs a usable rate for each of them (PLN needs none). When one is not stored, or is stale (see Rates), the answer is 503 with code `rates.unavailable`. A manual rate always works.

### Rates

- Rates come from the National Bank of Poland (NBP) as mid rates against PLN. Table A (31 currencies, published every business day) and table B (the other 116, published weekly) together cover the 147 currencies other than PLN. XDR, which NBP lists in table A, is ignored without a warning. Any other code that is not supported (for example a currency NBP adds later) is skipped, with one warning in the log per code per sync.
- A background job in the API loads the tables, table A first and then table B, on start and every 6 hours, and requests never call NBP. Each table has its own start: the day after the latest stored date among its currencies, or the whole history window (`Rates:HistoryDays`, 183 days) when any currency of the table has no stored rate yet. The first run therefore backfills about 183 days of both tables, and the first run after an upgrade backfills the currencies that were added. One table failing does not stop the other. If NBP is unreachable, the API keeps serving the stored rates.
- `GET /v1/rates` returns a plain array, not a `Page`. This is the one exception to "every collection is paginated": there are at most 147 pairs.
- `date`, `from` and `to` are optional, except that at least one of `from` and `to` is required: without both the answer is 400 `to: ["validation.required"]`. An omitted `date` means today's UTC date, and every `Rate` echoes the requested or resolved date in `date`. With only `from` the array holds that currency against every other one, with only `to` every other currency against it, and with both at most one pair. A pair with `from == to` is never returned.
- For a date, the rate of each currency is the latest stored one on or before that date. For weekends, holidays and future dates that is the latest earlier rate, and for a date before the history start it is the earliest stored rate of that currency. Table B is weekly, so on any day of the week a table B currency reads the rate of its latest publication on or before that day. Cross rates go through PLN and are rounded by the rate rounding rule.
- A rate older than 30 days counts as missing when it is used by `GET /v1/rates` and by the automatic conversions of writes (transfers, transactions, goal contributions, debts with an account and repayments without a manual rate, the conversion of a repayment into an account in a third currency, every account-to-base conversion, and group expenses and group repayments in another currency than the main currency of the group without a manual rate). The age is measured against `min(date, today)`: a rate dated more than 30 days before that day is stale, and one dated exactly 30 days before is not. The earliest-rate fallback for a date before the history start is never stale. A currency that NBP stopped publishing therefore leaves `GET /v1/rates` and answers 503 `rates.unavailable` on automatic conversions once its last rate is older than 30 days, while a manual `rate` still works for the entered-to-account step.
- The staleness rule never applies to reads that value stored data: the overview balance, budgets, goals, the debt summary (`summary` of `GET /v1/debts` and `Overview.debts`), the base currency change, copying budgets, moving budgets when a category is deleted and the group balances shown in another currency use the latest stored rate whatever its age, so a delisted currency never breaks a read.
- Pairs where a currency has no usable rate (not stored, or stale) are omitted, so the array can be empty right after the first start.
- Today's rates are cached for up to an hour, are the same for every user, and the sync invalidates them; any other date is read from the database on every request.

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
- Filters, all optional and combined with AND: `kind`, `categoryId`, `accountId`, `tagIds`, `from` and `to` (both inclusive, `YYYY-MM-DD`) and `search`. An unknown `kind` or `sort`, a `from` or `to` that is not a real date, or a `categoryId` or `accountId` that is not a GUID answers 400 with `validation.invalidValue` under the parameter name. `tagIds` is a comma-separated list of at most 10 tag ids (see Tags), and a list with a non-GUID element or with more than 10 elements answers `tagIds: ["validation.invalidValue"]`. `from` later than `to` is not an error and gives an empty page. The server does not know financial periods: the client turns a period into `from` and `to`.
- `search` is trimmed and compared case-insensitively as literal text, so `%`, `_` and `\` are plain characters. A transaction matches when its `title`, its `note`, the name of its category or the name of one of its tags contains the text. When the text also looks like an amount (digits, optionally followed by `.` or `,` and up to three digits), a transaction matches too when its entered `amount` written with the fraction digits of its entered currency starts with it, reading a comma as a dot: `1500` for a 0-digit currency such as JPY, `12.500` for a 3-digit one such as KWD and `12.50` for the rest. For example `12,5` matches 1250 PLN minor units (`12.50`) and 12500 (`125.00`) does not, `12.5` matches 12500 KWD minor units (`12.500`), and `15` matches 1500 JPY. An empty search is ignored.
- `sort` is one of `-date` (default), `date`, `-amount` and `amount`. `-date` is newest first: `date`, then creation time, then `id`, all descending. `date` is the same order reversed. `amount` sorts by `baseAmount` (not by the entered amount), ascending for `amount` and descending for `-amount`, and ties fall back to the `-date` order. Editing a transaction keeps its creation time.
- `sum` is the signed total in the base currency over every transaction that matches the filters, not only the page: `+baseAmount` for income and `-baseAmount` for expense. It is 0 for an empty result. `currency` is the base currency the `sum` and every `baseAmount` are in.
- `POST` and `PUT` take the account, the category and the entered `amount` and `currency`. The server stores `rate`, `accountAmount` and `baseAmount` with the transaction:
  - `rate` is 1 when `currency` equals the account currency (a sent `rate` is still validated, then ignored). Otherwise it is the manual `rate` rounded by the rate rounding rule when sent, and the exchange rate of the transaction `date` when not.
  - `accountAmount` is `amount` converted into the account currency with the formula above, rounded half away from zero to a whole minor unit of the account currency. A 1500 JPY expense on a PLN account at 0.024764 stores `accountAmount` 3715.
  - `baseAmount` is `amount` when `currency` is the base currency. Otherwise it is `accountAmount` converted from the account currency into the base currency at the exchange rate of the `date`, rounded the same way.
  - A converted `accountAmount` that rounds to 0 answers `rate: ["validation.positiveAmount"]`, and one above 100000000000 minor units of the account currency answers `rate: ["validation.invalidValue"]`.
  - A manual `rate` must be a JSON number with `0 < rate <= 1000000000`. It replaces only the entered-to-account rate: the account-to-base rate is always the exchange rate of the `date`.
  - A rate that is needed but not stored, or is stale (see Rates), answers 503 `rates.unavailable`.
- `tags` is optional and takes tag names (see Tags). `PUT` replaces the whole set, as it does for `title`, so a `PUT` without `tags` removes the tags of the transaction.
- `title` is optional, stored trimmed, and is `""` when missing or blank. `note` is stored trimmed and is `null` when missing or blank. The limits are 100 and 200 characters after trimming (`validation.titleTooLong`, `validation.noteTooLong`).
- `categoryId` must be a category of the user that is not the `transfer` category, and its kind must equal the transaction `kind` (`validation.categoryKindMismatch`). `accountId` must be an account of the user and not archived, except that `PUT` may keep the account the transaction already has even when it was archived since.
- A stored transaction never changes when exchange rates change later. Only a change of the base currency rewrites `baseAmount`: `PUT /v1/settings` with a new `baseCurrency` recomputes `baseAmount` of every transaction of the user at the exchange rate of its own date, exactly as a new transaction would be priced (same formula and rate rounding, but a stored rate is used whatever its age), and the `sum`, the `amount` sort and every `baseAmount` are then in the new currency. If a currency used by the transactions has no stored rate at all, the answer is 503 `rates.unavailable` and nothing changes.
- `GET /v1/transactions/{id}` answers 404 for an unknown or foreign id. `POST` answers 201 with `Location: /v1/transactions/{id}`, `PUT` 200 and `DELETE` 204.
- Transactions change `balance` and `inUse` of the account and `inUse` of the category. Deleting a transaction restores the balance.

### Tags

Every tag belongs to the current user, and an id of another user answers exactly like an unknown id (404). There is no endpoint to create a tag: a tag is created by name the first time a transaction is saved with it.

- A tag name is trimmed and has 1 to 30 characters, with no control characters. Spaces inside the name are kept as typed, so `Road  trip` and `Road trip` are two tags.
- Names are unique per user, case-insensitively. The first spelling the user typed is stored. When a transaction is saved with a name that exists in another case (`GROCERIES-run` for `groceries-run`), the stored tag and its spelling are reused and no second tag is created.
- A transaction has at most 10 tags. Names that differ only in case count once, and duplicates in one request are merged without an error.
- `POST` and `PUT /v1/transactions` take `tags` as an array of names, never ids. A missing or `null` `tags` means no tags. On `PUT` the array replaces the previous set, and `[]` or a missing `tags` removes all tags.
- Every `Transaction` carries `tags`, ordered by lower-case name, then `id`, and `[]` when there are none.
- `GET /v1/transactions?tagIds=<id>,<id>` keeps the transactions that have any of the listed tags, combined with the other filters by AND. A transaction with several of the listed tags is returned once, and `sum` and `total` count it once. An id that is unknown or belongs to another user matches nothing and is not an error.
- `search` also matches a transaction when the name of one of its tags contains the text, by the same literal, case-insensitive rule as the category name.
- `GET /v1/tags`: `pageSize` defaults to 100 (maximum 100). Tags are ordered by lower-case name, then `id`. The optional `search` is trimmed and matched case-insensitively as literal text against the name, so `%`, `_` and `\` are plain characters. `usage` is the number of the user's transactions with the tag, and is 0 for a tag no transaction uses.
- `PUT /v1/tags/{id}` renames a tag and answers 200 with the `Tag`. When another tag of the user already has the name, compared case-insensitively, the answer is 400 `name: ["validation.tagNameTaken"]`: tags are never merged. Changing only the case of the tag's own name is allowed.
- `DELETE /v1/tags/{id}` removes the tag from every transaction and answers 204.
- A tag that no transaction uses any more is kept, with `usage` 0, and is still listed until it is deleted.
- Tags are not cached.

## Budgets and goals

Every budget and goal belongs to the current user, and an id of another user answers exactly like an unknown id (404). The validation rules above apply: the first failing rule per field, all failing fields in one 400, a whitespace-only string counts as missing, and validation runs before the lookup, so an invalid body sent to an unknown id answers 400. Ids in paths must be GUIDs, any other value answers 404. Budgets and goals are never cached, and "today" is the UTC date of the server.

### Financial periods

- A period is a `YYYY-MM` key between `1970-01` and `2099-12`. Its first day depends on `monthStartDay` of the settings: that day of the key's month (1 to 28), or with `lastWorkingDay` the last Monday to Friday day of the previous month. It lasts until the day before the start of the next key. For example with `monthStartDay` 5, period `2026-10` runs from 2026-10-05 to 2026-11-04.
- The period of a date is found by trying the month of the date, then the previous one, then the next one. `GET /v1/budgets` without `period` shows the period of today.
- The server tells the client the range: `BudgetPage` carries `period`, `from` and `to`. Changing `monthStartDay` moves the ranges of existing budgets, and their keys stay.
- `spent` counts the expense transactions of the budget category dated inside the period, by `baseAmount`. Transfers are not counted.

### Budgets

- `GET /v1/budgets`: `pageSize` defaults to 100 (maximum 100). An invalid `period`, `page` or `pageSize` answers 400 `invalidValue` under the parameter name. Budgets are ordered by creation order (a copy keeps the order of its source). `summary` covers every budget of the period, not only the page.
- `limit` is stored in the base currency of the moment it was written. After a base currency change the response shows it converted at the rate of the period start, `round(limit * rate)`, with the rate rounded by the rate rounding rule and the result half away from zero.
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
- A goal with a linked account needs `fromAccountId` (an unarchived account of the user, other than the linked one) and creates a transfer from it to the linked account with `note` equal to the goal name. The rules of transfers apply: the same currency gives rate 1, otherwise the manual `rate` (rounded by the rate rounding rule), otherwise the stored rate of `date`; `rate: ["validation.positiveAmount"]` and `rate: ["validation.invalidValue"]` for a converted amount that rounds to 0 or is too large; 503 `rates.unavailable` when a rate is missing or stale, including the rate between the linked account and the base currency, which a manual `rate` never replaces. The transfer appears in `GET /v1/transfers` and changes both balances.
- `saved` grows by the amount received by the linked account, valued in the base currency at the rate of `date`.
- If the goal is linked to another account or unlinked by another request while the contribution runs, nothing is written and the answer is 409 `goal.concurrentUpdate`.

## Debts

A debt is money the user lent to or borrowed from a person. Debts are private: every debt and repayment belongs to the current user, and an id of another user, for a debt or a repayment, answers exactly like an unknown id (404). An account of another user is `validation.unknownAccount`, and lists and the summary never include the rows of another user. The validation rules above apply: the first failing rule per field, all failing fields in one 400, a whitespace-only string counts as missing, and validation runs before the lookup, so an invalid body sent to an unknown debt id answers 400 and only a valid body gives 404. Ids in paths must be GUIDs, any other value answers 404. Debts are never cached, and "today" is the UTC date of the server.

### Debt records

- `direction` is `lent` (money left the user, shown as "owed to me") or `borrowed` (money came to the user, shown as "I owe"). `counterparty` is free text of 1 to 40 characters after trimming with no control characters, stored trimmed. It is not a contact: `Max` and `max` are two people, and there are no per-person totals.
- A debt moves money only when it has an `accountId`. A lent debt adds `-accountAmount` to the balance of the account and a borrowed debt `+accountAmount`, like the legs of a transfer. A repayment of a lent debt adds `+accountAmount` to the account it names and a repayment of a borrowed debt `-accountAmount`. These legs count in `Account.balance` and `inUse`, and in the `validation.currencyLocked` and `validation.accountInUse` checks (see Accounts), and they are not listed by `GET /v1/transfers`: debt movements are listed through the debt endpoints. A repayment leg follows the current `direction` of its debt.
- `currency` is any of the 148 codes, and the account is optional. With an account, `accountAmount` is `amount` converted into the account currency as a transaction is: `rate` is 1 for the same currency, otherwise the manual `rate` (rounded by the rate rounding rule) when sent, and the exchange rate of the debt `date` when not, under the 30-day staleness limit (503 `rates.unavailable`, nothing is written). A converted `accountAmount` that rounds to 0 answers `rate: ["validation.positiveAmount"]`, and one above 100000000000 minor units of the account currency answers `rate: ["validation.invalidValue"]`. Without an account, `accountId`, `accountAmount` and `rate` are `null`, and a sent `rate` is validated and then ignored.
- `accountId` must be one of the user's accounts (`unknownAccount`) and not archived (`archivedAccount`). `PUT` may keep the account the debt already has even when it was archived since.
- A debt stores its `rate` and `accountAmount`, and they never change when exchange rates change later. No amount in the base currency is stored: the base currency only matters for the summary, which is computed at read time. A base currency change therefore rewrites nothing, and `PUT /v1/settings` does not look at the currencies of debts, as it does not look at the currencies of accounts.
- `dueDate` is optional and must be a real date not before `date` (`dueBeforeDate`, checked only when `date` is valid). A debt is `overdue` when it is open and its `dueDate` is before today (UTC): a debt due today is not overdue yet, and a settled debt never is. Repayment dates are not compared with the debt `date`.
- `repaid` is the sum of the `debtAmount` of the repayments and `remaining` is `amount - repaid`. `status` is `open` while `remaining` is above 0 and `settled` at 0, computed and never stored.
- `POST` answers 201 with `Location: /v1/debts/{id}`. There is no `GET /v1/debts/{id}`.
- `PUT` answers 200 with the debt and replaces every field, so the account amount is re-priced with the rules of `POST`, as `PUT /v1/transactions` does: the manual `rate` when sent, otherwise the exchange rate of `date` for an account in another currency than the debt, which answers 503 when that rate is stale. A client that wants to keep the stored rate sends it back as the manual `rate`.
  - The currency cannot change once the debt has a repayment (`currency: ["validation.debtCurrencyLocked"]`), and `amount` cannot go below `repaid` (`amount: ["validation.belowRepaid"]`). Sending the same currency is fine. When both apply they are reported together in one 400.
  - A failure of the account conversion (503 `rates.unavailable`, or a `rate` 400 for a converted amount that rounds to 0 or is too large) answers alone, without `belowRepaid` or `debtCurrencyLocked`: those two are checked only once the conversion succeeded.
  - `direction` may change even when repayments exist. Every repayment leg then follows the new direction, so the balance of the accounts they touched changes accordingly.
- `DELETE` answers 204, deletes the debt together with its repayments and so restores every balance their legs had changed. Deleting an unknown or foreign id answers 404.

### Listing and summary

- `GET /v1/debts`: `status` is `open` (default) or `settled`, case-sensitive. Any other value, including an empty one, answers 400 `status: ["validation.invalidValue"]`, as an invalid `page` or `pageSize` does under its parameter name. `pageSize` defaults to 100 (maximum 100), and `total` counts the debts of the chosen status.
- Open debts are ordered by `dueDate` ascending with the debts that have no due date last, then `date` descending, creation time descending and `id` descending. Settled debts are ordered by `date`, creation time and `id`, all descending. Editing a debt keeps its creation time.
- `currency` is the current base currency and `summary` is in it. The summary covers every open debt of the user, whatever the `status` filter and the page, so it is the same on every page. It is computed over open debts only: `owedToMe` (lent) and `owedByMe` (borrowed) are the remaining amounts summed per currency, each sum is converted into the base currency at today's latest stored rate whatever its age (a read, so the 30-day limit does not apply) and rounded half away from zero, and the converted sums are added. `open` counts the open debts and `overdue` the overdue ones. A user without open debts gets zeros. The same object is `Overview.debts`.
- When the currency of an open debt has no stored rate at all, the answer is 503 `rates.unavailable`, as the overview balance does. Because every page carries the summary, this includes `status=settled`. A settled debt needs no rate. After a base currency change this can happen until the rate sync has stored the missing rate.

### Repayments

- `POST /v1/debts/{id}/repayments` answers 200 with the updated `Debt`, not with the repayment, and no `Location` header. An unknown or foreign debt answers 404 once the body validated. `date` must not be later than today plus one year (`dateTooFar`).
- A repayment may be in any currency. `rate` is the rate from the repayment currency to the debt currency: 1 for the same currency, otherwise the manual `rate` (rounded by the rate rounding rule) when sent, and the exchange rate of the repayment `date` when not, under the 30-day staleness limit (503 `rates.unavailable`). `debtAmount` is `amount` converted with it and rounded half away from zero to a whole minor unit of the debt currency, so 50 EUR at 4.2563 take 212.82 off a PLN debt.
- The account is optional. It receives (lent debt) or pays (borrowed debt) `accountAmount`, which is `amount` when the account is in the repayment currency, `debtAmount` when it is in the debt currency, and otherwise `amount` converted into the account currency at the exchange rate of the repayment `date`. A manual `rate` never replaces that last conversion, and its stale or missing rate answers 503. Without an account, `accountId` and `accountAmount` are `null`.
- A `debtAmount` or `accountAmount` that rounds to 0 answers `rate: ["validation.positiveAmount"]`, and one above 100000000000 minor units `rate: ["validation.invalidValue"]`.
- A repayment cannot take more than the remaining amount: a `debtAmount` above `remaining` answers 400 `amount: ["validation.exceedsRemaining"]` and writes nothing, also for a settled debt. Repaying exactly the remaining amount settles the debt, which then is no longer overdue. An unknown or archived account, a rate failure and a converted amount of 0 or too large are reported before `exceedsRemaining`.
- A repayment stores its `rate`, `debtAmount` and `accountAmount`, and they never change when exchange rates change later. Repayments are never edited: delete the repayment and add it again.
- `GET /v1/debts/{id}/repayments` answers a `Page<DebtRepayment>`, newest first (`date`, then creation time, then `id`, all descending). `pageSize` defaults to 100 (maximum 100). An invalid `page` or `pageSize` answers 400 `invalidValue` before the lookup, and an unknown or foreign debt answers 404.
- `DELETE /v1/debts/{id}/repayments/{repaymentId}` answers 204, removes the repayment, gives its `debtAmount` back to `remaining` (a settled debt becomes open again) and restores the balance of its account. A repayment that does not exist, belongs to another debt or to another user, and an unknown or foreign debt, all answer 404.

### Debts in reports and totals

- `Overview.balance` counts what lies on the accounts, including the legs of debts and repayments, but not the amounts still owed. A client that shows the balance with debts computes `balance.amount + debts.owedToMe - debts.owedByMe`.
- Debts, repayments and their account legs are not transactions. Reports, budgets, `Overview.income`, `Overview.expense`, `transactionCount`, `spending`, `recent` and `hasTransactions` never see them, so lending, borrowing or repaying never shows up as income or expense. They change only `Account.balance` (and so `Overview.balance`), `inUse` of the account and `Overview.debts`.

## Expense groups

A group shares spending between named people: members are plain names, not users. Expenses are paid by one member and split between members, repayments move money from one member to another, and the balances say who owes whom. In this release only the user who created a group (its owner) reads or changes it: every group route needs a valid access token, and a group of another user, an unknown group and an entry id that belongs to another group all answer exactly like an unknown id (404). `DELETE /v1/me` deletes the groups of the user with everything in them.

The validation rules above apply: the first failing rule per field, all failing fields in one 400, a whitespace-only string counts as missing. Ids in paths must be GUIDs, any other value answers 404. Groups are never cached, and "today" is the UTC date of the server. Groups never touch accounts, budgets, reports or the overview: a group entry is not a transaction, moves no money on any account, and `Account.balance`, `inUse`, `Overview` and `Report` never see it. No endpoint creates a transaction or a debt from a group.

### Order of checks

A group write is checked in this order, and the first stage that fails answers alone:

1. The shape of the request (400, `validation.*` codes under the field names): types, ranges, lengths and the rules of each field. It runs before any lookup, so an invalid body sent to an unknown or foreign group, or to an unknown entry, answers 400 and only a valid body gives 404. Invalid `page`, `pageSize`, `deleted` and `currency` queries are part of this stage too.
2. The group (404), then the entry for `PUT`, `DELETE` and `restore` (404).
3. The references (400): the member and category ids that the body names must belong to this group. Every failing reference is reported together in one 400 (`payerId`, `categoryId`, `split`, `fromMemberId`, `toMemberId`), before any rate is looked at.
4. The rate (503 `rates.unavailable`), then the converted amount (400 under `rate`: `validation.positiveAmount` or `validation.invalidValue`). Nothing is written when any stage fails.

### Groups and members

- `GET /v1/groups` lists the groups of the user, newest created first (creation time, then `id`, both descending). `pageSize` defaults to 100 (maximum 100). `members` is the number of members and `total` the sum of `groupAmount` over the live expenses. `GET /v1/groups/{id}` answers the full `Group`.
- `POST /v1/groups` takes the `name`, the main `currency` and 1 to 50 member names, and answers 201 with the `Group` and `Location: /v1/groups/{id}`. Group names are not unique. Member positions follow the order of the `members` array. The first failing rule answers for `members`, in this order: missing, null, blank or an empty array (`validation.required`); not an array, or an element that is not a string, is blank or contains a control character (`validation.invalidValue`); a name longer than 40 characters (`validation.nameTooLong`); more than 50 names (`validation.tooManyMembers`); two equal names (`validation.memberNameTaken`).
- A member name is 1 to 40 characters after trimming with no control characters, and is stored trimmed. Names are unique per group, compared case-insensitively (`validation.memberNameTaken`), and a group has at most 50 members (`validation.tooManyMembers`). `POST /v1/groups/{id}/members` checks the name before the count: a name that is already taken answers `memberNameTaken` even when the group is full, and only a free name in a full group answers `tooManyMembers` (both under `name`). It adds a member at the end of the member order and answers 201 with the `GroupMember`, no `Location` header, `inUse` false. The member order never changes afterwards.
- `PUT /v1/groups/{id}/members/{memberId}` renames a member (200). Changing only the case of the member's own name is allowed. Past entries refer to the member by id, so they show the new name.
- A member is "in use" (`inUse`) when any history event of the group names it: as its actor, or in the state it records (`payerId`, a `memberId` of `shares`, `fromMemberId` or `toMemberId`). Every create, edit, delete and restore records the full state of its entry, so this covers the payer, the shares and both sides of every expense and repayment, deleted entries included, and every earlier state too: a member that an edit took out of an entry stays in use, so the history keeps its name and an earlier state can always be applied again. A member in use can be renamed but not removed: `DELETE /v1/groups/{id}/members/{memberId}` answers 400 `memberId: ["validation.memberInUse"]` and an unused member answers 204. Removing the last unused member leaves a group without members, in which no entry can be written until a member is added. An unknown member, or a member of another group, answers 404.
- Adding or removing members never changes a past entry: shares are stored per member, so every balance stays as it was. A member added later takes part only in the entries written after it, unless an old entry is edited without `split` (see Expenses).
- `PUT /v1/groups/{id}` replaces `name` and `currency` and answers 200 with the `Group`. `DELETE /v1/groups/{id}` answers 204 and removes the group with its members, categories, expenses, repayments and history.
- The main currency is any of the 148 currencies and every entry is converted into it. It can change only while the group has no expense and no repayment, deleted ones included (`currency: ["validation.groupCurrencyLocked"]`), and `currencyLocked` tells the client whether it can. Sending the group's own currency is always accepted.

### Categories

- A group offers the 9 standard categories, listed first in this order: `groceries`, `transport`, `housing`, `cafes`, `health`, `shopping`, `leisure`, `gifts`, `other`. They are the expense starter categories without `transfer`. Their `id` is the key, `standard` is true and `name` is `null`: the client translates the key. The group's own categories follow in creation order.
- `POST /v1/groups/{id}/categories` adds a category of the group and answers 201 with the `GroupCategory` (its `id` is a GUID string, `standard` false), no `Location` header. The name is 1 to 40 characters after trimming with no control characters, unique per group case-insensitively and different from the name of every standard category in en, pl, ru and uk, compared the same way (`validation.categoryNameTaken`). A group has at most 100 categories of its own (`validation.tooManyCategories`). The name is checked before the count: a name that is already taken (or is a standard name) answers `categoryNameTaken` even when the group already has 100 categories of its own, and only a free name answers `tooManyCategories` (both under `name`). Categories of a group cannot be renamed or deleted in this release.
- An expense stores a standard key or the id of a category of its group, nothing else. Category keys are case-sensitive.

### Expenses

- `payerId` is a member, `amount` an integer in minor units of `currency` (1 to 100000000000), and `date` a real date at most one year after today (UTC) with no lower bound. A `payerId` that is not a GUID answers `validation.unknownMember`, as does a GUID that is not a member of the group.
- `categoryId` is optional: missing, null or blank means `other`. Any other value is a standard key or a GUID: a value that is neither answers `validation.unknownCategory` with the shape of the body, and a GUID that is not a category of this group answers it after the group lookup.
- `tags` follow the rules of transaction tags: names of 1 to 30 characters after trimming with no control characters, at most 10 distinct (case-insensitively). Names are stored trimmed, a repeat in another case is dropped, and the first spelling is kept. There are no shared tag records: a group expense stores plain names, and they are not listed by `GET /v1/tags`.
- `description` is optional, stored trimmed, `null` when blank, at most 100 characters (`validation.titleTooLong`).
- `POST` answers 201 with the `GroupExpense` and no `Location` header. `PUT` replaces every field, so what the body leaves out takes its default again: no `categoryId` means `other`, no `tags` no tags, no `description` none, no `split` an equal split between every member the group has now, and no `rate` a fresh conversion (see Conversion). `createdBy` and `createdAt` never change. A deleted expense cannot be edited: it answers 404 until it is restored.
- `GET /v1/groups/{id}/expenses` answers a `Page<GroupExpense>` of the live expenses, or of the deleted ones with `deleted=true`. `deleted` is `true` or `false`, case-sensitive: missing means `false` and anything else answers 400 `deleted: ["validation.invalidValue"]`.

### Split and rounding

- Without `split` (missing or null) the expense is split equally between every current member. `{ mode: 'equal', memberIds }` splits it equally between those members. `{ mode: 'exact', shares: [{ memberId, amount }] }` gives each listed member an exact amount in the expense currency, each from 1 to `amount`, adding up to `amount` (`split: ["validation.splitMismatch"]`: also for a share above `amount`). The order of `memberIds` and `shares` does not matter, and `GroupExpense.shares` and `GroupExpenseState.shares` always list the members that share in member order. `split` on the expense is the mode that was used (`equal` also for a missing `split`).
- A `split` that is present and not null must be an object, so a blank string, a number or an array is `split: ["validation.invalidValue"]`, as is an unknown `mode`, a missing, empty or longer than 50 `memberIds` or `shares` array, a repeated member and an exact share that is not an object or has no integer `amount` from 1 to 100000000000. A repeated member is detected on the GUID where the value is one (so the same id in another letter case or with surrounding spaces is a repeat), on the trimmed text of any other string, and on the JSON text together with the kind of any other value (so `42` and `42` are a repeat, while `42` and `"42"` are not), and a missing or null `memberId` is never compared with another. A member id that is not a GUID (a non-string, or an exact share without `memberId`) is `split: ["validation.unknownMember"]`, and so is a GUID that is not a member of the group. Shape errors win over member errors: every shape check (types, amounts, count, repeated members) finishes before any `unknownMember`, so a split with an invalid share amount and an unknown member, or one that repeats a member and also holds a value that is not a GUID (`memberIds: [A, A, 42]`), answers `invalidValue`. `splitMismatch` is checked only when the shape is valid and `amount` is a valid positive amount.
- Rounding uses the largest remainder method, so the shares always add up exactly. An equal split allocates `amount` between the members with weight 1 each. The `groupAmount` of the expense is then allocated between the members in proportion to their share `amount`s, and the shares of an exact split are allocated the same way. A remainder minor unit goes first to the member with the largest remainder, ties go to the payer when the payer shares, then to the sharing members in member order. For example 10001 split between Anna, Olya and Max, paid by Max, gives Max 3334, Anna 3334 and Olya 3333. The shares of an expense add up to its `amount` and to its `groupAmount`, and a share can have a `groupAmount` of 0.
- An equal split never stores a share of 0: when `amount` is smaller than the number of members it is split between, the members whose share rounds to 0 minor units of the expense currency get no share at all, and `shares` lists only the members that got at least 1 (still in member order). The remainder rule above decides who keeps a unit, the payer first (when the payer is among the members), then the members in member order, and the `groupAmount` is allocated between the members that remain. For example 2 JPY split equally between Anna, Olya and Max, paid by Olya, gives Olya 1 and Anna 1 and Max no share, and 1 JPY split between the same members, paid by Olya, gives Olya the only share. So `GroupShare.amount` is always 1 or more, and an expense (`201`, never an error) can have fewer shares than the members its `split` names. An exact split is unchanged: each listed member gets the stated amount, from 1 to `amount`.

### Conversion

- Every expense and repayment is converted into the main currency of the group when it is written, and the result is stored with it. `rate` is the rate from the entry currency to the main currency per major unit, as in Accounts, transfers and rates. `groupAmount` is `amount` converted with the formula of that section, half away from zero to a whole minor unit of the main currency.
- For the main currency itself `rate` is 1, `rateManual` false and `groupAmount` equals `amount`: a sent `rate` is validated (a JSON number, `0 < rate <= 1000000000`, a blank string counts as missing) and then ignored. In any other currency the manual `rate`, rounded by the rate rounding rule, is used when sent and `rateManual` is true. Without a manual `rate` the rate is the stored one of the entry `date` (the latest on or before it, crossed through PLN, rounded by the rate rounding rule) under the 30-day staleness limit of writes, and `rateManual` is false. When that rate is missing or stale the answer is 503 `rates.unavailable` and nothing is written.
- A `groupAmount` that rounds to 0 answers `rate: ["validation.positiveAmount"]`, and one above 100000000000 minor units `rate: ["validation.invalidValue"]`.
- An entry stores its `rate`, `rateManual` and `groupAmount`, and they never change when exchange rates change later. Only an edit prices the entry again. `PUT` without `rate` always prices it at the rate of its `date` (503 when that rate is stale), even when the stored rate was manual. A client that wants to keep a manual rate sends it back: the web client resends `rate` only when `rateManual` is true, so an edit of an automatically priced entry follows the rates again. Delete and restore never price anything: a restored entry comes back exactly as it was, even when its rate would be stale today.

### Repayments

- A repayment moves `amount` from member `fromMemberId` to member `toMemberId`: the sender's balance goes up and the receiver's down, as if the sender had paid it. They must be different members of the group (`toMemberId: ["validation.sameMember"]`, checked when both are GUIDs). A member that is not a GUID or not a member of the group answers `validation.unknownMember` under its field, and both fields are reported together.
- `amount`, `currency`, `date`, `rate` and the conversion follow the rules of expenses, with `groupAmount` as the converted amount. `note` is optional, stored trimmed, `null` when blank, at most 200 characters (`validation.noteTooLong`).
- Any positive amount is accepted. A partial repayment is normal, and a repayment above what is owed is not refused: it turns the balance around, and shows in the balances and in the history. Repayments are not linked to expenses.
- `POST` answers 201 with the `GroupRepayment` and no `Location` header, `PUT` replaces every field (200, a missing `rate` prices again at the date's rate) and a deleted repayment cannot be edited (404). `GET /v1/groups/{id}/repayments` lists the live repayments, or the deleted ones with `deleted=true`, as a `Page<GroupRepayment>`.

### Delete, restore, history and actors

- `DELETE` of an expense or a repayment is soft and answers 204: the entry leaves the balances and the `total`, is listed with `deleted=true` and keeps every field. `POST .../restore` answers 200 with the entry exactly as it was, and puts it back into the balances. A deleted entry cannot be edited, so edit it after restoring it. Deleting a deleted entry, restoring a live one, and any id that is not an entry of this group answer 404. Entries are removed for good only together with their group.
- Every create, edit, delete and restore appends one event to the history of the entry: its `action`, the time `at`, the `actor` and the `state` of the entry right after the action (`GroupExpenseState` or `GroupRepaymentState`, the full state and not a difference: the client compares consecutive states). `GET .../history` answers a `Page<GroupEntryEvent<...>>`, newest first (`at`, then `id`, both descending), `pageSize` defaults to 100 (maximum 100). The history of a deleted entry stays readable, and an entry that does not belong to the group answers 404.
- `createdBy` and `createdAt` come from the first event of the entry. `updatedBy` and `updatedAt` come from the newest event after the first one, whatever its action (an edit, a delete or a restore), and are `null` for an entry that was never changed.
- A `GroupActor` is `{ kind: 'owner', memberId: null, name }` in this release, with the current name of the owner. `kind: 'member'` (a member's name and id) is reserved for later releases, and clients should show the `name` of either.
- There is no optimistic concurrency token. When two edits of one entry arrive at the same moment they are applied one after the other, the later one wins, and the earlier state stays in the history, where it can be applied again by editing the entry with it (the members it names stay in the group: see Groups and members).

### Balances and suggested transfers

- `GET /v1/groups/{id}/balances` is read-only and covers the live entries only. For every member, in member order and in the main currency of the group, `paid` is the `groupAmount` of the live expenses the member paid, `share` the member's `groupAmount` shares of the live expenses, `sent` and `received` the `groupAmount` of the live repayments the member sent and received, and `balance` is `paid - share + sent - received`: positive means the others owe the member, negative that the member owes. `total` is the sum of `groupAmount` over the live expenses. The balances of all members add up to exactly 0, and the `paid` column and the `share` column each add up to `total`. A group without entries gets zeros for every member, `transfers: []` and `total` 0.
- `transfers` are the suggested transfers that settle everyone: there is no list of who owes whom per pair. They are computed from the balances in the main currency. First every member with a negative balance, in member order, pays the first member whose balance is exactly the opposite. Then, repeatedly, the member with the largest debt pays the member with the largest credit the smaller of the two amounts, ties going to the earlier member. A transfer is listed in the order it was found. The list has at most one transfer fewer than the members with a non-zero balance and is empty when every balance is 0; in rare cases it has one transfer more than the theoretical minimum. For balances Anna +4232, Olya -698, Max -1767 and Kasia -1767 the transfers are Max to Anna 1767, Kasia to Anna 1767 and Olya to Anna 698.
- Without `currency` the figures are in the main currency (`currency` equals `groupCurrency`). `currency` is any of the 148 codes and shows every figure in it: `paid`, `share`, `sent`, `received`, `balance`, `total` and every transfer `amount` are converted separately from the main currency at today's (UTC) latest stored rate whatever its age (a read, so the 30-day limit does not apply), with the formula of Accounts, transfers and rates (the cross rate through PLN, rounded by the rate rounding rule) and half away from zero. The transfers are chosen in the main currency first and converted afterwards, so the converted figures can miss a zero sum, and a converted `balance` can differ from `paid - share + sent - received` of the converted figures, by a few minor units. With EUR at 4.30 PLN a balance of 4232 cents shows as 18198 grosze. The main currency itself as `currency` converts nothing.
- When the view currency or the main currency has no stored rate at all, the answer is 503 `rates.unavailable`, also for a group without entries. An invalid `currency` (`eur`, ` EUR`, `XDR`, or an empty value) answers 400 `currency: ["validation.invalidValue"]` before the group is looked up, and a group of another user answers 404. The figures are read in one consistent snapshot.

### Ordering and paging

- Every list is a `Page<T>`: `page` is 1-based, `pageSize` defaults to 100 and is at most 100 on every group list, and `total` counts the rows of the list (the live or the deleted ones, as the `deleted` flag says). An invalid `page` or `pageSize` answers 400 `invalidValue` under the parameter name, before the lookup.
- Groups: newest created first (creation time, then `id`, both descending). Expenses and repayments: `date`, then creation time, then `id`, all descending. Editing an entry keeps its creation time, so an edit moves it in the list only when its `date` changes. History: newest event first (`at`, then `id`, both descending).
- Members are in member order (the order of creation, new members last) in `Group.members`, `GroupExpense.shares`, `GroupBalances.members` and everywhere a list of members is shown, and the categories are in the order of `Group.categories`. A group holds at most 50 members and 109 categories (the 9 standard ones and 100 of its own). Both lists are embedded in `Group`, not paginated, and neither is `GroupBalances`, whose `transfers` are fewer than its members: this is an exception to "every collection is paginated", as for rates.

## Overview and reports

`GET /v1/overview` and `GET /v1/reports` are read-only and scoped to the current user. Every amount is in minor units of the current base currency, "today" is the UTC date of the server, and the financial periods follow `monthStartDay` (see Financial periods). Changing the base currency re-prices every figure. Neither answer is cached, because the figures depend on today, the rates and every write to accounts, transfers, categories, transactions, budgets, goals, debts and settings. Neither is paginated: the embedded lists are capped by the contract (5 slices plus the bucket, 5 budgets, 6 transactions, 3 goals, at most 12 report periods), and the report structure lists every expense category with spending because the chart needs all of them.

### Counted transactions

Both endpoints count the transactions of the user dated inside a range (both days inclusive) whose category is not the `transfer` category, by `baseAmount`. Income and expense are summed separately, and `count` is the number of transactions of each kind. Debts and repayments are not transactions and are never counted (see Debts).

### Overview

- `period` is `week`, `month` (default) or `year`, case-sensitive. Any other value, including an empty one, answers 400 `period: ["validation.invalidValue"]`.
- Ranges: `week` is the last 7 days ending today (`from` is today minus 6), and the previous range is the 7 days before it. `month` is the financial period of today, and the previous range is the period before it. `year` is the 12 financial periods ending with the period of today, and the previous range is the 12 periods before them.
- `income.change` and `expense.change` compare the amount with the previous range: `round((current - previous) * 100 / previous)` to one decimal, with halves rounded towards positive infinity, and a drop is negative. They are `null` when the previous amount is 0, and never `-0`. `averageExpense` is `expense.amount / expense.count` rounded half away from zero, `null` without expenses.
- `spending` is the expense per category inside the range, largest first (ties by name), without the `transfer` category. Only categories with spending above 0 are listed. The five largest become slices. Every other category, and the `other` category whatever its size, join one bucket with `categoryId: null` and `name: null`, appended last when its amount is above 0. `percent` is the share of the total with one decimal, split by the largest remainder so that the percents add up to exactly 100.0. Without expenses the list is empty.
- `balance.amount` is the sum of the balances of every account except archived accounts whose balance is 0, each converted into the base currency at today's rate (the latest stored one, however old) and rounded half away from zero. An archived account that still holds money counts. The balances include the debt and repayment legs of the accounts, but not the amounts still owed. `balance.accounts` is the number of counted accounts.
- `debts` is the summary of the user's open debts (`DebtSummary`, see Debts): `owedToMe` and `owedByMe` are the remaining amounts in the base currency at today's latest stored rate, `open` and `overdue` are counts. It does not depend on `period`, and a user without open debts gets zeros.
- `month` is always the financial period of today, whatever the range: `planned` is the sum of the budget limits, `spent` the sum of the spent amounts, `left` their difference, and `budgets` the number of budgets.
- `topBudgets` lists the budgets of that period by usage (`spent / limit`) descending, then by `spent` descending, then by creation order, at most 5. `name` is the category name, and the other fields are those of `Budget`.
- `recent` lists the counted transactions of the range, newest first (`date`, then creation time, then `id`, all descending), at most 6. `amount` is the `baseAmount`.
- `goals` lists the goals that are not reached first, then the reached ones, each group in creation order, at most 3. `goalCount` counts every goal.
- `hasTransactions` is true when the user has any transaction on any date, so the client can tell a new user from an empty range.
- A user with no data gets a complete document with zeros, empty lists and `change: null`.
- 503 `rates.unavailable`: the balance of an account in another currency, a budget, a goal or an open debt needs a rate that is not stored, so nothing can be converted. The case cannot happen when every currency in use has a stored rate. The figures are read in one consistent snapshot.

### Reports

- `range` is `3m`, `6m` (default) or `12m`, case-sensitive. Any other value answers 400 `range: ["validation.invalidValue"]`.
- `periods` are the 3, 6 or 12 financial periods ending with the period of today, oldest first. `from` is the first day of the first period and `to` the last day of the current one. Every period is present, with 0 for income and expense when it has no transactions. A transaction lands in the period that contains its date.
- `totals` are the sums over the periods. `averageIncome` and `averageExpense` are the totals divided by the number of periods and rounded half away from zero.
- `savingsRate` is `(income - expense) / income` in percent with one decimal, rounded half away from zero, and can be negative. It is `null` when the income is 0, and never `-0`.
- `structure` covers the current period only: the expense per category, largest first (ties by name), without the `transfer` category and with the `other` category as a normal item. Only categories with spending above 0 are listed, and there is no top-N cut. `percent` is split like the overview spending, so the percents add up to 100.0, and `total` is the sum of the amounts. Without expenses `items` is empty and `total` is 0.
- Reports read only the stored `baseAmount`, so they never need an exchange rate and never answer 503. The figures are read in one consistent snapshot.
