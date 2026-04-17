import { test, expect } from '../fixtures/test';
import { NavigationPage } from '../page-objects/navigation.page';

const navItems = ['List Accounts', 'List Tasks', 'Download Files'];

test.describe('Navigation', () => {
  let nav: NavigationPage;

  test.beforeEach(async ({ page, appType }) => {
    nav = new NavigationPage(page, appType);
    await page.goto('/');
  });

  test('all navigation items are visible', async () => {
    for (const label of navItems) {
      const locator = await nav.isNavItemVisible(label);
      await expect(locator).toBeVisible();
    }
  });

  test('clicking a nav item navigates to correct URL', async ({ page }) => {
    await nav.clickNavItem('List Accounts');
    await expect(page).toHaveURL(/accounts/i);
  });
});
