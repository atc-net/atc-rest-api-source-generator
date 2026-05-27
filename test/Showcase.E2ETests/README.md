# Showcase E2E Tests

Playwright end-to-end tests for the Showcase sample's three frontend apps:

- **Blazor WASM** — `Showcase.BlazorApp`
- **React + Fetch** — `Showcase.ReactApp.Fetch` (uses the native `fetch` API; default `--client-type Fetch` of the generator)
- **React + Axios** — `Showcase.ReactApp.Axios` (uses the `axios` library; `--client-type Axios`)

The same test specs run against all three apps using Playwright's multi-project feature, so any spec gain or regression is verified in lock-step across every supported generator output.

## Architecture

```
fixtures/test.ts       Custom base test exposing appType ('blazor' | 'react') and appVariant ('fetch' | 'axios')
page-objects/*.ts      Thin page wrappers with role-based selectors
specs/*.ts             Test specs (each runs once per project automatically)
playwright.config.ts   Three projects (blazor/react-fetch/react-axios) with different baseURLs
```

Both MudBlazor and MUI render semantic HTML, so role-based selectors (`getByRole`, `getByText`) work across all apps. Only the navigation page object branches on `appType` since Blazor renders nav links and React renders nav buttons. The two React projects share identical page logic; only the generated `src/api/` differs (fetch vs axios). They share the same `appType: 'react'` so existing branching in page objects continues to work for both.

## Prerequisites

```bash
npm install
npx playwright install chromium
```

## Running Tests

The `webServer` config auto-starts the API and all three frontends. If they're already running (e.g., via Aspire), Playwright reuses the existing servers.

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

15 spec files covering all Showcase pages, run once per project (48 tests per project × 3 projects = 144 tests total):

| Spec | Route | What it tests |
|------|-------|---------------|
| home | `/` | Dashboard cards, navigation to sections |
| accounts | `/accounts` | CRUD: create, load, delete accounts |
| accounts-paginated | `/accounts/paginated` | Page size, load, pagination controls |
| accounts-streaming | `/accounts/async-enumerable` | Start/cancel/clear streaming |
| tasks | `/tasks` | CRUD: create, load tasks |
| users | `/users` | Search, filters, user list |
| user-form | `/users/create` | Form fields, submit, cancel |
| user-details | `/users/:id` | Profile display, edit/delete buttons |
| files | `/files` | File cards, preview dialog |
| files-upload | `/files/upload` | 4 upload tabs, upload buttons |
| notifications | `/notifications` | Connect/disconnect, status display |
| notification-subscriptions | `/notifications/subscriptions` | Create subscription form |
| webhook-demo | `/webhooks/demo` | Trigger buttons, connection state |
| exception-testing | `/testing/exceptions` | Run all tests, verify results |
| navigation | sidebar | Nav items visible, click navigates |
