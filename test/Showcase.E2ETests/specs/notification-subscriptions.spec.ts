import { test, expect } from '../fixtures/test';
import { apiGet, unique } from '../fixtures/api';
import { NotificationSubscriptionsPage } from '../page-objects/notification-subscriptions.page';

type Subscription = { id: string; name?: string | null; topics: string[]; isActive: boolean };

// createSubscription, listSubscriptions, getSubscriptionById and deleteSubscription
test.describe('Notification Subscriptions Page', () => {
  test('creates a subscription, reads it on its own and deletes it - and the API agrees', async ({ page, request, appType }, testInfo) => {
    const subscriptions = new NotificationSubscriptionsPage(page);
    const name = unique('E2E subscription', testInfo);
    await subscriptions.goto();
    await expect(subscriptions.heading).toBeVisible();

    // Create, for the System and Alert topics (the Blazor form starts with those two)
    await subscriptions.nameTextbox.fill(name);
    if (appType === 'react') {
      await subscriptions.selectTopics(['System', 'Alert']);
    }
    await subscriptions.createButton.click();
    const row = subscriptions.row(name);
    await expect(row).toBeVisible();
    await expect(row).toContainText('System');
    await expect(row).toContainText('Alert');

    const created = (await apiGet<Subscription[]>(request, '/notifications/subscriptions')).body.find((s) => s.name === name);
    expect(created, 'the API lists the created subscription').toBeTruthy();
    expect(created!.topics).toEqual(['System', 'Alert']);
    expect(created!.isActive).toBe(true);

    // Details: the subscription read on its own
    await row.getByRole('button', { name: 'Details' }).click();
    await expect(subscriptions.detailsId).toHaveText(created!.id);
    await expect(subscriptions.detailsName).toHaveText(name);
    await expect(subscriptions.detailsTopics).toHaveText('System, Alert');

    // Delete (React asks first)
    const deleted = page.waitForResponse((r) => r.request().method() === 'DELETE');
    await row.getByRole('button', { name: 'Delete' }).click();
    if (appType === 'react') {
      await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
      // An open dialog hides the page from the accessibility tree: the list is looked at once it closed
      await expect(page.getByRole('dialog')).toBeHidden();
    }
    expect((await deleted).status()).toBe(204);
    await expect(subscriptions.row(name)).toHaveCount(0);
    expect((await apiGet(request, `/notifications/subscriptions/${created!.id}`)).status).toBe(404);
  });
});
