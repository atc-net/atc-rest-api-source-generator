import { randomUUID } from 'node:crypto';
import { test, expect } from '../fixtures/test';
import { unique } from '../fixtures/api';
import { sendDataChange, sendSystemNotification, sendUserActivity } from '../fixtures/webhooks';
import { WebhookDemoPage } from '../page-objects/webhook-demo.page';

// The webhooks (onSystemNotification, onUserActivity, onDataChange): an external system calls the API's webhook
// endpoints, and each handler pushes the event to the connected pages over SignalR
test.describe('Webhook Demo Page', () => {
  let webhookPage: WebhookDemoPage;

  test.beforeEach(async ({ page }) => {
    webhookPage = new WebhookDemoPage(page);
    await webhookPage.goto();
    await expect(webhookPage.heading).toBeVisible();
    await webhookPage.waitUntilListening();
  });

  test('shows each webhook call the API receives, pushed over SignalR', async ({ request }, testInfo) => {
    const message = unique('E2E webhook', testInfo);
    const userId = randomUUID();
    const entityId = randomUUID();

    await sendSystemNotification(request, message);
    await expect(webhookPage.event(message)).toBeVisible();

    await sendUserActivity(request, userId);
    await expect(webhookPage.event(userId)).toContainText(`User ${userId} performed ProfileUpdate`);

    await sendDataChange(request, entityId);
    await expect(webhookPage.event(entityId)).toContainText('Updated');
    await expect(webhookPage.event(entityId)).toContainText('Account');
  });

  test('the trigger buttons call the webhooks, and the events come back', async ({ page, appType }) => {
    test.skip(appType !== 'blazor', 'Only the Blazor page sends webhooks itself (the React page only receives them).');

    // Each button posts an event of its own: the event the page then shows is found by what the request sent
    const trigger = async (button: typeof webhookPage.systemAlertButton, path: string) => {
      const [request] = await Promise.all([
        page.waitForRequest((r) => r.method() === 'POST' && r.url().endsWith(`/webhooks/${path}`)),
        button.click(),
      ]);
      expect((await request.response())?.status()).toBe(200);
      return request.postDataJSON() as Record<string, string>;
    };

    const alert = await trigger(webhookPage.systemAlertButton, 'system-notification');
    // A warning reaches the page twice: through the System and the Alert topic
    await expect(webhookPage.event(alert.message).first()).toBeVisible();

    const activity = await trigger(webhookPage.userActivityButton, 'user-activity');
    await expect(webhookPage.event(activity.userId)).toContainText(`User ${activity.userId} performed ${activity.action}`);

    const change = await trigger(webhookPage.dataChangeButton, 'data-change');
    await expect(webhookPage.event(change.entityId)).toContainText(`${change.operation} ${change.entityType}/${change.entityId}`);
  });
});
