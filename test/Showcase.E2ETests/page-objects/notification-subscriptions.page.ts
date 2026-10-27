import type { Page, Locator } from '@playwright/test';

export class NotificationSubscriptionsPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly nameTextbox: Locator;
  readonly createButton: Locator;
  readonly loadButton: Locator;
  readonly table: Locator;
  readonly detailsId: Locator;
  readonly detailsName: Locator;
  readonly detailsTopics: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /notification subscriptions/i });
    // Blazor: "Name", React: "Name (optional)"
    this.nameTextbox = page.getByLabel(/^name/i);
    this.createButton = page.getByRole('button', { name: 'Create', exact: true });
    // Blazor: "Refresh", React: "Load Subscriptions"
    this.loadButton = page.getByRole('button', { name: /load subscriptions|refresh/i });
    this.table = page.getByRole('table');
    this.detailsId = page.getByTestId('subscription-details-id');
    this.detailsName = page.getByTestId('subscription-details-name');
    this.detailsTopics = page.getByTestId('subscription-details-topics');
  }

  async goto() {
    await this.page.goto('/notifications/subscriptions');
  }

  // React: the MUI multi-select of the topics
  async selectTopics(topics: string[]) {
    await this.page.getByRole('combobox').click();
    for (const topic of topics) {
      await this.page.getByRole('option', { name: topic, exact: true }).click();
    }
    await this.page.keyboard.press('Escape');
  }

  row(name: string): Locator {
    return this.table.getByRole('row').filter({ hasText: name });
  }
}
