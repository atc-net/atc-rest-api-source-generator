import { test, expect } from '../fixtures/test';
import { NotificationsPage } from '../page-objects/notifications.page';

test.describe('Notifications Page', () => {
  let notificationsPage: NotificationsPage;

  test.beforeEach(async ({ page }) => {
    notificationsPage = new NotificationsPage(page);
    await notificationsPage.goto();
  });

  test('displays heading', async () => {
    await expect(notificationsPage.heading).toBeVisible();
  });

  test('shows connection controls', async ({ page }) => {
    // Both apps show Connect and/or Disconnect buttons
    // Use button role with partial name match to handle icon+text buttons
    const connect = page.getByRole('button', { name: /connect/i }).first();
    await expect(connect).toBeVisible();
  });

  test('shows connection status', async () => {
    await expect(notificationsPage.connectionStatus).toBeVisible();
  });
});
