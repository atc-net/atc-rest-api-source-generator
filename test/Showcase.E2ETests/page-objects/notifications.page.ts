import type { Page, Locator } from '@playwright/test';

export class NotificationsPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly connectButton: Locator;
  readonly disconnectButton: Locator;
  readonly connectionStatus: Locator;

  constructor(page: Page) {
    this.page = page;
    // Blazor: "Live Notification Feed", React: "Live Notifications"
    this.heading = page.getByRole('heading', { name: /live notification/i });
    this.connectButton = page.getByRole('button', { name: 'Connect', exact: true });
    this.disconnectButton = page.getByRole('button', { name: 'Disconnect', exact: true });
    this.connectionStatus = page.getByText(/connected|disconnected/i);
  }

  async goto() {
    await this.page.goto('/notifications');
  }

  async clickConnect() {
    await this.connectButton.click();
  }

  async clickDisconnect() {
    await this.disconnectButton.click();
  }
}
