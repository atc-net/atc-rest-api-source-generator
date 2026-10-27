import type { Page, Locator } from '@playwright/test';

export class ExceptionTestingPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly runAllButton: Locator;
  readonly clearButton: Locator;
  readonly resultsTable: Locator;
  readonly errorResponsesTable: Locator;
  readonly declaredBadRequestTable: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /exception testing/i });
    this.runAllButton = page.getByRole('button', { name: /run all/i });
    this.clearButton = page.getByRole('button', { name: /clear/i });
    // The page has two tables: the exception results, then the error-response results.
    this.resultsTable = page.getByRole('table').first();
    this.errorResponsesTable = page.getByRole('table').nth(1);
    // Blazor only: the results of the operation that declares its own 400 model.
    this.declaredBadRequestTable = page.getByRole('table').nth(2);
  }

  async goto() {
    await this.page.goto('/testing/exceptions');
  }

  async runAllTests() {
    await this.runAllButton.click();
  }

  async clearResults() {
    await this.clearButton.click();
  }

  async requestErrorResponse(status: number) {
    await this.page.getByRole('button', { name: String(status), exact: true }).click();
  }

  async sendDeclaredBadRequest(label: string) {
    await this.page.getByRole('button', { name: label, exact: true }).click();
  }

  getResultRows(): Locator {
    return this.resultsTable.getByRole('row');
  }
}
