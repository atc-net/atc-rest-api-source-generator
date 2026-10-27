import { test, expect } from '../fixtures/test';

// The generated SWR hooks (`--hooks Swr`) against the live API. React only: Blazor has no SWR client.
test.describe('Tasks Page (SWR hooks)', () => {
  test.beforeEach(async ({ page, appType }) => {
    test.skip(appType !== 'react', 'The SWR hooks are a React (TypeScript client) feature.');
    await page.goto('/tasks/swr');
    await expect(page.getByRole('heading', { name: 'Tasks (SWR hooks)' })).toBeVisible();
  });

  test('lists the tasks without a manual load', async ({ page }) => {
    await expect(page.getByTestId('swr-tasks-table').locator('tbody tr').first()).toBeVisible();
  });

  test('a created task appears because the mutation revalidates the list', async ({ page }) => {
    const name = 'SWR task ' + Date.now();
    await page.getByLabel(/^name$/i).fill(name);
    await page.getByRole('button', { name: /^create$/i }).click();

    await expect(page.getByTestId('swr-tasks-table').getByRole('cell', { name, exact: true })).toBeVisible();
  });

  test('an unknown id shows the ApiError after exactly one request', async ({ page, browserErrors }) => {
    browserErrors.expectStatus(404, '/api/v1/tasks/987654321', 'the lookup asks for an unknown id on purpose');
    const byIdRequests: string[] = [];
    page.on('request', (request) => {
      if (/\/api\/v1\/tasks\/[^/?]+/.test(request.url()) && request.method() === 'GET') {
        byIdRequests.push(request.url());
      }
    });

    // The empty id keeps the query idle.
    await page.waitForTimeout(300);
    expect(byIdRequests).toHaveLength(0);

    await page.getByLabel(/task id/i).fill('987654321');
    await page.getByRole('button', { name: /look up/i }).click();

    await expect(page.getByTestId('swr-lookup-error')).toContainText('404');

    // SWR's own retry is off in the generated hooks, so nothing follows the first request.
    await page.waitForTimeout(1500);
    expect(byIdRequests).toHaveLength(1);
  });
});
