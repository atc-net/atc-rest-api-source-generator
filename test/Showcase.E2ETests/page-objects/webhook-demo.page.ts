import type { Page, Locator } from '@playwright/test';

export class WebhookDemoPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly connectionStatus: Locator;
  readonly systemAlertButton: Locator;
  readonly userActivityButton: Locator;
  readonly dataChangeButton: Locator;
  readonly clearButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /webhook demo/i });
    this.connectionStatus = page.getByText(/connected|disconnected/i);
    this.systemAlertButton = page.getByRole('button', { name: /system alert/i });
    this.userActivityButton = page.getByRole('button', { name: /user activity/i });
    this.dataChangeButton = page.getByRole('button', { name: /data change/i });
    this.clearButton = page.getByRole('button', { name: /clear/i });
  }

  async goto() {
    await this.page.goto('/webhooks/demo');
  }
}
