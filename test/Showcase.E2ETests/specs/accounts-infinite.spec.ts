import { test, expect } from '../fixtures/test';

// The generated infinite hooks (React Query useInfiniteQuery and SWR useSWRInfinite) of the paginated
// listPaginatedAccounts read against the live API: 120 seeded accounts (and those other specs create for a moment),
// 50 per page. React only.
test.describe('Accounts Page (infinite)', () => {
  test.beforeEach(async ({ page, appType }) => {
    test.skip(appType !== 'react', 'The infinite hooks are a React (TypeScript client) feature.');
  });

  for (const hook of ['rq', 'swr']) {
    test(`${hook}: loads page after page through the continuation token and stops at the last one`, async ({ page }) => {
      const pageRequests: URL[] = [];
      page.on('request', (request) => {
        const url = new URL(request.url());
        if (request.method() === 'GET' && url.pathname.endsWith('/api/v1/accounts/paginated')) {
          pageRequests.push(url);
        }
      });

      await page.goto('/accounts/infinite');
      const section = page.getByTestId(`${hook}-infinite`);
      const summary = page.getByTestId(`${hook}-summary`);
      const loadMore = section.getByRole('button', { name: /load more/i });

      await expect(summary).toHaveText('50 accounts in 1 pages');
      await loadMore.click();
      await expect(summary).toHaveText('100 accounts in 2 pages');
      await loadMore.click();
      // The API seeds 120 accounts; the accounts spec of another project may have one of its own at this moment -
      // the last page holds the rest, whatever it counts
      await expect(summary).toHaveText(/^1[2-4]\d accounts in 3 pages$/);
      await expect(page.getByTestId(`${hook}-done`)).toBeVisible();
      await expect(loadMore).toBeDisabled();

      // Every page keeps the hook's own query; each later page sends the previous page's token back.
      const tokens = pageRequests.map((url) => url.searchParams.get('continuation'));
      expect(pageRequests.every((url) => url.searchParams.get('pageSize') === '50')).toBe(true);
      expect(tokens).toContain(null);
      expect(tokens).toContain('page:1');
      expect(tokens).toContain('page:2');
      expect(tokens).not.toContain('page:3');
    });
  }
});
