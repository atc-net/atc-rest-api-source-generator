import { test, expect } from '../fixtures/test';
import { ExceptionTestingPage } from '../page-objects/exception-testing.page';

test.describe('Exception Testing Page', () => {
  let exceptionPage: ExceptionTestingPage;

  test.beforeEach(async ({ page, browserErrors }) => {
    // The page asks the /testing endpoints for their errors on purpose: each error response is logged by Chromium
    browserErrors.expect(
      /^console\.error Failed to load resource: the server responded with a status of \d{3}\b.*\(\S*\/api\/v1\/testing\/\S*\)$/,
      'the exception testing page requests error responses on purpose',
    );
    exceptionPage = new ExceptionTestingPage(page);
    await exceptionPage.goto();
  });

  test('displays heading', async () => {
    await expect(exceptionPage.heading).toBeVisible();
  });

  test('shows run all button', async () => {
    await expect(exceptionPage.runAllButton).toBeVisible();
  });

  test('shows clear button', async () => {
    await expect(exceptionPage.clearButton).toBeVisible();
  });

  test('can run all tests and see results in table', async () => {
    await exceptionPage.runAllTests();
    await expect(exceptionPage.resultsTable).toBeVisible();
    const rows = exceptionPage.getResultRows();
    await expect(rows.first()).toBeVisible();
  });

  // Every error is an RFC 9457 ProblemDetails: the generated client exposes the handler's message as `detail`.
  test('shows the ProblemDetails detail of an error response', async () => {
    await exceptionPage.requestErrorResponse(404);
    await expect(exceptionPage.errorResponsesTable).toContainText('No item with that id exists.');
  });

  test('shows the validation errors of a 400', async () => {
    await exceptionPage.requestErrorResponse(400);
    await expect(exceptionPage.errorResponsesTable).toContainText('The name must be at most 20 characters.');
  });

  // /testing is x-retry-enabled: false in the spec, so its 5xx and 429 answers are not retried
  // (with retries, "Run All" waited for the back-off of every 500/501/504 - about a minute).
  // Counted, not timed: a retried call is a second request for the same URL.
  test('runs all exception tests without retrying them', async ({ page }) => {
    const calls: string[] = [];
    page.on('request', (request) => {
      if (request.method() !== 'OPTIONS' && request.url().includes('/api/v1/testing/')) calls.push(request.url());
    });
    await exceptionPage.runAllTests();
    await expect(exceptionPage.page.getByText('12/12 passed')).toBeVisible({ timeout: 60_000 });
    expect(calls.length).toBeGreaterThan(0);
    expect(new Set(calls).size).toBe(calls.length);
  });

  // A 400 that declares its own model (TestingProblem, no errors): the C# EndpointPerOperation client keeps the
  // declared model in BadRequestContent and gives the ValidationFilter's field errors through
  // BadRequestValidationErrors. The TypeScript clients already expose them as ValidationError.errors.
  test.describe('declared 400 model', () => {
    test.beforeEach(async ({ appType }) => {
      test.skip(appType !== 'blazor', 'BadRequestValidationErrors is a C# EndpointPerOperation client feature.');
    });

    test('an invalid request body gives the declared model and the typed field errors', async () => {
      await exceptionPage.sendDeclaredBadRequest('Invalid request');
      const table = exceptionPage.declaredBadRequestTable;
      await expect(table).toContainText('One or more validation errors occurred.');
      await expect(table).toContainText(/name: .*(3|minimum)/i);
      await expect(table).toContainText(/age: .*150/i);
    });

    test('a 400 from the handler gives the declared model without field errors', async () => {
      await exceptionPage.sendDeclaredBadRequest('Name taken');
      const table = exceptionPage.declaredBadRequestTable;
      await expect(table).toContainText('The name is taken');
      await expect(table).toContainText('is already in use.');
      await expect(table).toContainText('(none)');
    });

    test('a valid request is a 204', async () => {
      await exceptionPage.sendDeclaredBadRequest('Valid request');
      await expect(exceptionPage.declaredBadRequestTable).toContainText('204');
    });
  });

  test('shows the Retry-After of a 429', async () => {
    await exceptionPage.requestErrorResponse(429);
    await expect(exceptionPage.errorResponsesTable).toContainText('Slow down - try again in 30 seconds.');
    await expect(exceptionPage.errorResponsesTable).toContainText('30');
  });
});
