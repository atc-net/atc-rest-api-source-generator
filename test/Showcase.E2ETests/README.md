# Showcase E2E Tests

Playwright end-to-end tests for the Showcase sample's two frontend apps (Blazor WASM and React). The same test specs run against both apps using Playwright's multi-project feature.

## Architecture

```
fixtures/test.ts       Custom base test exposing appType ('blazor' | 'react')
page-objects/*.ts      Thin page wrappers with role-based selectors
specs/*.ts             Test specs (each runs once per app automatically)
playwright.config.ts   Two projects (blazor/react) with different baseURLs
```

Both MudBlazor and MUI render semantic HTML, so role-based selectors (`getByRole`, `getByText`) work across both apps. Only the navigation page object branches on `appType` since Blazor renders nav links and React renders nav buttons.

## Prerequisites

```bash
npm install
npx playwright install chromium
```

## Running Tests

The `webServer` config auto-starts the API and both frontends. If they're already running (e.g., via Aspire), Playwright reuses the existing servers.

```bash
npm test                  # run all tests (both apps)
npm run test:blazor       # Blazor only
npm run test:react        # React only
npm run test:ui           # interactive UI mode (great for debugging)
npm run report            # view HTML report from last run
```

### Useful flags

```bash
npx playwright test accounts.spec.ts          # single spec file
npx playwright test --headed                  # visible browser
npx playwright test --headed --project=react  # visible browser, React only
npx playwright test --debug                   # step-by-step debugger
```

## Ports

| App | Port | Source |
|-----|------|--------|
| Showcase.Api | 15046 | `Properties/launchSettings.json` |
| Showcase.BlazorApp | 5048 | `Properties/launchSettings.json` |
| Showcase.ReactApp | 5173 | `vite.config.ts` |

## Test Coverage

15 spec files covering all Showcase pages (104 tests total, 52 per app):

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
