import { test, expect } from '../fixtures/test';
import { HomePage } from '../page-objects/home.page';

test.describe('Home Page', () => {
  let homePage: HomePage;

  test.beforeEach(async ({ page }) => {
    homePage = new HomePage(page);
    await homePage.goto();
  });

  test('displays heading', async () => {
    await expect(homePage.heading).toBeVisible();
  });

  test('shows feature cards with section names', async ({ page }) => {
    // Both apps show card titles for all 4 sections
    for (const section of ['Accounts', 'Tasks', 'Users', 'Files']) {
      await expect(page.getByText(section).first()).toBeVisible();
    }
  });

  test('section pages are reachable', async ({ page }) => {
    // Verify one section page loads from the home page
    await page.goto('/accounts');
    await expect(page.getByRole('heading', { name: /accounts/i }).first()).toBeVisible();
  });
});
