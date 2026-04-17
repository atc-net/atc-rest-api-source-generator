import type { Page, Locator } from '@playwright/test';

export class NotificationSubscriptionsPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly nameTextbox: Locator;
  readonly createButton: Locator;
  readonly loadButton: Locator;
  readonly table: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /notification subscriptions/i });
    // Blazor: "Name", React: "Name (optional)"
    this.nameTextbox = page.getByLabel(/^name/i);
    this.createButton = page.getByRole('button', { name: 'Create', exact: true });
    // Blazor: "Refresh", React: "Load Subscriptions"
    this.loadButton = page.getByRole('button', { name: /load subscriptions|refresh/i });
    this.table = page.getByRole('table');
  }

  async goto() {
    await this.page.goto('/notifications/subscriptions');
  }

  async loadSubscriptions() {
    await this.loadButton.click();
  }
}
