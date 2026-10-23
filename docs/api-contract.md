# API contract: authentication, users, settings, accounts, transfers, rates, categories, transactions, tags, budgets, goals, debts, overview and reports

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
| `validation.required` | the field is missing, null or blank, including the `name` of `PUT /v1/tags/{id}`. Goal `deadline` and `accountId` and contribution `rate` are optional, and `fromAccountId` is required only for a linked goal. A debt needs `direction`, `counterparty`, `amount`, `currency` and `date`, and a repayment needs `date`, `amount` and `currency`: the other fields of both are optional. `GET /v1/rates` needs at least one of `from` and `to`: when both are missing, `to` is reported (field `to`) |
| `validation.invalidEmail` | the email is not a valid address. On login it is returned only when the email contains a control character |
| `validation.emailTaken` | register, or `PUT /v1/me`: another account uses this email |
| `validation.nameTooLong` | the name is longer than 40 characters after trimming (users, accounts, categories and goals), or the debt `counterparty` is |
| `validation.passwordTooShort` | register: fewer than 10 characters |
| `validation.passwordTooLong` | register: more than 128 characters |
| `validation.invalidPassword` | `DELETE /v1/me`: the password is wrong, or the account is locked after repeated failures (field `password`) |
| `validation.invalidValue` | the value is of the wrong type or outside the allowed set, for example an unknown currency (anything but exactly three upper-case letters naming one of the 148 supported codes, so `jpy`, ` JPY` and `XDR` are refused), a `baseCurrency` other than PLN, EUR, USD or UAH, a `monthStartDay` outside 1 to 28 or a name containing a control character. On accounts and transfers it also covers an amount that is not an integer or is above 100000000000 in absolute value (minor units of its own currency), a `date` that is not a real `YYYY-MM-DD`, a manual `rate` that is not a JSON number, is not greater than 0 or is above 1000000000, a `note` that is not a string or is longer than 200 characters, and a converted transfer amount above 100000000000 minor units of the target account currency (reported under `rate`, also when the product is too large to represent). On categories it covers an unknown `kind` or `color`. On transactions it covers an unknown `kind` or `currency`, a `title` or `note` that is not a string, and the same rules for `amount`, `date`, `rate` and the converted amount as on transfers, and for `tags` a value that is not an array, an element that is not a string, and an element that is blank or contains a control character (checked before `tagTooLong` and `tooManyTags`). On tags it covers a `name` that is not a string or contains a control character. On budgets it covers a `period` that is not a `YYYY-MM` key between 1970-01 and 2099-12 and a `limit` that is not an integer or is above 100000000000. On goals it covers a `name` that is not a string or contains a control character, a `target` that is not an integer or is above 100000000000, and a `deadline` that is not a `YYYY-MM` key or is more than 600 months after the current month. On contributions it covers the same rules for `amount`, `date` and `rate` as on transfers. On debts it covers an unknown `direction` or `currency`, a `counterparty` or `note` that is not a string or contains a control character, a `dueDate` that is not a real `YYYY-MM-DD`, and the same rules for `amount`, `date`, `rate` and the converted `accountAmount` as on transactions. On repayments it covers an unknown `currency`, a `note` that is not a string or contains a control character, and the same rules for `amount`, `date`, `rate` and the converted `debtAmount` and `accountAmount` as on transfers. In queries it is returned under the parameter name for an invalid `page`, `pageSize`, `accountId`, `categoryId`, `tagIds`, `date`, `from`, `to`, `kind`, `sort`, debt `status` (anything but `open` or `settled`, case-sensitive), budget `period`, overview `period` (anything but `week`, `month` or `year`, case-sensitive) or report `range` (anything but `3m`, `6m` or `12m`) |
| `validation.currencyLocked` | `PUT /v1/accounts/{id}`: the currency of an account that is in use (it has a transfer, a transaction, a debt or a repayment) cannot change (field `currency`) |
| `validation.cardLast4` | `cardLast4` is present, not blank and not exactly 4 digits. Checked for every account type |
| `validation.accountInUse` | `DELETE /v1/accounts/{id}`: the account has transfers, transactions, debts or repayments, so it can only be archived (field `id`) |
| `validation.dateTooFar` | transfer, transaction, goal contribution, debt or repayment `date` is later than today plus one year |
| `validation.unknownAccount` | transfer `fromAccountId` or `toAccountId`, transaction `accountId`, goal `accountId`, contribution `fromAccountId`, debt `accountId` or repayment `accountId` is not one of the user's accounts (or not a GUID) |
| `validation.archivedAccount` | transfer `fromAccountId` or `toAccountId`, transaction `accountId`, goal `accountId`, contribution `fromAccountId`, debt `accountId` or repayment `accountId` is an archived account. A contribution to a goal whose linked account is archived answers it under `accountId`. On `PUT /v1/transactions/{id}` the account the transaction already has is allowed even when archived, `PUT /v1/goals/{id}` may keep the account the goal is already linked to, and `PUT /v1/debts/{id}` may keep the account the debt already has |
| `validation.sameAccount` | transfer `toAccountId` equals `fromAccountId`, or a contribution `fromAccountId` equals the account the goal is linked to |
| `validation.positiveAmount` | transfer `fromAmount`, transaction `amount`, budget `limit`, goal `target`, contribution `amount`, debt `amount` or repayment `amount` is 0 or negative, or the converted amount rounds to 0 (reported under `rate`: the `accountAmount` of a debt, the `debtAmount` or `accountAmount` of a repayment) |
| `validation.categoryNameTaken` | `POST`/`PUT /v1/categories`: another category of the same kind has this name, compared case-insensitively after trimming (field `name`) |
| `validation.categoryKindLocked` | `PUT /v1/categories/{id}`: the kind of a system category, or of a category that is in use, cannot change (field `kind`) |
| `validation.systemCategory` | `DELETE /v1/categories/{id}`: a system category cannot be deleted (field `id`) |
| `validation.unknownCategory` | transaction or budget `categoryId` is not one of the user's categories, is not a GUID, or is the `transfer` category. Also returned when the category was deleted at the same moment |
| `validation.categoryKindMismatch` | transaction `categoryId` has a different kind than the transaction `kind`. Checked only when `kind` itself is valid |
| `validation.titleTooLong` | transaction `title` is longer than 100 characters after trimming |
| `validation.noteTooLong` | transaction, debt or repayment `note` is longer than 200 characters after trimming (transfers report this as `validation.invalidValue`) |
| `validation.tagTooLong` | a transaction tag, or the `name` of `PUT /v1/tags/{id}`, is longer than 30 characters after trimming (field `tags` or `name`) |
| `validation.tooManyTags` | transaction `tags` holds more than 10 distinct names, compared case-insensitively (field `tags`) |
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
| 503 | `rates.unavailable` (title "Service Unavailable") | `POST /v1/transfers`, `POST`/`PUT /v1/transactions`, `PUT /v1/settings`, `GET /v1/budgets`, `POST /v1/budgets`, `PUT /v1/budgets/{id}`, `GET /v1/goals`, `PUT /v1/goals/{id}`, `POST /v1/goals/{id}/contributions`, `GET /v1/debts`, `POST /v1/debts`, `PUT /v1/debts/{id}`, `POST /v1/debts/{id}/repayments`, `DELETE /v1/categories/{id}`, `GET /v1/overview` | a conversion that needs an exchange rate that is not stored yet, or whose newest stored rate is older than 30 days (see Rates): a cross-currency transfer, transaction, contribution, debt with an account or repayment without a manual `rate` (a transaction may also need the rate between the account and base currencies, a contribution the rate between the linked account and base currencies, a repayment into or out of an account in a third currency the rate between the repayment and account currencies), or a base currency change while some currency used by a transaction, budget, goal or contribution has no stored rate at all. Nothing is written. Retry later; transfers, transactions, contributions, debts and repayments can also send a manual `rate` (only for the entered-to-account conversion of a transaction, a contribution or a debt and for the repayment-to-debt conversion of a repayment, never for the account-to-base one or for a repayment's account in a third currency). A stale rate is refused only on the automatic conversions of transfers, transactions, contributions, debts and repayments. The base currency change and every read value stored data with the latest stored rate whatever its age. On the budget and goal reads, `POST /v1/budgets`, `PUT /v1/budgets/{id}`, `PUT /v1/goals/{id}` and `DELETE /v1/categories/{id}` (a budget merge that needs a rate) the case cannot happen once the settings check passed and is kept so that it never becomes a 500. On `GET /v1/overview` it is returned when the balance, a budget, a goal or an open debt needs a rate that is not stored. On `GET /v1/debts` it is returned when an open debt needs a rate that was never stored, whatever the `status` filter, because every page carries the summary (see Debts) |

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
- A rate older than 30 days counts as missing when it is used by `GET /v1/rates` and by the automatic conversions of writes (transfers, transactions, goal contributions, debts with an account and repayments without a manual rate, the conversion of a repayment into an account in a third currency, and every account-to-base conversion). The age is measured against `min(date, today)`: a rate dated more than 30 days before that day is stale, and one dated exactly 30 days before is not. The earliest-rate fallback for a date before the history start is never stale. A currency that NBP stopped publishing therefore leaves `GET /v1/rates` and answers 503 `rates.unavailable` on automatic conversions once its last rate is older than 30 days, while a manual `rate` still works for the entered-to-account step.
- The staleness rule never applies to reads that value stored data: the overview balance, budgets, goals, the debt summary (`summary` of `GET /v1/debts` and `Overview.debts`), the base currency change, copying budgets and moving budgets when a category is deleted use the latest stored rate whatever its age, so a delisted currency never breaks a read.
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
