import { test, expect } from '../fixtures/test';

const apiBaseUrl = 'http://localhost:15046/api/v1';
const demoToken = 'demo';

test.describe('User as XML', () => {
  test('the user details page shows the user as XML and reads it back into the model', async ({ page, request }) => {
    // A user, and what the API says about it as JSON
    const list = await request.get(`${apiBaseUrl}/users`, { headers: { Authorization: `Bearer ${demoToken}` } });
    expect(list.ok()).toBeTruthy();
    const body = await list.json();
    const users = Array.isArray(body) ? body : body.items ?? body.results ?? [];
    const user = users[0];
    expect(user).toBeTruthy();

    await page.goto(`/users/${user.id}`);
    await page.getByTestId('user-view-xml').click();

    // The XML the server answered with Accept: application/xml ...
    const xml = page.getByTestId('user-xml');
    await expect(xml).toContainText('<User>');
    await expect(xml).toContainText(`<email>${user.email}</email>`);
    await expect(xml).toContainText(`<city>${user.address.city}</city>`);

    // ... read back into the model by the generated XML mapping
    await expect(page.getByTestId('user-xml-parsed')).toHaveText(
      `Read back from the XML: ${user.firstName} ${user.lastName} · ${user.email} · ${user.address.city}`,
    );
  });
});