import { expect, type Page, type Locator } from '@playwright/test';

export class WebhookDemoPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly events: Locator;
  readonly systemAlertButton: Locator;
  readonly userActivityButton: Locator;
  readonly dataChangeButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /webhook demo/i });
    this.events = page.getByTestId('webhook-event');
    // Blazor only
    this.systemAlertButton = page.getByRole('button', { name: /system alert/i });
    this.userActivityButton = page.getByRole('button', { name: /user activity/i });
    this.dataChangeButton = page.getByRole('button', { name: /data change/i });
  }

  async goto() {
    await this.page.goto('/webhooks/demo');
  }

  // Both pages connect and subscribe to every topic on their own, and say "Listening" once subscribed
  async waitUntilListening() {
    await expect(this.page.getByText('Listening', { exact: true })).toBeVisible();
  }

  // The event that mentions this (unique) text - other pages' heartbeats arrive too
  event(text: string): Locator {
    return this.events.filter({ hasText: text });
  }
}
