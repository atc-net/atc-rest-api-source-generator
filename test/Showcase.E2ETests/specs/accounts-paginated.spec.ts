import { test, expect } from '../fixtures/test';
import { AccountsPaginatedPage } from '../page-objects/accounts-paginated.page';

test.describe('Accounts Paginated', () => {
  let paginatedPage: AccountsPaginatedPage;

  test.beforeEach(async ({ page }) => {
    paginatedPage = new AccountsPaginatedPage(page);
    await paginatedPage.goto();
  });

  test('displays heading', async () => {
    await expect(paginatedPage.heading).toBeVisible();
  });

  test('shows page size control', async () => {
    await expect(paginatedPage.pageSizeInput).toBeVisible();
    await expect(paginatedPage.loadButton).toBeVisible();
  });

  test('can load first page', async () => {
    await paginatedPage.setPageSize(5);
    await paginatedPage.loadPage();
    await expect(paginatedPage.paginationInfo).toBeVisible();
  });

  test('table appears after loading', async () => {
    await expect(paginatedPage.table).toBeVisible();
    await paginatedPage.loadPage();
    await expect(paginatedPage.table).toBeVisible();
  });
});
