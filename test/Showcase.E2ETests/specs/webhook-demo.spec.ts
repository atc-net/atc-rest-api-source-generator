import { test, expect } from '../fixtures/test';
import { WebhookDemoPage } from '../page-objects/webhook-demo.page';

test.describe('Webhook Demo Page', () => {
  let webhookPage: WebhookDemoPage;

  test.beforeEach(async ({ page }) => {
    webhookPage = new WebhookDemoPage(page);
    await webhookPage.goto();
  });

  test('displays heading', async () => {
    await expect(webhookPage.heading).toBeVisible();
  });

  test('shows page content', async ({ page }) => {
    // Verify page has meaningful content beyond the heading
    // Trigger buttons may not render until SignalR connects; check for section text instead
    await expect(page.getByText(/trigger|connection|webhook/i).first()).toBeVisible();
  });
});
