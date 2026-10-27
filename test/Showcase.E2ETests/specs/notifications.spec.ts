import { test, expect } from '../fixtures/test';
import { unique } from '../fixtures/api';
import { sendSystemNotification } from '../fixtures/webhooks';
import { NotificationsPage } from '../page-objects/notifications.page';

test.describe('Notifications Page', () => {
  let notifications: NotificationsPage;

  test.beforeEach(async ({ page }) => {
    notifications = new NotificationsPage(page);
    await notifications.goto();
    await expect(notifications.heading).toBeVisible();
  });

  // SignalR: the page connects to the hub, subscribes to the topics, and shows a notification the server pushes -
  // one the test sends through the system-notification webhook, so it is this test's own
  test('receives a notification over SignalR while subscribed, and none after unsubscribing', async ({ page, request }, testInfo) => {
    await notifications.connectButton.click();
    await expect(notifications.disconnectButton).toBeVisible();
    await expect(page.getByText('Connected', { exact: true })).toBeVisible();
    await notifications.subscribeButton.click();
    await expect(notifications.unsubscribeButton).toBeEnabled();

    const message = unique('E2E notification', testInfo);
    await sendSystemNotification(request, message);
    await expect(notifications.item(message)).toBeVisible();

    // Unsubscribed, the hub sends this connection nothing more (the hub needs the id the subscription got)
    await notifications.unsubscribeButton.click();
    await expect(notifications.subscribeButton).toBeEnabled();
    const later = `${message} after unsubscribing`;
    await sendSystemNotification(request, later);
    await page.waitForTimeout(1500);
    await expect(notifications.item(later)).toHaveCount(0);

    await notifications.disconnectButton.click();
    await expect(notifications.connectButton).toBeVisible();
    await expect(page.getByText('Disconnected', { exact: true })).toBeVisible();
  });

  // listNotifications: one HTTP response the server keeps writing (an event every 5-10 seconds), read as it arrives
  test('reads notifications from the HTTP stream as they arrive, until stopped', async ({ browserErrors }) => {
    test.setTimeout(60_000);
    browserErrors.expectAborted('/api/v1/notifications/stream', 'stopping the stream cancels its request');

    await notifications.startStreamButton.click();
    await expect(notifications.streamStatus).toHaveText(/^Streaming - \d+ received$/);
    await expect(notifications.streamItems.first()).toBeVisible({ timeout: 25_000 });
    await expect(notifications.streamItems.first()).toHaveText(/^(System|User|Data): \[#1\] /);

    await notifications.stopStreamButton.click();
    await expect(notifications.streamStatus).toHaveText('Stopped');
  });
});
