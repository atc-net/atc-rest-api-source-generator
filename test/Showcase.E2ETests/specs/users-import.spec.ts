import { test, expect } from '../fixtures/test';
import { apiBaseUrl } from '../ports';

const demoToken = 'demo';

// A list of users as XML, the second one with the given first name
const usersXml = (stamp: string, secondFirstName: string) => `<users>
  <user><firstName>Katherine</firstName><lastName>Johnson</lastName><email>katherine.${stamp}@example.com</email><dateOfBirth>1918-08-26</dateOfBirth><role>Employee</role><address><street>1 Langley Blvd</street><city>Hampton</city><postalCode>23681</postalCode><country>United States</country></address></user>
  <user><firstName>${secondFirstName}</firstName><lastName>Vaughan</lastName><email>dorothy.${stamp}@example.com</email><dateOfBirth>1910-09-20</dateOfBirth><role>Manager</role><address><street>2 Langley Blvd</street><city>Hampton</city><postalCode>23681</postalCode><country>United States</country></address></user>
</users>`;

test.describe('Users import', () => {
  test('imports a list of users sent as XML, and the server validates every user', async ({ page, request, browserErrors }, testInfo) => {
    browserErrors.expectStatus(400, '/api/v1/users/import', 'the last import sends an invalid user on purpose');
    await page.goto('/users');
    const xml = page.getByTestId('users-import-xml');
    const status = page.getByTestId('users-import-status');

    // The list the generated XML mapping wrote: a <users> root with a <user> per item
    await expect(xml).toHaveValue(/^<users>\s*<user>\s*<firstName>Ada<\/firstName>/);
    await page.getByTestId('users-import').click();
    await expect(status).toHaveText('Imported 2 users from XML');
    const rows = page.getByTestId('users-import-row');
    await expect(rows).toHaveCount(2);
    await expect(rows.nth(0)).toContainText('Ada Lovelace · ada.');
    await expect(rows.nth(1)).toContainText('Grace Hopper · grace.');

    // The users exist: the API finds the imported email
    const email = ((await rows.nth(1).textContent()) ?? '').split('· ')[1]!.trim();
    const found = await request.get(`${apiBaseUrl}/users?search=${encodeURIComponent(email)}`, { headers: { Authorization: `Bearer ${demoToken}` } });
    expect(found.ok()).toBeTruthy();
    expect(((await found.json()) as { email: string }[]).map((u) => u.email)).toEqual([email]);

    // A list of our own
    const stamp = `${testInfo.project.name}-${Date.now()}`;
    await xml.fill(usersXml(stamp, 'Dorothy'));
    await page.getByTestId('users-import').click();
    await expect(status).toHaveText('Imported 2 users from XML');
    await expect(rows).toHaveText([`Katherine Johnson · katherine.${stamp}@example.com`, `Dorothy Vaughan · dorothy.${stamp}@example.com`]);

    // An invalid second user: the validation filter names that item
    await xml.fill(usersXml(`${stamp}-invalid`, 'D'));
    await page.getByTestId('users-import').click();
    await expect(status).toHaveText(/^400: \[1\]\.firstName: /);
    await expect(rows).toHaveCount(0);
  });
});
