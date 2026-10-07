import { test, expect } from '../fixtures/test';
import { apiGet, unique } from '../fixtures/api';
import { UserFormPage } from '../page-objects/user-form.page';
import { UserDetailsPage } from '../page-objects/user-details.page';

type User = { id: string; firstName: string; lastName: string; email: string; address: { city: string; country: string } };

const guid = /\/users\/([0-9a-f-]{36})$/;

// createUser, getUserById, updateUserById and deleteUserById through the user form and the user details page
test.describe('Users (create, details, edit, delete)', () => {
  test('the create form shows its fields, and Cancel goes back to the users', async ({ page, appType }) => {
    const form = new UserFormPage(page, appType);
    await form.gotoCreate();
    await expect(form.heading).toBeVisible();
    for (const input of [form.firstNameInput, form.lastNameInput, form.emailInput, form.streetInput, form.cityInput, form.postalCodeInput, form.countryInput]) {
      await expect(input).toBeVisible();
    }
    await form.cancelButton.click();
    await expect(page).toHaveURL(/\/users$/);
  });

  test('creates a user, shows it, edits it and deletes it - and the API agrees at every step', async ({ page, request, appType }, testInfo) => {
    const form = new UserFormPage(page, appType);
    const details = new UserDetailsPage(page);
    const stamp = unique('', testInfo).trim().replace(/\s+/g, '-').toLowerCase();
    const user = {
      firstName: 'Mary',
      lastName: 'Jackson',
      email: `mary.${stamp}@example.com`,
      street: '1 Langley Blvd',
      city: 'Hampton',
      postalCode: '23681',
      country: 'United States',
    };

    // Create: the app opens the new user's details page
    await form.gotoCreate();
    await form.fill(user);
    await form.submit();
    await expect(page).toHaveURL(guid);
    const userId = guid.exec(new URL(page.url()).pathname)![1];
    await expect(details.nameHeading('Mary Jackson')).toBeVisible();
    await expect(page.getByText(user.email).first()).toBeVisible();

    const created = await apiGet<User>(request, `/users/${userId}`);
    expect(created.status).toBe(200);
    expect(created.body).toMatchObject({ id: userId, firstName: 'Mary', lastName: 'Jackson', email: user.email, address: { city: 'Hampton', country: 'United States' } });

    // Edit: the form opens with the user, and the details show the new name
    await details.editButton.click();
    await expect(page).toHaveURL(new RegExp(`/users/${userId}/edit$`));
    await expect(form.heading).toHaveText(/edit user/i);
    await expect(form.firstNameInput).toHaveValue('Mary');
    await form.lastNameInput.fill('Winston Jackson');
    await form.cityInput.fill('Newport News');
    await form.submit();
    await expect(page).toHaveURL(new RegExp(`/users/${userId}$`));
    await expect(details.nameHeading('Mary Winston Jackson')).toBeVisible();

    const updated = await apiGet<User>(request, `/users/${userId}`);
    expect(updated.body).toMatchObject({ lastName: 'Winston Jackson', email: user.email, address: { city: 'Newport News' } });

    // Delete: back on the users page, and the API no longer has the user
    expect(await details.delete()).toBe(204);
    await expect(page).toHaveURL(/\/users$/);
    expect((await apiGet(request, `/users/${userId}`)).status).toBe(404);
    const search = await apiGet<User[]>(request, `/users?search=${encodeURIComponent(user.email)}`);
    expect(search.body).toEqual([]);
  });
});
