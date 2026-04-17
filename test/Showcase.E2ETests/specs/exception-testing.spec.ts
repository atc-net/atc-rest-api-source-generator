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
});
