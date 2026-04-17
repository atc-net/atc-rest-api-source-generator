import { test, expect } from '../fixtures/test';
import { AccountsStreamingPage } from '../page-objects/accounts-streaming.page';

test.describe('Accounts Streaming', () => {
  let streamingPage: AccountsStreamingPage;

  test.beforeEach(async ({ page }) => {
    streamingPage = new AccountsStreamingPage(page);
    await streamingPage.goto();
  });

  test('displays heading', async () => {
    await expect(streamingPage.heading).toBeVisible();
  });

  test('shows streaming controls', async ({ page }) => {
    await expect(streamingPage.startStreamingButton).toBeVisible();
    // Cancel may be hidden until streaming starts; verify Clear is present
    await expect(streamingPage.clearButton).toBeVisible();
  });

  test('can start streaming and items appear', async ({ page }) => {
    await streamingPage.startStreaming();

    // Wait for table rows to appear (works for both Blazor and React)
    await expect(streamingPage.table.locator('tbody tr').first()).toBeVisible({
      timeout: 15_000,
    });
  });

  test('can cancel streaming', async () => {
    await streamingPage.startStreaming();

    // Wait briefly for streaming to begin, then cancel
    await expect(streamingPage.cancelButton).toBeEnabled();
    await streamingPage.cancelStreaming();

    // After canceling, the start button should become enabled again
    await expect(streamingPage.startStreamingButton).toBeEnabled();
  });
});
