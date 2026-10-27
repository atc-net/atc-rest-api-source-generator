import { test as base, expect, type ConsoleMessage, type Request } from '@playwright/test';

export type AppType = 'blazor' | 'react';
export type AppVariant = 'fetch' | 'axios' | undefined;

// The browser errors a test is held to: an uncaught exception (pageerror), a console error or a failed request
// (requestfailed) fails the test - unless the test says it expects that very error, and why.
export type BrowserErrors = {
  // Expects the errors that match the pattern (as described: "console.error ...", "requestfailed METHOD URL ...")
  expect(pattern: RegExp, reason: string): void;
  // Expects the console error Chromium logs for an error response: that status, from a URL that contains the fragment
  expectStatus(status: number, urlFragment: string, reason: string): void;
  // Expects requests to URLs that contain the fragment to be cancelled (net::ERR_ABORTED) - a stream the test stops
  expectAborted(urlFragment: string, reason: string): void;
};

type CustomFixtures = {
  appType: AppType;
  appVariant: AppVariant;
  browserErrors: BrowserErrors;
};

type ExpectedError = { pattern: RegExp; reason: string };

// Noise that is not a bug in any app: a GET cancelled before its answer, then sent again and answered. React's
// StrictMode (the dev server) mounts a component twice, and React Query cancels the first mount's query - the
// generated hooks pass its signal - and sends it again; whether the first request is still in flight then is timing.
// Nothing is lost. A cancelled request that is never answered still fails the test.
const expectedEverywhere: ExpectedError[] = [
  { pattern: /^requestfailed GET \S+ net::ERR_ABORTED \(re-sent and answered\)$/, reason: 'a cancelled GET sent again and answered' },
];

// Noise of an app that is not a bug in it. Keep these lists short, and say why each entry is here.
const expectedFor: Record<AppType, ExpectedError[]> = {
  blazor: [
    // The WebAssembly HttpClient streams a response, and releases one without a body (a 204, a 304, a 200 with no
    // content) by aborting its fetch once the response is disposed - Chromium reports that as a cancelled request
    // although the call succeeded. Only a bodiless answer: a cancelled request with a body, or one that never got an
    // answer, still fails the test.
    { pattern: /^requestfailed \S+ \S+ net::ERR_ABORTED \(after \d{3}, no body\)$/, reason: 'a bodiless answer released by the WebAssembly HttpClient' },
  ],
  react: [],
};

const escape = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

const describeConsole = (message: ConsoleMessage) => {
  const location = message.location();
  return `console.error ${message.text()}${location.url ? ` (${location.url}:${location.lineNumber})` : ''}`;
};

const describeFailedRequest = async (request: Request) => {
  const failed = `requestfailed ${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`;
  // A request can fail after its answer arrived (its body was cancelled): the status says which. Best effort, and
  // bounded - the teardown waits for this; without a status the entry stays as it is (and is not "no body")
  const response = await Promise.race([
    request.response().catch(() => null),
    new Promise<null>((resolve) => setTimeout(() => resolve(null), 1_000)),
  ]);
  if (!response) return failed;
  const status = response.status();
  const bodiless = status === 204 || status === 304 || response.headers()['content-length'] === '0';
  return `${failed} (after ${status}${bodiless ? ', no body' : ''})`;
};

export const test = base.extend<CustomFixtures>({
  appType: async ({}, use, testInfo) => {
    const appType = (testInfo.project.metadata?.appType as AppType) ?? 'react';
    await use(appType);
  },
  appVariant: async ({}, use, testInfo) => {
    const appVariant = testInfo.project.metadata?.appVariant as AppVariant;
    await use(appVariant);
  },

  // Automatic: every test watches its page, and fails on any error nobody said to expect
  browserErrors: [
    async ({ page, appType }, use, testInfo) => {
      const expected: ExpectedError[] = [...expectedEverywhere, ...expectedFor[appType]];
      // Each error is described at the end of the test - a cancelled request may be answered later
      const seen: Promise<string | (() => Promise<string>)>[] = [];
      let watching = true;
      const record = (error: string | Promise<string | (() => Promise<string>)>) => {
        if (watching) seen.push(Promise.resolve(error));
      };

      page.on('pageerror', (error) => record(`pageerror ${error.name}: ${error.message}`));
      page.on('console', (message) => {
        if (message.type() === 'error') record(describeConsole(message));
      });
      // When each URL was last answered by a GET that succeeded (below 400), to tell a re-sent request from a lost one
      const answered = new Map<string, number>();
      page.on('requestfinished', (request) => {
        if (request.method() !== 'GET') return;
        void request.response().then((response) => {
          if (response && response.status() < 400) answered.set(request.url(), Date.now());
        }).catch(() => {});
      });
      page.on('requestfailed', (request) => {
        const failedAt = Date.now();
        record(describeFailedRequest(request).then((failed) => async () =>
          failed.endsWith('net::ERR_ABORTED') && request.method() === 'GET' && (answered.get(request.url()) ?? 0) >= failedAt
            ? `${failed} (re-sent and answered)`
            : failed));
      });

      await use({
        expect: (pattern, reason) => expected.push({ pattern, reason }),
        expectStatus: (status, urlFragment, reason) =>
          expected.push({
            pattern: new RegExp(`^console\\.error Failed to load resource: the server responded with a status of ${status}\\b.*\\(\\S*${escape(urlFragment)}\\S*\\)$`),
            reason,
          }),
        expectAborted: (urlFragment, reason) =>
          expected.push({ pattern: new RegExp(`^requestfailed \\S+ \\S*${escape(urlFragment)}\\S* net::ERR_ABORTED`), reason }),
      });

      // The page closes after this fixture: what its teardown cancels is not the app's doing
      watching = false;
      const errors = await Promise.all(
        (await Promise.all(seen)).map((error) => (typeof error === 'function' ? error() : error)));
      const unexpected = errors.filter((error) => !expected.some(({ pattern }) => pattern.test(error)));
      if (unexpected.length > 0) {
        await testInfo.attach('browser-errors', { body: unexpected.join('\n'), contentType: 'text/plain' });
      }
      expect(unexpected, 'the page raised errors nobody expected').toEqual([]);
    },
    { auto: true },
  ],
});

export { expect };
