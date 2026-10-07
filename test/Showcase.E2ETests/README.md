# Showcase E2E Tests

Playwright end-to-end tests for the Showcase sample's three frontend apps:

- **Blazor WASM** — `Showcase.BlazorApp`
- **React + Fetch** — `Showcase.ReactApp.Fetch` (uses the native `fetch` API; default `--client-type Fetch` of the generator)
- **React + Axios** — `Showcase.ReactApp.Axios` (uses the `axios` library; `--client-type Axios`)

The same test specs run against all three apps using Playwright's multi-project feature, so any spec gain or regression is verified in lock-step across every supported generator output.

## Architecture

```
fixtures/test.ts           Base test: appType ('blazor' | 'react'), appVariant ('fetch' | 'axios'), browserErrors
fixtures/api.ts            Direct API reads as the demo identity, and unique names for the data a test creates
fixtures/webhooks.ts       Calls the API's webhook endpoints, as the external system that sends them
fixtures/name-tag-crud.ts  The create/update/delete spec shared by accounts and tasks
page-objects/*.ts          Thin page wrappers with role-based selectors
specs/*.ts                 Test specs (each runs once per project automatically)
playwright.config.ts       Three projects (blazor/react-fetch/react-axios) with different baseURLs
```

### Browser errors fail a test

Every test watches its page (the automatic `browserErrors` fixture): an uncaught exception (`pageerror`), a console
error, or a failed request (`requestfailed`) fails it. A test that causes an error on purpose says so, and why:

```ts
test('an unknown id shows the error', async ({ page, browserErrors }) => {
  browserErrors.expectStatus(404, '/api/v1/tasks/987654321', 'the lookup asks for an unknown id on purpose');
  // ...
});
```

`expectStatus(status, urlFragment, reason)` covers Chromium's console error for an error response, `expectAborted(urlFragment, reason)`
a request the test cancels (a stream it stops), and `expect(pattern, reason)` anything else. The only noise tolerated is in
`fixtures/test.ts`: a GET cancelled before its answer and then sent again and answered (React StrictMode on the dev server remounts a
component, and React Query cancels and re-sends its query), and on Blazor a bodiless answer (204, 304, a 200 without content) that the
WebAssembly HttpClient releases by aborting its fetch.

### Shared data

The API is in memory, and one run of all three projects shares it. A test that creates data names it uniquely (`unique()` in
`fixtures/api.ts`), looks only at its own rows, and checks the outcome against the API - never against a global count. The run starts
the API with `Showcase__DisableRateLimiter=true`: three apps from one IP address and one demo identity would exceed the specification's
per-minute limits (50 user requests, 10 uploads).

Both MudBlazor and MUI render semantic HTML, so role-based selectors (`getByRole`, `getByText`) work across all apps. Only the navigation page object branches on `appType` since Blazor renders nav links and React renders nav buttons. The two React projects share identical page logic; only the generated `src/api/` and `src/api-swr/` differ (fetch vs axios). `specs/swr-tasks.spec.ts` (SWR hooks) and `specs/accounts-infinite.spec.ts` (React Query and SWR infinite hooks) cover React-only pages and skip on Blazor. They share the same `appType: 'react'` so existing branching in page objects continues to work for both.

## Prerequisites

```bash
npm install
npx playwright install chromium
```

## Running Tests

The `webServer` config auto-starts the API and all three frontends, and fails on a port that is already taken. With `PW_REUSE=1` it
reuses servers that are already running (e.g., via Aspire) - then start the API with `Showcase__DisableRateLimiter=true`, or a full
run meets the API's rate limits (429).

```bash
npm test                  # run all tests (all three projects)
npm run test:blazor       # Blazor only
npm run test:react        # Both React projects (fetch + axios)
npm run test:react-fetch  # React + Fetch only
npm run test:react-axios  # React + Axios only
npm run test:ui           # interactive UI mode (great for debugging)
npm run report            # view HTML report from last run
```

### Useful flags

```bash
npx playwright test accounts.spec.ts                # single spec file (runs against all 3 projects)
npx playwright test --headed                        # visible browser
npx playwright test --headed --project=react-axios  # visible browser, React+Axios only
npx playwright test --debug                         # step-by-step debugger
```

## Ports

| App | Port | Source |
|-----|------|--------|
| Showcase.Api | 15046 | `Properties/launchSettings.json` |
| Showcase.BlazorApp | 5048 | `Properties/launchSettings.json` |
| Showcase.ReactApp.Fetch | 5173 | `PORT` env (set by `playwright.config.ts`); falls back to `vite.config.ts` default |
| Showcase.ReactApp.Axios | 5174 | `PORT` env (set by `playwright.config.ts`); falls back to `vite.config.ts` default |

## Test Coverage

21 spec files, run once per project (63 tests per project × 3 projects = 189 tests; the React-only and the Blazor-only
tests skip on the other projects):

| Spec | Route | What it tests |
|------|-------|---------------|
| home | `/` | Dashboard cards, a section reached through the app's navigation |
| navigation | sidebar | Nav items visible, click navigates |
| accounts | `/accounts` | Create, rename and delete an account (createAccount, listAccounts, updateAccountById, getAccountById, deleteAccountById), each step read back from the page and the API |
| accounts-paginated | `/accounts/paginated` | Page size, load, pagination controls |
| accounts-streaming | `/accounts/async-enumerable` | Start/cancel streaming |
| accounts-infinite | `/accounts/infinite` | React Query and SWR infinite hooks follow the continuation token (React only) |
| tasks | `/tasks` | Create, rename and delete a task (createTask ... deleteTaskById), read back from the page and the API |
| swr-tasks | `/tasks/swr` | The SWR hooks (React only) |
| users | `/users` | Search, filters, user list |
| users-crud | `/users/create`, `/users/:id`, `/users/:id/edit` | Create a user through the form, its details page, edit, delete (createUser, getUserById, updateUserById, deleteUserById) - and the API agrees at every step |
| users-query | `/users` | queryUsers: the HTTP QUERY request carries the ids in its body; an unknown id is skipped |
| users-import | `/users` | importUsers: a list of users as XML, validated per item |
| user-xml | `/users/:id` | getUserById as XML, read back into the model |
| auth | API | 401 without a token; 403 for createUser/deleteUserById with a token that lacks the users:write and admin scopes; 201 with both |
| files | `/files` | File cards, preview dialog |
| files-upload | `/files/upload` | The four uploads: the request's media type, the 200, the page's message, and the file the API stored (read back with getFileById) |
| documents | `/documents` | Raw-body upload, download, summary as JSON/PDF, 303 and 304 |
| notifications | `/notifications` | SignalR: connect, subscribe, receive a notification sent through a webhook, none after unsubscribing; listNotifications: the HTTP stream's events |
| notification-subscriptions | `/notifications/subscriptions` | Create a subscription, read it on its own, delete it (createSubscription, listSubscriptions, getSubscriptionById, deleteSubscription), checked against the API |
| webhook-demo | `/webhooks/demo` | The three webhooks, called by the test, reach the page over SignalR; on Blazor the trigger buttons too |
| exception-testing | `/testing/exceptions` | Run all tests, ProblemDetails, validation errors, Retry-After |
