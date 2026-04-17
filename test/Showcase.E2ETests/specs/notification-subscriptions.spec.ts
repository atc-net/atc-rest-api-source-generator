import { test, expect } from '../fixtures/test';
import { NotificationSubscriptionsPage } from '../page-objects/notification-subscriptions.page';

test.describe('Notification Subscriptions Page', () => {
  let subscriptionsPage: NotificationSubscriptionsPage;

  test.beforeEach(async ({ page }) => {
    subscriptionsPage = new NotificationSubscriptionsPage(page);
    await subscriptionsPage.goto();
  });

  test('displays heading', async () => {
    await expect(subscriptionsPage.heading).toBeVisible();
  });

  test('shows create form', async () => {
    await expect(subscriptionsPage.nameTextbox).toBeVisible();
    await expect(subscriptionsPage.createButton).toBeVisible();
  });

  test('shows load button', async () => {
    await expect(subscriptionsPage.loadButton).toBeVisible();
  });
});
