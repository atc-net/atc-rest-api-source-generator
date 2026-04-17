import { test, expect } from '../fixtures/test';
import { AccountsPage } from '../page-objects/accounts.page';

test.describe('Accounts Page', () => {
  let accountsPage: AccountsPage;

  test.beforeEach(async ({ page }) => {
    accountsPage = new AccountsPage(page);
    await accountsPage.goto();
  });

  test('displays heading', async () => {
    await expect(accountsPage.heading).toBeVisible();
  });

  test('shows create form with Name and Tag fields', async () => {
    await expect(accountsPage.nameInput).toBeVisible();
    await expect(accountsPage.tagInput).toBeVisible();
    await expect(accountsPage.createButton).toBeVisible();
  });

  test('can load accounts', async () => {
    await accountsPage.loadAccounts();
    await expect(accountsPage.table).toBeVisible();
  });

  test('create form accepts input', async () => {
    await accountsPage.nameInput.fill('Test Account');
    await expect(accountsPage.nameInput).toHaveValue('Test Account');
    await expect(accountsPage.createButton).toBeEnabled();
  });
});
