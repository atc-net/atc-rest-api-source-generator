import { test, expect } from '../fixtures/test';
import { UserFormPage } from '../page-objects/user-form.page';

test.describe('User Form Page', () => {
  let userFormPage: UserFormPage;

  test.beforeEach(async ({ page }) => {
    userFormPage = new UserFormPage(page);
    await userFormPage.gotoCreate();
  });

  test('displays create heading', async () => {
    await expect(userFormPage.heading).toBeVisible();
  });

  test('shows required fields', async () => {
    await expect(userFormPage.firstNameInput).toBeVisible();
    await expect(userFormPage.lastNameInput).toBeVisible();
    await expect(userFormPage.emailInput).toBeVisible();
  });

  test('submit button exists', async () => {
    await expect(userFormPage.submitButton).toBeVisible();
  });

  test('cancel button navigates back to users', async ({ page }) => {
    await userFormPage.cancelButton.click();
    await expect(page).toHaveURL(/\/users/);
  });
});
