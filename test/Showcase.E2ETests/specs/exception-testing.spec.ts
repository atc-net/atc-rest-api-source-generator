import { test, expect } from '../fixtures/test';
import { ExceptionTestingPage } from '../page-objects/exception-testing.page';

test.describe('Exception Testing Page', () => {
  let exceptionPage: ExceptionTestingPage;

  test.beforeEach(async ({ page }) => {
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

  test('shows the Retry-After of a 429', async () => {
    await exceptionPage.requestErrorResponse(429);
    await expect(exceptionPage.errorResponsesTable).toContainText('Slow down - try again in 30 seconds.');
    await expect(exceptionPage.errorResponsesTable).toContainText('30');
  });
});
