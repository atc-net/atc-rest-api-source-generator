import { test, expect } from '../fixtures/test';
import { demoHeaders, uniqueSurname } from '../fixtures/api';
import { apiBaseUrl } from '../ports';
import { UserFormPage } from '../page-objects/user-form.page';

const address = { street: '1 Langley Blvd', city: 'Hampton', postalCode: '23681', country: 'United States' };

// The server's 400 in the user form: each error at its field - the domain's (FluentValidation) and the specification's
// (DataAnnotations) alike - not one line for the whole form; the form stays open with what was typed
test.describe('Users validation (form)', () => {
  test('a name another user has is marked at the last name, after the half-second lookup', async ({ page, request, appType, browserErrors }, testInfo) => {
    browserErrors.expectStatus(400, '/users', 'the form sends a user whose name is taken, on purpose');
    const lastName = uniqueSurname('Vaughan');
    const stamp = `${testInfo.project.name}-${Date.now()}`;
    const existing = await request.post(`${apiBaseUrl}/users`, {
      headers: demoHeaders,
      data: { firstName: 'Dorothy', lastName, email: `dorothy.${stamp}@example.com`, dateOfBirth: '1910-09-20', role: 'Employee', address },
    });
    expect(existing.status()).toBe(201);

    const form = new UserFormPage(page, appType);
    await form.gotoCreate();
    await form.fill({ firstName: 'Dorothy', lastName, email: `dorothy.again.${stamp}@example.com`, ...address });
    await form.submit();

    await expect(page.getByText(`A user named Dorothy ${lastName} already exists.`).first()).toBeVisible();
    await expect(page).toHaveURL(/\/users\/create$/);
    await expect(form.lastNameInput).toHaveValue(lastName);
    await expect(form.lastNameInput).toHaveAttribute('aria-invalid', 'true');
    await expect(form.firstNameInput).not.toHaveAttribute('aria-invalid', 'true');
  });

  test('a phone number the specification refuses is marked at the phone field', async ({ page, appType, browserErrors }, testInfo) => {
    browserErrors.expectStatus(400, '/users', 'the form sends a phone number that breaks the pattern, on purpose');
    const form = new UserFormPage(page, appType);
    await form.gotoCreate();
    await form.fill({ firstName: 'Dorothy', lastName: uniqueSurname('Vaughan'), email: `dorothy.${testInfo.project.name}-${Date.now()}@example.com`, ...address });
    // Valid to the form's own checks, refused by the specification's pattern on the server
    await form.phoneInput.fill('12-34-56');
    await form.submit();

    await expect(page.getByText(/\^\\\+\?\[1-9\]\\d\{6,14\}\$/).first()).toBeVisible();
    await expect(page).toHaveURL(/\/users\/create$/);
    await expect(form.phoneInput).toHaveAttribute('aria-invalid', 'true');
  });
});
