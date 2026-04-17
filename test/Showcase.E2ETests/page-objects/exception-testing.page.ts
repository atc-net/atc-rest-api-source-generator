import type { Page, Locator } from '@playwright/test';

export class ExceptionTestingPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly runAllButton: Locator;
  readonly clearButton: Locator;
  readonly resultsTable: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /exception testing/i });
    this.runAllButton = page.getByRole('button', { name: /run all/i });
    this.clearButton = page.getByRole('button', { name: /clear/i });
    this.resultsTable = page.getByRole('table');
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

  getResultRows(): Locator {
    return this.resultsTable.getByRole('row');
  }
}
