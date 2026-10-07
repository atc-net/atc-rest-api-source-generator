import { test, expect } from '../fixtures/test';
import { HomePage } from '../page-objects/home.page';
import { NavigationPage } from '../page-objects/navigation.page';

test.describe('Home Page', () => {
  let homePage: HomePage;

  test.beforeEach(async ({ page }) => {
    homePage = new HomePage(page);
    await homePage.goto();
    // The app is up before a test navigates on: leaving a page that still loads cancels its downloads
    await expect(homePage.heading).toBeVisible();
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

  test('section pages are reachable', async ({ page, appType }) => {
    // One section page, reached from the home page through the app's own navigation (a new page load would cancel
    // what the home page still loads, its web fonts)
    await new NavigationPage(page, appType).clickNavItem('List Accounts');
    await expect(page).toHaveURL(/\/accounts$/);
    await expect(page.getByRole('heading', { name: /accounts/i }).first()).toBeVisible();
  });
});
