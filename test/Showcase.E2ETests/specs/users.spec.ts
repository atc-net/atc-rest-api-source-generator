import { test, expect } from '../fixtures/test';
import { UsersPage } from '../page-objects/users.page';

test.describe('Users Page', () => {
  let usersPage: UsersPage;

  test.beforeEach(async ({ page }) => {
    usersPage = new UsersPage(page);
    await usersPage.goto();
  });

  test('displays heading', async () => {
    await expect(usersPage.heading).toBeVisible();
  });

  test('shows filter controls', async ({ page }) => {
    // Verify search input exists (Blazor: "Search (name, email)", React: "Search")
    await expect(page.getByLabel(/search/i).first()).toBeVisible();
  });

  test('shows create user button', async ({ page }) => {
    // Blazor: "New User", React: "Create User"
    await expect(page.getByText(/create user|new user/i).first()).toBeVisible();
  });

  test('can load users via search', async () => {
    await usersPage.searchButton.click();
    await expect(usersPage.table).toBeVisible();
  });
});
