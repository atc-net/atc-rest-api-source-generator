import { type Page, type Locator } from '@playwright/test';

export class AccountsStreamingPage {
  readonly heading: Locator;
  readonly startStreamingButton: Locator;
  readonly cancelButton: Locator;
  readonly clearButton: Locator;
  readonly itemsChip: Locator;
  readonly table: Locator;

  constructor(private readonly page: Page) {
    // Blazor: "Async Enumerable Accounts", React: "Accounts (Streaming)"
    this.heading = page.getByRole('heading', { name: /async enumerable|streaming/i });
    this.startStreamingButton = page.getByRole('button', { name: 'Start Streaming' });
    this.cancelButton = page.getByRole('button', { name: 'Cancel' });
    this.clearButton = page.getByRole('button', { name: 'Clear' });
    // Blazor: "Total Items: N", React: "N items received"
    this.itemsChip = page.getByText(/\d+ items received|total items/i);
    this.table = page.getByRole('table');
  }

  async goto() {
    await this.page.goto('/accounts/async-enumerable');
  }

  async startStreaming() {
    await this.startStreamingButton.click();
  }

  async cancelStreaming() {
    await this.cancelButton.click();
  }

  async clear() {
    await this.clearButton.click();
  }
}
