import { test, expect } from '../fixtures/test';
import { UserFormPage } from '../page-objects/user-form.page';

test.describe('User Details Page', () => {
  test('create form renders with required fields', async ({ page }) => {
    const userFormPage = new UserFormPage(page);
    await userFormPage.gotoCreate();
    await expect(userFormPage.heading).toBeVisible();
    await expect(userFormPage.firstNameInput).toBeVisible();
    await expect(userFormPage.lastNameInput).toBeVisible();
    await expect(userFormPage.emailInput).toBeVisible();
    await expect(userFormPage.submitButton).toBeVisible();
  });
});
