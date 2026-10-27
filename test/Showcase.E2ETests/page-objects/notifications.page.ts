import type { Page, Locator } from '@playwright/test';

export class NotificationsPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly connectButton: Locator;
  readonly disconnectButton: Locator;
  readonly subscribeButton: Locator;
  readonly unsubscribeButton: Locator;
  readonly items: Locator;
  readonly startStreamButton: Locator;
  readonly stopStreamButton: Locator;
  readonly streamStatus: Locator;
  readonly streamItems: Locator;

  constructor(page: Page) {
    this.page = page;
    // Blazor: "Live Notification Feed", React: "Live Notifications"
    this.heading = page.getByRole('heading', { name: /live notification/i });
    this.connectButton = page.getByRole('button', { name: 'Connect', exact: true });
    this.disconnectButton = page.getByRole('button', { name: 'Disconnect', exact: true });
    // Blazor: "Subscribe", React: "Subscribe All"
    this.subscribeButton = page.getByRole('button', { name: /^subscribe( all)?$/i });
    this.unsubscribeButton = page.getByRole('button', { name: 'Unsubscribe', exact: true });
    this.items = page.getByTestId('notification-item');
    this.startStreamButton = page.getByRole('button', { name: 'Start Stream', exact: true });
    this.stopStreamButton = page.getByRole('button', { name: 'Stop Stream', exact: true });
    this.streamStatus = page.getByTestId('notifications-stream-status');
    this.streamItems = page.getByTestId('notifications-stream-item');
  }

  async goto() {
    await this.page.goto('/notifications');
  }

  // The notification with this message (the feed also shows the server's own heartbeats)
  item(message: string): Locator {
    return this.items.filter({ hasText: message });
  }
}
